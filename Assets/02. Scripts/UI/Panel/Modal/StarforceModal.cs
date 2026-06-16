using System;
using System.Collections.Generic;
using Fusion;
using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class StarforceModal : ModalBase
{
    public override EUIType UIType => EUIType.StarforceModal;
    public const int MAX_PARTICIPANTS = 5;

    const float MIN_PASS_SECONDS = 1.2f;
    const float MAX_PASS_SECONDS = 2.4f;

    static readonly Color ActiveNameColor = new(1f, 0.92f, 0.35f, 1f);
    static readonly Color PendingNameColor = new(0.75f, 0.75f, 0.75f, 1f);
    static readonly Color SuccessNameColor = new(0.45f, 0.95f, 0.55f, 1f);
    static readonly Color FailNameColor = new(1f, 0.45f, 0.45f, 1f);

    [SerializeField] RectTransform _trackRect;
    [SerializeField] RectTransform _successZoneRect;
    [SerializeField] StarforceStar[] _stars = new StarforceStar[MAX_PARTICIPANTS];
    [SerializeField] Color[] _starColors = new Color[MAX_PARTICIPANTS];
    [SerializeField] TextMeshProUGUI _turnStatusTMP;
    [SerializeField] TextMeshProUGUI[] _participantNameTMPs = new TextMeshProUGUI[MAX_PARTICIPANTS];

    readonly List<PlayerRef> _participants = new(MAX_PARTICIPANTS);

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;

    bool _isZoneCached;
    float _zoneMinRatio;
    float _zoneMaxRatio;

    int _localParticipantIndex = -1;
    int _cachedTurnIndex = -1;
    int _processedTurnIndex = -1;
    float _turnStartTime = -1f;
    float _turnCycleSeconds;
    bool _isTurnResolved;
    bool _hasRequestedClose;

    StarforceStar ActiveStar => _stars != null && _stars.Length > 0 ? _stars[0] : null;

    public void OpenModal()
    {
        OpenPanel();
    }

    protected override void OnModalOpened()
    {
        _cachedTurnIndex = -1;
        _processedTurnIndex = -1;
        _turnStartTime = -1f;
        _turnCycleSeconds = 0f;
        _isTurnResolved = false;
        _hasRequestedClose = false;
        _isZoneCached = false;

        EnsureParticipantNameUI();
        BuildParticipants();
        HideExtraStars();
        UpdateParticipantLabels(0, EStarforceResult.None);

        if (Network != null && Network.IsServerHost)
        {
            ServerInitSession();
        }

        PlayerNetworkObject.InGameDataChanged += HandleNetworkDataChanged;
    }

    protected override void OnModalClosed()
    {
        PlayerNetworkObject.InGameDataChanged -= HandleNetworkDataChanged;

        _participants.Clear();
        _localParticipantIndex = -1;
        _cachedTurnIndex = -1;
        _turnStartTime = -1f;
    }

    void Update()
    {
        if (_participants.Count == 0 || !TryGetSessionState(out int turnIndex, out EStarforceResult sessionResult))
        {
            return;
        }

        if (sessionResult != EStarforceResult.None)
        {
            UpdateParticipantLabels(turnIndex, sessionResult);
            UpdateTurnStatus(turnIndex, sessionResult);
            TryRequestClose();
            return;
        }

        if (turnIndex != _cachedTurnIndex)
        {
            OnTurnChanged(turnIndex);
        }

        TryCacheTurnTiming(turnIndex);

        if (_turnStartTime < 0f || ActiveStar == null)
        {
            UpdateParticipantLabels(turnIndex, sessionResult);
            UpdateTurnStatus(turnIndex, sessionResult);
            return;
        }

        float elapsed = Mathf.Max(0f, CurrentSimulationTime - _turnStartTime);
        float cycleSeconds = _turnCycleSeconds > 0f ? _turnCycleSeconds : MIN_PASS_SECONDS;
        float ratio = Mathf.Clamp01(elapsed / cycleSeconds);
        float anchoredX = RatioToAnchoredX(ratio);

        ActiveStar.MoveTo(anchoredX);

        if (Network != null && Network.IsServerHost)
        {
            TryHostTimeout(turnIndex, elapsed, cycleSeconds);
            TryProcessTurnResult(turnIndex);
        }

        if (IsLocalActiveTurn(turnIndex) && !_isTurnResolved)
        {
            HandleLocalInput(ratio, anchoredX);
        }

        UpdateParticipantLabels(turnIndex, sessionResult);
        UpdateTurnStatus(turnIndex, sessionResult);
    }

    float CurrentSimulationTime =>
        Network != null && Network.TryGetAliveRunner(out NetworkRunner runner) ? runner.SimulationTime : 0f;

    void BuildParticipants()
    {
        _participants.Clear();
        _localParticipantIndex = -1;

        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return;
        }

        var active = new List<PlayerRef>(Network.GetActivePlayers());
        active.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));

        int count = Mathf.Min(active.Count, MAX_PARTICIPANTS);
        for (int i = 0; i < count; i++)
        {
            _participants.Add(active[i]);

            if (active[i] == runner.LocalPlayer)
            {
                _localParticipantIndex = i;
            }
        }
    }

    void HideExtraStars()
    {
        if (_stars == null)
        {
            return;
        }

        for (int i = 0; i < _stars.Length; i++)
        {
            if (_stars[i] == null)
            {
                continue;
            }

            _stars[i].SetVisible(i == 0);
        }
    }

    void OnTurnChanged(int turnIndex)
    {
        _cachedTurnIndex = turnIndex;
        _processedTurnIndex = -1;
        _turnStartTime = -1f;
        _turnCycleSeconds = 0f;
        _isTurnResolved = false;

        SetupActiveStar(turnIndex);
        TryCacheTurnTiming(turnIndex);
    }

    void SetupActiveStar(int turnIndex)
    {
        if (ActiveStar == null || turnIndex < 0 || turnIndex >= _participants.Count)
        {
            return;
        }

        Color color = Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject)
            ? GetColor(playerObject.StarforceColor)
            : GetColor((EStarColor)(turnIndex % _starColors.Length));

        ActiveStar.ResetStar(color);
        ActiveStar.MoveTo(RatioToAnchoredX(0f));
    }

    void ServerInitSession()
    {
        if (!Network.TryGetAliveRunner(out NetworkRunner runner) || !Players.TryGetHost(out PlayerNetworkObject host))
        {
            return;
        }

        host.StarforceTurnIndex = 0;
        host.StarforceSessionResult = EStarforceResult.None;

        for (int i = 0; i < _participants.Count; i++)
        {
            if (!Players.TryGet(_participants[i], out PlayerNetworkObject playerObject))
            {
                continue;
            }

            playerObject.StarforceColor = (EStarColor)(i % _colorCount);
            playerObject.StarforceResult = EStarforceResult.None;
            playerObject.StarforceStopRatio = 0f;
            playerObject.StarforceStartTime = 0f;
            playerObject.StarforceCycleSeconds = 0f;
        }

        ServerBeginTurn(0);
    }

    int _colorCount => _starColors != null && _starColors.Length > 0 ? _starColors.Length : MAX_PARTICIPANTS;

    void ServerBeginTurn(int turnIndex)
    {
        if (!Network.TryGetAliveRunner(out NetworkRunner runner) || turnIndex < 0 || turnIndex >= _participants.Count)
        {
            return;
        }

        if (!Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject))
        {
            return;
        }

        playerObject.StarforceColor = (EStarColor)(turnIndex % _colorCount);
        playerObject.StarforceResult = EStarforceResult.None;
        playerObject.StarforceStopRatio = 0f;
        playerObject.StarforceStartTime = runner.SimulationTime;
        playerObject.StarforceCycleSeconds = UnityEngine.Random.Range(MIN_PASS_SECONDS, MAX_PASS_SECONDS);
    }

    void ServerCompleteTurn(EStarforceResult result, float stopRatio)
    {
        if (!Players.TryGetHost(out PlayerNetworkObject host))
        {
            return;
        }

        int turnIndex = host.StarforceTurnIndex;
        if (turnIndex < 0 || turnIndex >= _participants.Count)
        {
            return;
        }

        if (!Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject))
        {
            return;
        }

        playerObject.StarforceResult = result;
        playerObject.StarforceStopRatio = stopRatio;

        if (result == EStarforceResult.Fail)
        {
            host.StarforceSessionResult = EStarforceResult.Fail;
            return;
        }

        int nextTurn = turnIndex + 1;
        if (nextTurn >= _participants.Count)
        {
            host.StarforceSessionResult = EStarforceResult.Success;
            return;
        }

        host.StarforceTurnIndex = nextTurn;
        ServerBeginTurn(nextTurn);
    }

    void TryCacheTurnTiming(int turnIndex)
    {
        if (turnIndex < 0 || turnIndex >= _participants.Count)
        {
            return;
        }

        if (!Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject))
        {
            return;
        }

        if (playerObject.StarforceStartTime <= 0f)
        {
            return;
        }

        _turnStartTime = playerObject.StarforceStartTime;
        if (playerObject.StarforceCycleSeconds > 0f)
        {
            _turnCycleSeconds = playerObject.StarforceCycleSeconds;
        }
    }

    void TryHostTimeout(int turnIndex, float elapsed, float cycleSeconds)
    {
        if (elapsed < cycleSeconds)
        {
            return;
        }

        if (!Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject))
        {
            return;
        }

        if (playerObject.StarforceResult != EStarforceResult.None)
        {
            return;
        }

        playerObject.StarforceResult = EStarforceResult.Fail;
        playerObject.StarforceStopRatio = 1f;
    }

    void TryProcessTurnResult(int turnIndex)
    {
        if (Network == null || !Network.IsServerHost)
        {
            return;
        }

        if (!TryGetSessionState(out int currentTurn, out EStarforceResult sessionResult) || sessionResult != EStarforceResult.None)
        {
            return;
        }

        if (currentTurn != turnIndex || _processedTurnIndex == turnIndex)
        {
            return;
        }

        if (!Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject))
        {
            return;
        }

        if (playerObject.StarforceResult == EStarforceResult.None)
        {
            return;
        }

        _processedTurnIndex = turnIndex;
        ServerCompleteTurn(playerObject.StarforceResult, playerObject.StarforceStopRatio);
    }

    bool IsLocalActiveTurn(int turnIndex) => _localParticipantIndex >= 0 && _localParticipantIndex == turnIndex;

    void HandleLocalInput(float ratio, float anchoredX)
    {
        if (Keyboard.current == null || !Keyboard.current[Key.Space].wasPressedThisFrame)
        {
            return;
        }

        bool success = IsRatioInSuccessZone(ratio);
        SubmitLocalResult(success ? EStarforceResult.Success : EStarforceResult.Fail, ratio, anchoredX);
    }

    void SubmitLocalResult(EStarforceResult result, float ratio, float anchoredX)
    {
        _isTurnResolved = true;
        ActiveStar?.Stop(anchoredX);

        if (!Players.TryGetLocal(out PlayerNetworkObject local))
        {
            return;
        }

        if (Network.IsServerHost)
        {
            local.StarforceResult = result;
            local.StarforceStopRatio = ratio;
            TryProcessTurnResult(_cachedTurnIndex);
        }
        else
        {
            local.RPC_SetStarforceResult(result, ratio);
        }
    }

    void HandleNetworkDataChanged()
    {
        if (!TryGetSessionState(out int turnIndex, out EStarforceResult sessionResult))
        {
            return;
        }

        if (turnIndex != _cachedTurnIndex)
        {
            OnTurnChanged(turnIndex);
        }

        TryCacheTurnTiming(turnIndex);

        if (turnIndex >= 0 && turnIndex < _participants.Count &&
            Players.TryGet(_participants[turnIndex], out PlayerNetworkObject playerObject) &&
            playerObject.StarforceResult != EStarforceResult.None)
        {
            ActiveStar?.Stop(RatioToAnchoredX(playerObject.StarforceStopRatio));
        }

        if (Network != null && Network.IsServerHost)
        {
            TryProcessTurnResult(turnIndex);
        }

        UpdateParticipantLabels(turnIndex, sessionResult);
        UpdateTurnStatus(turnIndex, sessionResult);

        if (sessionResult != EStarforceResult.None)
        {
            TryRequestClose();
        }
    }

    bool TryGetSessionState(out int turnIndex, out EStarforceResult sessionResult)
    {
        turnIndex = 0;
        sessionResult = EStarforceResult.None;

        if (!Players.TryGetHost(out PlayerNetworkObject host))
        {
            return false;
        }

        turnIndex = host.StarforceTurnIndex;
        sessionResult = host.StarforceSessionResult;
        return true;
    }

    void TryRequestClose()
    {
        if (_hasRequestedClose)
        {
            return;
        }

        _hasRequestedClose = true;
        ClosePanel();
    }

    void UpdateTurnStatus(int turnIndex, EStarforceResult sessionResult)
    {
        if (_turnStatusTMP == null)
        {
            return;
        }

        if (sessionResult == EStarforceResult.Success)
        {
            _turnStatusTMP.text = "전원 성공!";
            return;
        }

        if (sessionResult == EStarforceResult.Fail)
        {
            _turnStatusTMP.text = "실패!";
            return;
        }

        if (turnIndex < 0 || turnIndex >= _participants.Count)
        {
            _turnStatusTMP.text = string.Empty;
            return;
        }

        string name = ResolveDisplayName(_participants[turnIndex]);
        _turnStatusTMP.text = $"{name}님의 차례 ({turnIndex + 1}/{_participants.Count})";
    }

    void UpdateParticipantLabels(int turnIndex, EStarforceResult sessionResult)
    {
        if (_participantNameTMPs == null)
        {
            return;
        }

        for (int i = 0; i < _participantNameTMPs.Length; i++)
        {
            TextMeshProUGUI label = _participantNameTMPs[i];
            if (label == null)
            {
                continue;
            }

            if (i >= _participants.Count)
            {
                label.gameObject.SetActive(false);
                continue;
            }

            label.gameObject.SetActive(true);
            label.text = ResolveDisplayName(_participants[i]);

            Color color = PendingNameColor;
            FontStyles style = FontStyles.Normal;

            if (Players.TryGet(_participants[i], out PlayerNetworkObject playerObject))
            {
                if (playerObject.StarforceResult == EStarforceResult.Success)
                {
                    color = SuccessNameColor;
                }
                else if (playerObject.StarforceResult == EStarforceResult.Fail)
                {
                    color = FailNameColor;
                }
                else if (sessionResult == EStarforceResult.None && i == turnIndex)
                {
                    color = ActiveNameColor;
                    style = FontStyles.Bold;
                }
            }

            label.color = color;
            label.fontStyle = style;
        }
    }

    string ResolveDisplayName(PlayerRef playerRef)
    {
        if (Players.TryGet(playerRef, out PlayerNetworkObject playerObject))
        {
            string networkName = playerObject.DisplayName.Value;
            if (!string.IsNullOrEmpty(networkName))
            {
                return networkName;
            }
        }

        return $"Player {playerRef.PlayerId}";
    }

    void EnsureParticipantNameUI()
    {
        if (_turnStatusTMP == null)
        {
            _turnStatusTMP = CreateLabel("TurnStatus", new Vector2(0f, -12f), new Vector2(640f, 36f), 28f);
        }

        if (_participantNameTMPs[0] != null)
        {
            return;
        }

        var row = new GameObject("ParticipantNames", typeof(RectTransform));
        row.transform.SetParent(transform, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -52f);
        rowRect.sizeDelta = new Vector2(640f, 32f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < MAX_PARTICIPANTS; i++)
        {
            _participantNameTMPs[i] = CreateLabel($"Name{i}", Vector2.zero, new Vector2(100f, 28f), 22f, row.transform);
        }
    }

    TextMeshProUGUI CreateLabel(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, Transform parent = null)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = PendingNameColor;
        tmp.text = string.Empty;
        return tmp;
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
        if (_isZoneCached)
        {
            return;
        }

        float trackWidth = _trackRect.rect.width;
        if (trackWidth <= 0f)
        {
            return;
        }

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
}
