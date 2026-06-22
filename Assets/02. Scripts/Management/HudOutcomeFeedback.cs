using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.GameContent)]
public class HudOutcomeFeedback : MonoBehaviour
{
    InGameManager _inGame;

    void Start()
    {
        _inGame = App.SceneManager.InGame;
        if (_inGame == null) return;
        _inGame.OnOutcomeDetermined += HandleOutcome;
    }

    void OnDestroy()
    {
        if (_inGame == null) return;
        _inGame.OnOutcomeDetermined -= HandleOutcome;
    }

    void HandleOutcome(EOutcome outcome)
    {
        if (outcome == EOutcome.Draw)
            return;

        EEffect effect = outcome == EOutcome.Win ? EEffect.HudEnemyHit : EEffect.HudPlayerHit;
        App.SystemManager.Effect.PlayEffect(effect);
        App.SystemManager.Sound.PlayRandomSFX(SoundManager.AttackSfx);
    }
}
