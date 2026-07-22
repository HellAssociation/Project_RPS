using System;
using Fusion;
using SystemEnums;
using UnityEngine;
using System.Collections.Generic;

public class PlayerHand : Hand
{
    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players  => App.Game.Players;
    void Start()
    {
        _inGame = App.SceneManager.InGame;

        if (ModeData.IsSingleControl)
        {
            if (_inGame != null) _inGame.OnLocalFingerMaskChanged += ApplyFingerMask;
            return;
        }

        if (Players != null) Players.OnPlayersChanged += RefreshFromNetwork;
        RefreshFromNetwork();
    }

    void OnDestroy()
    {
        if (_inGame != null && ModeData.IsSingleControl)
            _inGame.OnLocalFingerMaskChanged -= ApplyFingerMask;

        if (!ModeData.IsSingleControl && Players != null)
            Players.OnPlayersChanged -= RefreshFromNetwork;
    }

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
