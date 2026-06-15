using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 세션 플레이어의 <see cref="PlayerNetworkObject"/> 조회·스폰·로비 스냅샷 빌드를 담당합니다.
/// [Networked] 원본은 <see cref="PlayerNetworkObject"/>이며, 여기서는 게임/UI가 쓰는 플레이어 진입점입니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class PlayerManager : CommonManagerBase
{
    public const int MAX_HP = 3;

    const string PlayerPrefabResource = "PlayerNetworkObject";

    NetworkObject _playerPrefab;

    NetworkManager Network => App.SystemManager.Network;

    /// <summary><see cref="PlayerNetworkObject"/>의 로비/인게임 [Networked] 값이 바뀔 때 호출됩니다.</summary>
    public event Action OnPlayersChanged;

    public EFingerType LocalAssignedFinger =>
        TryGetLocal(out PlayerNetworkObject local) ? local.AssignedFinger : EFingerType.None;

    public string LocalDisplayName
    {
        get
        {
            if (TryGetLocal(out PlayerNetworkObject local))
            {
                string networkName = local.DisplayName.Value;
                if (!string.IsNullOrEmpty(networkName))
                {
                    return networkName;
                }
            }

            return Network != null ? Network.LocalDisplayName : "Player";
        }
    }

    public bool IsLocalReady =>
        TryGetLocal(out PlayerNetworkObject local) && local.IsReady;

    protected override void Awake()
    {
        base.Awake();

        if (_playerPrefab == null)
        {
            _playerPrefab = Resources.Load<NetworkObject>(PlayerPrefabResource);
        }

        PlayerNetworkObject.LobbyDataChanged += HandlePlayerDataChanged;
        PlayerNetworkObject.InGameDataChanged += HandlePlayerDataChanged;
    }

    void OnDestroy()
    {
        PlayerNetworkObject.LobbyDataChanged -= HandlePlayerDataChanged;
        PlayerNetworkObject.InGameDataChanged -= HandlePlayerDataChanged;
    }

    void HandlePlayerDataChanged()
    {
        OnPlayersChanged?.Invoke();
    }

    public bool TryGetLocal(out PlayerNetworkObject player)
    {
        player = null;

        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return false;
        }

        return TryGet(runner.LocalPlayer, out player);
    }

    public bool TryGet(PlayerRef playerRef, out PlayerNetworkObject player)
    {
        player = null;

        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return false;
        }

        NetworkObject playerObject = runner.GetPlayerObject(playerRef);
        return playerObject != null && playerObject.TryGetComponent(out player);
    }

    /// <summary>게임 씬 진입 후 despawn된 player object를 호스트가 다시 스폰합니다.</summary>
    public IEnumerator ServerEnsurePlayerObjectsCoroutine()
    {
        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            yield break;
        }

        if (_playerPrefab == null)
        {
            Debug.LogError($"[PlayerManager] Player prefab not found: {PlayerPrefabResource}");
            yield break;
        }

        INetworkSceneManager sceneManager = runner.SceneManager;
        while (sceneManager != null && sceneManager.IsBusy)
        {
            yield return null;
        }

        yield return null;

        foreach (PlayerRef player in Network.GetActivePlayers())
        {
            if (runner.GetPlayerObject(player) != null)
            {
                continue;
            }

            yield return SpawnAsyncCoroutine(runner, player);
        }
    }

    IEnumerator SpawnAsyncCoroutine(NetworkRunner runner, PlayerRef player)
    {
        if (_playerPrefab == null || runner == null || !runner.IsServer || runner.GetPlayerObject(player) != null)
        {
            yield break;
        }

        bool isLocalPlayer = player == runner.LocalPlayer;
        string localName = Network != null ? Network.LocalDisplayName : string.Empty;

        NetworkSpawnOp spawnOp = runner.SpawnAsync(
            _playerPrefab,
            inputAuthority: player,
            onBeforeSpawned: (_, obj) =>
            {
                if (isLocalPlayer && obj.TryGetComponent(out PlayerNetworkObject playerObject))
                {
                    playerObject.DisplayName = localName;
                }
            });

        while (!spawnOp.IsSpawned && !spawnOp.IsFailed)
        {
            yield return null;
        }

        if (spawnOp.IsFailed)
        {
            Debug.LogError($"[PlayerManager] Failed to spawn player object for {player}.");
            yield break;
        }

        if (spawnOp.Object != null)
        {
            runner.SetPlayerObject(player, spawnOp.Object);
        }
    }

    public void Spawn(NetworkRunner runner, PlayerRef player)
    {
        if (_playerPrefab == null || runner == null || !runner.IsServer || runner.GetPlayerObject(player) != null)
        {
            return;
        }

        bool isLocalPlayer = player == runner.LocalPlayer;
        string localName = Network != null ? Network.LocalDisplayName : string.Empty;

        NetworkObject spawned = runner.Spawn(
            _playerPrefab,
            inputAuthority: player,
            onBeforeSpawned: (_, obj) =>
            {
                if (isLocalPlayer && obj.TryGetComponent(out PlayerNetworkObject playerObject))
                {
                    playerObject.DisplayName = localName;
                }
            });

        runner.SetPlayerObject(player, spawned);
    }

    public void Despawn(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        NetworkObject playerObject = runner.GetPlayerObject(player);
        if (playerObject != null)
        {
            runner.Despawn(playerObject);
        }
    }

    public bool TryGetHost(out PlayerNetworkObject host)
    {
        host = null;
        if (Network == null || !Network.TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
            return false;

        PlayerRef hostRef = ResolveHostPlayer(runner, Network.GetActivePlayers());
        return hostRef.IsRealPlayer && TryGet(hostRef, out host);
    }

    /// <summary>로비 UI용 플레이어 목록을 <see cref="PlayerNetworkObject"/>에서 빌드합니다.</summary>
    public List<LobbyPlayer> BuildLobbyPlayers()
    {
        var players = new List<LobbyPlayer>();

        if (Network == null || !Network.IsRunning)
        {
            return players;
        }

        IReadOnlyList<PlayerRef> activePlayers = Network.GetActivePlayers();
        if (activePlayers.Count == 0)
        {
            AddLocalLobbyFallback(players);
            return players;
        }

        if (!Network.TryGetAliveRunner(out NetworkRunner runner))
        {
            return players;
        }

        PlayerRef hostPlayer = ResolveHostPlayer(runner, activePlayers);
        string localId = Network.GetLocalPlayerId();

        foreach (PlayerRef playerRef in activePlayers)
        {
            string playerId = playerRef.PlayerId.ToString();
            bool isLocal = playerId == localId;
            bool isHost = playerRef == hostPlayer;
            string displayName = ResolveDisplayName(playerRef, playerId, isLocal);
            bool isReady = !isHost && TryGet(playerRef, out PlayerNetworkObject lobbyPlayer) && lobbyPlayer.IsReady;

            players.Add(new LobbyPlayer(playerId, displayName, isHost, isLocal, isReady));
        }

        return players;
    }

    void AddLocalLobbyFallback(List<LobbyPlayer> players)
    {
        if (Network == null || !Network.IsRunning || players == null)
        {
            return;
        }

        string localId = Network.GetLocalPlayerId();
        if (string.IsNullOrEmpty(localId))
        {
            return;
        }

        bool isHost = Network.Session.IsHost;
        bool isReady = !isHost && IsLocalReady;
        players.Add(new LobbyPlayer(localId, LocalDisplayName, isHost, true, isReady));
    }

    string ResolveDisplayName(PlayerRef playerRef, string playerId, bool isLocal)
    {
        if (TryGet(playerRef, out PlayerNetworkObject player))
        {
            string networkName = player.DisplayName.Value;
            if (!string.IsNullOrEmpty(networkName))
            {
                return networkName;
            }
        }

        return isLocal ? LocalDisplayName : $"Player {playerId}";
    }

    static PlayerRef ResolveHostPlayer(NetworkRunner runner, IReadOnlyList<PlayerRef> activePlayers)
    {
        if (runner.IsServer)
        {
            return runner.LocalPlayer;
        }

        PlayerRef host = default;
        int minId = int.MaxValue;

        foreach (PlayerRef playerRef in activePlayers)
        {
            if (playerRef.PlayerId < minId)
            {
                minId = playerRef.PlayerId;
                host = playerRef;
            }
        }

        return host;
    }
}
