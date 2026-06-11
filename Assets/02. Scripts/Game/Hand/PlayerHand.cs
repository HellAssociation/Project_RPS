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
        inGame.OnLocalFingerMaskChanged     += ApplyFingerMask;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnLocalAssignedFingerChanged -= AssignActiveFinger;
        inGame.OnLocalFingerExtendedChanged -= SetActiveFingerOpen;
        inGame.OnLocalFingerMaskChanged     -= ApplyFingerMask;
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

    void ApplyFingerMask(EFingerType mask)
    {
        if (fingerTable == null) return;
        foreach (var kvp in fingerTable)
            kvp.Value.SetOpen((mask & kvp.Key) != 0);
    }
}
