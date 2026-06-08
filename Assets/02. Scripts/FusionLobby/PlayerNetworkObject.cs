using System;
using Fusion;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 세션에 참가한 플레이어 1명의 네트워크 상태를 [Networked]로 동기화합니다.
/// 로비(닉네임/준비) + 인게임(손가락 배정/입력)을 모두 보관합니다.
/// 런타임 Spawn된 NetworkObject이므로 Single 씬 로드 시 소멸하며, 게임 씬에서는
/// 호스트가 <see cref="PlayerManager.ServerEnsurePlayerObjectsCoroutine"/>로 다시 스폰합니다.
/// (재스폰 시 InputAuthority가 <see cref="Spawned"/>에서 닉네임을 재전송하므로 이름이 복원됩니다.)
/// Host 모드이므로 StateAuthority는 호스트가 가지며, 클라이언트는 RPC로 호스트에 값 변경을 요청합니다.
/// </summary>
public class PlayerNetworkObject : NetworkBehaviour
{
    /// <summary>로비 표시 정보(닉네임/준비/호스트 여부)가 바뀌면 호출됩니다.</summary>
    public static event Action LobbyDataChanged;

    /// <summary>인게임 정보(손가락 배정/입력)가 바뀌면 호출됩니다.</summary>
    public static event Action InGameDataChanged;

    [Networked] public NetworkString<_16> DisplayName { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public EFingerType AssignedFinger { get; set; }
    [Networked] public NetworkBool IsFingerExtended { get; set; }
    [Networked] public Vector2 CursorScreenPos { get; set; }

    [Networked] public EStarColor StarforceColor { get; set; }
    [Networked] public EStarforceResult StarforceResult { get; set; }
    [Networked] public float StarforceStopRatio { get; set; }
    [Networked] public float StarforceStartTime { get; set; }
    [Networked] public float StarforceCycleSeconds { get; set; }

    ChangeDetector _changeDetector;

    public int PlayerId => Object != null && Object.InputAuthority.IsRealPlayer
        ? Object.InputAuthority.PlayerId
        : -1;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);

        // 로컬 플레이어의 오브젝트라면, 내 닉네임을 호스트에게 전달한다.
        if (Object.HasInputAuthority)
        {
            string localName = App.SystemManager.Network != null
                ? App.SystemManager.Network.LocalDisplayName
                : PlayerProfile.DisplayName;

            RPC_SetDisplayName(localName);
        }

        LobbyDataChanged?.Invoke();
        InGameDataChanged?.Invoke();
    }

    public override void Render()
    {
        if (_changeDetector == null)
        {
            return;
        }

        bool lobbyChanged = false;
        bool inGameChanged = false;
        bool fingerExtendedChanged = false;

        foreach (string changed in _changeDetector.DetectChanges(this))
        {
            switch (changed)
            {
                case nameof(DisplayName):
                case nameof(IsReady):
                    lobbyChanged = true;
                    break;
                case nameof(AssignedFinger):
                    inGameChanged = true;
                    break;
                case nameof(IsFingerExtended):
                    inGameChanged = true;
                    fingerExtendedChanged = true;
                    break;
                case nameof(StarforceColor):
                case nameof(StarforceResult):
                case nameof(StarforceStopRatio):
                case nameof(StarforceStartTime):
                case nameof(StarforceCycleSeconds):
                    inGameChanged = true;
                    break;
            }
        }

#if UNITY_EDITOR
        if (fingerExtendedChanged &&
            !Object.HasInputAuthority &&
            App.IsGameScene &&
            App.SceneManager.InGame != null &&
            App.SceneManager.InGame.IsWaveActive &&
            AssignedFinger != EFingerType.None)
        {
            string displayName = DisplayName.Value;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = $"Player {PlayerId}";
            }

            Debug.Log($"[PlayerNetworkObject] 손가락 입력 — {displayName}, {AssignedFinger}, {(IsFingerExtended ? "펴기" : "접기")}");
        }
#endif

        if (lobbyChanged)
        {
            LobbyDataChanged?.Invoke();
        }

        if (inGameChanged)
        {
            InGameDataChanged?.Invoke();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        LobbyDataChanged?.Invoke();
        InGameDataChanged?.Invoke();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetDisplayName(string displayName)
    {
        DisplayName = displayName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool isReady)
    {
        IsReady = isReady;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetFingerExtended(NetworkBool isExtended)
    {
        IsFingerExtended = isExtended;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetStarforceResult(EStarforceResult result, float stopRatio)
    {
        StarforceResult = result;
        StarforceStopRatio = stopRatio;
    }
}
