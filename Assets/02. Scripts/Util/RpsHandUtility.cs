using System;
using System.Collections.Generic;
using SystemEnums;

/// <summary>
/// 5손가락 합산 상태에서 RPS(가위·바위·보)를 판정합니다.
/// </summary>
public static class RpsHandUtility
{
    // Canonical Thumb -> Pinky order (index 0..4). Single source of truth for finger ordering.
    public static readonly EFingerType[] AllFingers =
    {
        EFingerType.Thumb,
        EFingerType.Index,
        EFingerType.Middle,
        EFingerType.Ring,
        EFingerType.Pinky,
    };

    public static EHandPosition Judge(EFingerType mask)
    {
        return ((EFingerType)(mask & EFingerType.All)) switch
        {
            (EFingerType)EHandPosition.Rock => EHandPosition.Rock,
            (EFingerType)EHandPosition.Paper => EHandPosition.Paper,
            (EFingerType)EHandPosition.Scissors => EHandPosition.Scissors,
            _ => EHandPosition.Invalid,
        };
    }

    /// <summary>
    /// 플레이어 ID 목록에 5손가락을 무작위 1:1 배정합니다. (플레이어 수가 5 미만이면 남는 손가락은 미배정)
    /// </summary>
    public static Dictionary<int, EFingerType> CreateRandomAssignments(IReadOnlyList<int> playerIds, Random random = null)
    {
        random ??= new Random();

        var fingers = (EFingerType[])AllFingers.Clone();
        CollectionUtility.Shuffle(fingers, random);

        var assignments = new Dictionary<int, EFingerType>();
        int assignCount = Math.Min(playerIds.Count, fingers.Length);

        for (int i = 0; i < assignCount; i++)
        {
            assignments[playerIds[i]] = fingers[i];
        }

        return assignments;
    }
}
