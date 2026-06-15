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
