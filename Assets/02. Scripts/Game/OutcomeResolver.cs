using SystemEnums;

/// <summary>
/// Resolves an RPS outcome from player vs enemy hand. Special round rules will hook in here later.
/// </summary>
public static class OutcomeResolver
{
    public static EOutcome Resolve(EHandPosition player, EHandPosition enemy)
    {
        bool playerInvalid = player == EHandPosition.Invalid;
        bool enemyInvalid  = enemy == EHandPosition.Invalid || enemy == EHandPosition.Random;

        if (playerInvalid && enemyInvalid) return EOutcome.Draw;
        if (playerInvalid) return EOutcome.Lose;
        if (enemyInvalid)  return EOutcome.Win;

        if (player == enemy) return EOutcome.Draw;

        bool win = (player == EHandPosition.Rock     && enemy == EHandPosition.Scissors) ||
                   (player == EHandPosition.Paper    && enemy == EHandPosition.Rock)     ||
                   (player == EHandPosition.Scissors && enemy == EHandPosition.Paper);

        return win ? EOutcome.Win : EOutcome.Lose;
    }
}
