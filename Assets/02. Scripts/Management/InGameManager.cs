using System;
using System.Collections;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 인게임 씬 매니저. 손가락 배정, 3초 입력 라운드, RPS 판정을 담당합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class InGameManager : SceneManagerBase
{
    public const float RoundDurationSeconds = 3f;

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;
    InputManager Input => App.SystemManager.Input;

    Coroutine _roundCoroutine;
    Coroutine _hostSetupCoroutine;
    bool _hostSetupStarted;

    public EInGamePhase Phase { get; private set; } = EInGamePhase.WaitingForSetup;
    public bool IsRoundActive => Phase == EInGamePhase.RoundInput;
    public float RoundTimeRemaining { get; private set; }
    public EFingerType LocalAssignedFinger => Players.LocalAssignedFinger;
    public bool LocalFingerExtended => Input.IsFingerExtended;
    public EHandPosition LastHandPosition { get; private set; } = EHandPosition.Invalid;

    public event Action<EFingerType> OnLocalAssignedFingerChanged;
    public event Action<bool> OnLocalFingerExtendedChanged;
    public event Action<float> OnRoundTimerUpdated;
    public event Action<EHandPosition> OnRoundJudged;

    void OnEnable()
    {
        Input.OnFingerToggled += HandleFingerToggled;
        Network.OnInGameSceneReady += HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived += HandleFingerAssignmentsReceived;
        Network.OnRoundStarted += HandleRoundStarted;
        Network.OnRoundResultReceived += HandleRoundResultReceived;
    }

    void OnDisable()
    {
        Input.OnFingerToggled -= HandleFingerToggled;
        Network.OnInGameSceneReady -= HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived -= HandleFingerAssignmentsReceived;
        Network.OnRoundStarted -= HandleRoundStarted;
        Network.OnRoundResultReceived -= HandleRoundResultReceived;
    }

    void Start()
    {
        BeginInGameSetup();
    }

    void BeginInGameSetup()
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        _hostSetupStarted = false;
        Phase = EInGamePhase.WaitingForSetup;
        LastHandPosition = EHandPosition.Invalid;
        RoundTimeRemaining = 0f;
        Input.SetEnabled(false);
        Input.ResetFingerState();
        Network.ResetInGameRoundNotification();
    }

    void HandleInGameSceneReady()
    {
        TryBeginHostSetup();
    }

    void TryBeginHostSetup()
    {
        if (_hostSetupStarted || !Network.IsServerHost || Phase != EInGamePhase.WaitingForSetup)
        {
            return;
        }

        _hostSetupStarted = true;
        _hostSetupCoroutine = StartCoroutine(HostSetupCoroutine());
    }

    /// <summary>
    /// 호스트: player object 재확보 → 손가락 배정 → 라운드 시작.
    /// 클라이언트: <see cref="HandleFingerAssignmentsReceived"/> 경로로 진행.
    /// </summary>
    IEnumerator HostSetupCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();

        Network.ServerInitializeFingerAssignments();
        yield return null;

        ApplyAssignmentsReady();
        Network.ServerStartRound(RoundDurationSeconds);
        _hostSetupCoroutine = null;
    }

    void HandleFingerAssignmentsReceived()
    {
        ApplyAssignmentsReady();
    }

    void ApplyAssignmentsReady()
    {
        if (Phase != EInGamePhase.WaitingForSetup || LocalAssignedFinger == EFingerType.None)
        {
            return;
        }

        Phase = EInGamePhase.AssignmentsReady;
        OnLocalAssignedFingerChanged?.Invoke(LocalAssignedFinger);
    }

    void HandleFingerToggled(bool isExtended)
    {
        if (!IsRoundActive || LocalAssignedFinger == EFingerType.None)
        {
            return;
        }

        Network.ClientSendFingerState(isExtended);
        OnLocalFingerExtendedChanged?.Invoke(isExtended);

#if UNITY_EDITOR
        Debug.Log($"[InGameManager] 손가락 입력 — {Players.LocalDisplayName}, {LocalAssignedFinger}, {(isExtended ? "펴기" : "접기")}");
#endif
    }

    void HandleRoundStarted(float durationSeconds)
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        if (_roundCoroutine != null)
        {
            StopCoroutine(_roundCoroutine);
        }

        _roundCoroutine = StartCoroutine(RoundTimerCoroutine(durationSeconds));
    }

    IEnumerator RoundTimerCoroutine(float durationSeconds)
    {
        Phase = EInGamePhase.RoundInput;
        RoundTimeRemaining = durationSeconds;
        Input.ResetFingerState();
        Input.SetEnabled(true);
        OnRoundTimerUpdated?.Invoke(RoundTimeRemaining);

        while (RoundTimeRemaining > 0f)
        {
            RoundTimeRemaining -= Time.deltaTime;
            OnRoundTimerUpdated?.Invoke(Mathf.Max(0f, RoundTimeRemaining));
            yield return null;
        }

        RoundTimeRemaining = 0f;
        OnRoundTimerUpdated?.Invoke(0f);
        Input.SetEnabled(false);
        Phase = EInGamePhase.RoundJudging;

        if (Network.IsServerHost)
        {
            Network.ServerJudgeAndBroadcastRoundResult();
        }
    }

    void HandleRoundResultReceived(EHandPosition handPosition)
    {
        LastHandPosition = handPosition;
        Phase = EInGamePhase.RoundComplete;
        OnRoundJudged?.Invoke(handPosition);
    }
}
