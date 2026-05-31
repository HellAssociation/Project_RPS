using SystemEnums;
using UnityEngine;

public class PlayerHand : Hand
{
    Finger activeFinger;

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnLocalAssignedFingerChanged += AssignActiveFinger;
        inGame.OnLocalFingerExtendedChanged += SetActiveFingerOpen;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnLocalAssignedFingerChanged -= AssignActiveFinger;
        inGame.OnLocalFingerExtendedChanged -= SetActiveFingerOpen;
    }

    void AssignActiveFinger(EFingerType _fingerType)
    {
        activeFinger = GetFinger(_fingerType);
        activeFinger?.SetOpen(false);
    }

    void SetActiveFingerOpen(bool _isOpen)
    {
        activeFinger?.SetOpen(_isOpen);
    }
}
