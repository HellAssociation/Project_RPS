using System.Collections.Generic;

/// <summary>
/// Draw-without-replacement pool of card indices for one run. The host draws the offered
/// cards each round; every offered card is removed so it never reappears (decision: 제시된 3장 모두 제거).
/// </summary>
public class CardDeck
{
    readonly List<int> _remaining = new();

    public int RemainingCount => _remaining.Count;

    public void Reset(IReadOnlyList<int> all)
    {
        _remaining.Clear();
        if (all == null) return;
        for (int i = 0; i < all.Count; i++) _remaining.Add(all[i]);
    }

    /// <summary>최대 count장을 무작위로 뽑고 풀에서 제거합니다. predicate가 있으면 조건을 만족하는 카드만 대상입니다.</summary>
    public int[] Draw(int count, System.Random rng, System.Func<int, bool> predicate = null)
    {
        int eligibleCount = 0;
        for (int i = 0; i < _remaining.Count; i++)
        {
            if (predicate == null || predicate(_remaining[i]))
                eligibleCount++;
        }

        int take = count < eligibleCount ? count : eligibleCount;
        if (take <= 0) return System.Array.Empty<int>();

        var result = new int[take];
        for (int r = 0; r < take; r++)
        {
            int pick = rng.Next(eligibleCount);
            int seen = 0;
            for (int i = 0; i < _remaining.Count; i++)
            {
                if (predicate != null && !predicate(_remaining[i]))
                    continue;

                if (seen == pick)
                {
                    result[r] = _remaining[i];
                    _remaining.RemoveAt(i);
                    eligibleCount--;
                    break;
                }

                seen++;
            }
        }

        return result;
    }
}
