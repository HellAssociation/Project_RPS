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
    const float SetupDelaySeconds = 0.5f;

    NetworkManager Network => App.Game.Network;
    PlayerManager Players => App.Game.Manager;
    InputManager Input => App.SystemManager.Input;

    Coroutine _roundCoroutine;
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
        App.OnSceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        Input.OnFingerToggled -= HandleFingerToggled;
        Network.OnInGameSceneReady -= HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived -= HandleFingerAssignmentsReceived;
        Network.OnRoundStarted -= HandleRoundStarted;
        Network.OnRoundResultReceived -= HandleRoundResultReceived;
        App.OnSceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(EScene scene)
    {
        if (scene != EScene.InGame)
        {
            return;
        }

        BeginInGameSetup();
        StartCoroutine(BeginAsHostCoroutine());
    }

    void HandleInGameSceneReady()
    {
        if (!Network.IsServerHost)
        {
            return;
        }

        StartCoroutine(BeginAsHostCoroutine());
    }

    void BeginInGameSetup()
    {
        _hostSetupStarted = false;
        Phase = EInGamePhase.WaitingForSetup;
        LastHandPosition = EHandPosition.Invalid;
        RoundTimeRemaining = 0f;
        Input.SetEnabled(false);
        Input.ResetFingerState();
        Players.SyncGamePlayersFromNetwork();
        Players.ResetInGameState();
    }

    IEnumerator BeginAsHostCoroutine()
    {
        if (_hostSetupStarted || Phase != EInGamePhase.WaitingForSetup)
        {
            yield break;
        }

        _hostSetupStarted = true;
        yield return new WaitForSeconds(SetupDelaySeconds);

        if (!Network.IsServerHost || Phase != EInGamePhase.WaitingForSetup)
        {
            yield break;
        }

        Network.ServerInitializeFingerAssignments();
    }

    void HandleFingerAssignmentsReceived(System.Collections.Generic.IReadOnlyDictionary<int, EFingerType> assignments)
    {
        Phase = EInGamePhase.AssignmentsReady;
        OnLocalAssignedFingerChanged?.Invoke(LocalAssignedFinger);

        Debug.Log($"[InGameManager] 손가락 배정 완료 — 로컬: {LocalAssignedFinger}");

        if (Network.IsServerHost)
        {
            Network.ServerStartRound(RoundDurationSeconds);
        }
    }

    void HandleFingerToggled(bool isExtended)
    {
        if (!IsRoundActive || LocalAssignedFinger == EFingerType.None)
        {
            return;
        }

        Players.SetLocalFingerExtended(isExtended);
        Network.ClientSendFingerState(isExtended);
        OnLocalFingerExtendedChanged?.Invoke(isExtended);
    }

    void HandleRoundStarted(float durationSeconds)
    {
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
        Players.SetLocalFingerExtended(false);
        OnRoundTimerUpdated?.Invoke(RoundTimeRemaining);

        Debug.Log($"[InGameManager] 라운드 시작 — {durationSeconds:0.#}초");

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

        Debug.Log("[InGameManager] 입력 종료 — 판정 중");

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

        Debug.Log($"[InGameManager] 판정 — {handPosition}");
    }
}
