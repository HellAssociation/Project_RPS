using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

[System.Serializable]
public class HandAppearance
{
    public const int FINGER_SPRITE_COUNT = 10;

    [SerializeField] EEnemyHandType type;
    [SerializeField] Sprite handSprite;
    [Tooltip("Thumb -> Pinky, Open / Close (10 entries)")]
    [SerializeField] Sprite[] fingerSprites = new Sprite[FINGER_SPRITE_COUNT];

    public EEnemyHandType Type => type;
    public Sprite HandSprite => handSprite;

    public Sprite GetFingerSprite(EFingerType _fingerType, bool _isOpen)
    {
        int fingerIndex = System.Array.IndexOf(RpsHandUtility.AllFingers, _fingerType);
        if (fingerIndex < 0 || fingerSprites == null) return null;

        int spriteIndex = fingerIndex * 2 + (_isOpen ? 0 : 1);
        if (spriteIndex < 0 || spriteIndex >= fingerSprites.Length) return null;

        return fingerSprites[spriteIndex];
    }

#if UNITY_EDITOR
    public void EditorSetup(EEnemyHandType _type, Sprite _handSprite, Sprite[] _fingerSprites)
    {
        type = _type;
        handSprite = _handSprite;
        fingerSprites = _fingerSprites;
    }
#endif
}

[System.Serializable]
public class RoundSprite
{
    [Tooltip("Duplicates act as weights")]
    [SerializeField] List<EEnemyHandType> pool = new();

    public IReadOnlyList<EEnemyHandType> Pool => pool;
}

[CreateAssetMenu(fileName = "RoundSpriteData", menuName = "RPS/Round Sprite Data")]
public class RoundSpriteData : ScriptableObject
{
    [SerializeField] HandAppearance[] appearances;
    [Tooltip("Index = round - 1")]
    [SerializeField] RoundSprite[] roundSprites;

    Dictionary<EEnemyHandType, HandAppearance> _table;

    void BuildTable()
    {
        _table = new Dictionary<EEnemyHandType, HandAppearance>();
        if (appearances == null) return;

        foreach (HandAppearance appearance in appearances)
        {
            if (appearance == null) continue;

            if (!_table.TryAdd(appearance.Type, appearance))
                Debug.LogError($"[Error] {appearance.Type} duplicate hand appearance!");
        }
    }

    public bool TryGetAppearance(EEnemyHandType _type, out HandAppearance _appearance)
    {
        if (_table == null) BuildTable();
        return _table.TryGetValue(_type, out _appearance);
    }

    public IReadOnlyList<EEnemyHandType> GetPool(int _round)
    {
        int index = _round - 1;
        if (roundSprites == null || index < 0 || index >= roundSprites.Length)
            return null;

        return roundSprites[index].Pool;
    }

#if UNITY_EDITOR
    public void EditorSetAppearances(HandAppearance[] _appearances)
    {
        appearances = _appearances;
        _table = null;
    }
#endif
}
