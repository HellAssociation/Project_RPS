using System;
using System.Collections.Generic;
using SystemEnums;

/// <summary>
/// 5손가락 합산 상태에서 RPS(가위·바위·보)를 판정합니다.
/// </summary>
public static class RpsHandUtility
{
    static readonly EFingerType[] AssignableFingers =
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

        var fingers = (EFingerType[])AssignableFingers.Clone();
        Shuffle(fingers, random);

        var assignments = new Dictionary<int, EFingerType>();
        int assignCount = Math.Min(playerIds.Count, fingers.Length);

        for (int i = 0; i < assignCount; i++)
        {
            assignments[playerIds[i]] = fingers[i];
        }

        return assignments;
    }

    static void Shuffle(EFingerType[] array, Random random)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
