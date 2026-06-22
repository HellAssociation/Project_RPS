using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

// Dev-only: during a single-control wave, press the cheat key to set the hand to whatever beats
// the enemy's resolved position. No-op in builds.
public class DevAutoWinCheat : MonoBehaviour
{
    [SerializeField] Key cheatKey = Key.F9;

    void Awake()
    {
#if UNITY_EDITOR
        DontDestroyOnLoad(gameObject);
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current == null || !Keyboard.current[cheatKey].wasPressedThisFrame)
            return;

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null || !inGame.IsWaveActive || !ModeData.IsSingleControl)
            return;

        InGameUIManager ui = App.UI.InGame;
        if (ui == null || !ui.TryGetPanel(out NextIconPanel nextIcon))
            return;

        EHandPosition winning = RpsHandUtility.GetWinningHand(nextIcon.CurrentResolvedHandPosition);
        App.SystemManager.Input?.SetExtendedFingersMask((EFingerType)(int)winning);
#endif
    }
}
