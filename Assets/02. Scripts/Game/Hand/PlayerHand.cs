using Fusion;
using SystemEnums;

public class PlayerHand : Hand
{
    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players  => App.Game.Players;

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;

        if (ModeData.IsSingleControl)
        {
            if (inGame != null) inGame.OnLocalFingerMaskChanged += ApplyFingerMask;
            return;
        }

        if (Players != null) Players.OnPlayersChanged += RefreshFromNetwork;
        RefreshFromNetwork();
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;

        if (ModeData.IsSingleControl)
        {
            if (inGame != null) inGame.OnLocalFingerMaskChanged -= ApplyFingerMask;
            return;
        }

        if (Players != null) Players.OnPlayersChanged -= RefreshFromNetwork;
    }

    // Single-control (1v1): local mask drives all five fingers.
    void ApplyFingerMask(EFingerType mask)
    {
        if (fingerTable == null) return;
        foreach (var kvp in fingerTable)
            kvp.Value.SetOpen((mask & kvp.Key) != 0);
    }

    // Multi-control (1v5): every client draws the shared hand from each player's networked finger state.
    void RefreshFromNetwork()
    {
        if (fingerTable == null) return;
        CloseAll();

        NetworkManager net = Network;
        PlayerManager players = Players;
        if (net == null || players == null) return;

        foreach (PlayerRef player in net.GetActivePlayers())
        {
            if (players.TryGet(player, out PlayerNetworkObject po) && po.AssignedFinger != EFingerType.None)
                GetFinger(po.AssignedFinger)?.SetOpen(po.IsFingerExtended);
        }
    }
}
