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

    /// <summary>최대 count장을 무작위로 뽑고 풀에서 제거합니다. 남은 수가 적으면 그만큼만 반환.</summary>
    public int[] Draw(int count, System.Random rng)
    {
        int take = count < _remaining.Count ? count : _remaining.Count;
        if (take <= 0) return System.Array.Empty<int>();

        var result = new int[take];
        for (int i = 0; i < take; i++)
        {
            int pick = rng.Next(_remaining.Count);
            result[i] = _remaining[pick];
            _remaining.RemoveAt(pick);
        }
        return result;
    }
}
