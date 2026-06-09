using System;
using System.Collections.Generic;
using Fusion;
using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 메이플스토리 스타포스 형태(좌우 이동)의 모달 콘텐츠.
/// Frame/Track/SuccessZone은 화면에 하나만 두고, 그 위에서 색깔이 다른 별(최대 5개)이
/// 함께 움직이며 각자 스페이스를 누른 시점에 멈춘다 — 화면 겹침 문제 자체가 없다.
///
/// 별은 호스트가 정한 시작 시각(StarforceStartTime)을 기준으로 모든 클라이언트가
/// 동일한 비율로 계산해 움직이므로 매 프레임 위치를 주고받지 않아도 동기화된다.
/// 스페이스를 누른 시점의 결과(성공/실패, 정지 비율)만 PlayerNetworkObject로 동기화한다.
/// </summary>
public class StarforceModalContent : ModalContent
{
    public const int MAX_PARTICIPANTS = 5;

    const float MIN_CYCLE_SECONDS = 1.2f;
    const float MAX_CYCLE_SECONDS = 2.4f;

    [SerializeField] RectTransform _trackRect;
    [SerializeField] RectTransform _successZoneRect;
    [SerializeField] StarforceStar[] _stars = new StarforceStar[MAX_PARTICIPANTS];
    [SerializeField] Color[] _starColors = new Color[MAX_PARTICIPANTS];

    static readonly EStarColor[] _colorPool = (EStarColor[])Enum.GetValues(typeof(EStarColor));

    readonly List<PlayerRef> _participants = new(MAX_PARTICIPANTS);
    readonly float[] _cycleSeconds = new float[MAX_PARTICIPANTS];

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;

    bool _isZoneCached;
    float _zoneMinRatio;
    float _zoneMaxRatio;

    int _localSlotIndex = -1;
    float _startTime = -1f;
    bool _isLocalFinished;
    bool _hasRequestedClose;

    public override void Activate()
    {
        _startTime = -1f;
        _isLocalFinished = false;
        _hasRequestedClose = false;
        Array.Clear(_cycleSeconds, 0, _cycleSeconds.Length);

        BuildParticipants();
        SetupStars();

        if (Network != null && Network.IsServerHost)
        {
            ServerAssignChallenge();
        }

        TryCacheStartTime();

        PlayerNetworkObject.InGameDataChanged += HandleNetworkDataChanged;
    }

    public override void Deactivate()
    {
        PlayerNetworkObject.InGameDataChanged -= HandleNetworkDataChanged;

        _participants.Clear();
        _localSlotIndex = -1;
        _startTime = -1f;
    }

    void Update()
    {
        if (_startTime < 0f || _participants.Count == 0)
        {
            return;
        }

        TryCacheCycleSeconds();

        float elapsed = Mathf.Max(0f, CurrentSimulationTime - _startTime);
        float localRatio = 0f;
        float localAnchoredX = 0f;

        for (int i = 0; i < _participants.Count; i++)
        {
            float cycleSeconds = _cycleSeconds[i] > 0f ? _cycleSeconds[i] : MIN_CYCLE_SECONDS;
            float ratio = Mathf.PingPong(elapsed / cycleSeconds, 1f);
            float anchoredX = RatioToAnchoredX(ratio);

            _stars[i].MoveTo(anchoredX);

            if (i == _localSlotIndex)
            {
                localRatio = ratio;
                localAnchoredX = anchoredX;
            }
        }

        HandleLocalInput(localRatio, localAnchoredX);
    }

    float CurrentSimulationTime =>
        Network != null && Network.TryGetAliveRunner(out NetworkRunner runner) ? runner.SimulationTime : 0f;

    void BuildParticipants()
    {
        _participants.Clear();
        _localSlotIndex = -1;

        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return;
        }

        var active = new List<PlayerRef>(Network.GetActivePlayers());
        active.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));

        int count = Mathf.Min(active.Count, _stars.Length);
        for (int i = 0; i < count; i++)
        {
            _participants.Add(active[i]);

            if (active[i] == runner.LocalPlayer)
            {
                _localSlotIndex = i;
            }
        }
    }

    void SetupStars()
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            bool isActive = i < _participants.Count;
            _stars[i].SetVisible(isActive);

            if (!isActive) continue;

            Color color = Players.TryGet(_participants[i], out PlayerNetworkObject playerObject)
                ? GetColor(playerObject.StarforceColor)
                : Color.white;

            _stars[i].ResetStar(color);
            _stars[i].MoveTo(RatioToAnchoredX(0f));
        }
    }

    /// <summary>호스트가 참가자별 색상을 무작위로 배정하고 공통 시작 시각을 기록한다.</summary>
    void ServerAssignChallenge()
    {
        if (!Network.TryGetAliveRunner(out NetworkRunner runner)) return;

        EStarColor[] shuffled = ShuffleColors();
        float startTime = runner.SimulationTime;

        for (int i = 0; i < _participants.Count; i++)
        {
            if (!Players.TryGet(_participants[i], out PlayerNetworkObject playerObject)) continue;

            playerObject.StarforceColor        = shuffled[i % shuffled.Length];
            playerObject.StarforceResult       = EStarforceResult.None;
            playerObject.StarforceStopRatio    = 0f;
            playerObject.StarforceStartTime    = startTime;
            playerObject.StarforceCycleSeconds = UnityEngine.Random.Range(MIN_CYCLE_SECONDS, MAX_CYCLE_SECONDS);
        }
    }

    void TryCacheStartTime()
    {
        if (_startTime >= 0f || _localSlotIndex < 0) return;
        if (!Players.TryGet(_participants[_localSlotIndex], out PlayerNetworkObject local)) return;
        if (local.StarforceStartTime <= 0f) return;

        _startTime = local.StarforceStartTime;
    }

    /// <summary>참가자별로 호스트가 배정한 이동 주기를 한 번씩 캐싱한다.</summary>
    void TryCacheCycleSeconds()
    {
        for (int i = 0; i < _participants.Count; i++)
        {
            if (_cycleSeconds[i] > 0f) continue;
            if (!Players.TryGet(_participants[i], out PlayerNetworkObject playerObject)) continue;
            if (playerObject.StarforceCycleSeconds <= 0f) continue;

            _cycleSeconds[i] = playerObject.StarforceCycleSeconds;
        }
    }

    void HandleLocalInput(float ratio, float anchoredX)
    {
        if (_isLocalFinished || _localSlotIndex < 0) return;
        if (Keyboard.current == null || !Keyboard.current[Key.Space].wasPressedThisFrame) return;

        bool success = IsRatioInSuccessZone(ratio);
        SubmitLocalResult(success ? EStarforceResult.Success : EStarforceResult.Fail, ratio, anchoredX);
    }

    void SubmitLocalResult(EStarforceResult result, float ratio, float anchoredX)
    {
        _isLocalFinished = true;
        _stars[_localSlotIndex].Stop(anchoredX);

        if (!Players.TryGetLocal(out PlayerNetworkObject local)) return;

        if (Network.IsServerHost)
        {
            local.StarforceResult    = result;
            local.StarforceStopRatio = ratio;
        }
        else
        {
            local.RPC_SetStarforceResult(result, ratio);
        }

        TryRequestCloseWhenAllFinished();
    }

    void HandleNetworkDataChanged()
    {
        TryCacheStartTime();

        for (int i = 0; i < _participants.Count; i++)
        {
            if (i == _localSlotIndex || _stars[i].IsStopped) continue;
            if (!Players.TryGet(_participants[i], out PlayerNetworkObject playerObject)) continue;
            if (playerObject.StarforceResult == EStarforceResult.None) continue;

            _stars[i].Stop(RatioToAnchoredX(playerObject.StarforceStopRatio));
        }

        TryRequestCloseWhenAllFinished();
    }

    void TryRequestCloseWhenAllFinished()
    {
        if (_hasRequestedClose || _participants.Count == 0) return;

        for (int i = 0; i < _participants.Count; i++)
        {
            if (!_stars[i].IsStopped) return;
        }

        _hasRequestedClose = true;
        RequestClose();
    }

    float RatioToAnchoredX(float ratio)
    {
        float halfWidth = _trackRect.rect.width * 0.5f;
        return Mathf.Lerp(-halfWidth, halfWidth, Mathf.Clamp01(ratio));
    }

    bool IsRatioInSuccessZone(float ratio)
    {
        EnsureZoneCached();
        return ratio >= _zoneMinRatio && ratio <= _zoneMaxRatio;
    }

    void EnsureZoneCached()
    {
        if (_isZoneCached) return;

        float trackWidth = _trackRect.rect.width;
        if (trackWidth <= 0f) return;

        float trackLeft = -trackWidth * 0.5f;
        float zoneHalfWidth = _successZoneRect.rect.width * 0.5f;
        float zoneCenter = _successZoneRect.anchoredPosition.x;

        _zoneMinRatio = Mathf.Clamp01((zoneCenter - zoneHalfWidth - trackLeft) / trackWidth);
        _zoneMaxRatio = Mathf.Clamp01((zoneCenter + zoneHalfWidth - trackLeft) / trackWidth);
        _isZoneCached = true;
    }

    Color GetColor(EStarColor color)
    {
        int index = (int)color;
        return index >= 0 && index < _starColors.Length ? _starColors[index] : Color.white;
    }

    static EStarColor[] ShuffleColors()
    {
        var colors = (EStarColor[])_colorPool.Clone();
        for (int i = colors.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (colors[i], colors[j]) = (colors[j], colors[i]);
        }

        return colors;
    }
}
