using SystemEnums;
using UnityEngine;

[CreateAssetMenu(fileName = "HandSpriteData", menuName = "RPS/Hand Sprite Data")]
public class HandSpriteData : ScriptableObject
{
    public const int FINGER_SPRITE_COUNT = 10;

    [SerializeField] EEnemyHandType handId;
    [SerializeField] Sprite handSprite;
    [Tooltip("Thumb -> Pinky, Open / Close (10 entries)")]
    [SerializeField] Sprite[] fingerSprites = new Sprite[FINGER_SPRITE_COUNT];

    public EEnemyHandType HandId => handId;
    public Sprite HandSprite => handSprite;
    public Sprite[] FingerSprites => fingerSprites;

    public Sprite GetFingerSprite(EFingerType _fingerType, bool _isOpen)
    {
        int fingerIndex = FingerIndex(_fingerType);
        if (fingerIndex < 0 || fingerSprites == null) return null;

        int spriteIndex = fingerIndex * 2 + (_isOpen ? 0 : 1);
        if (spriteIndex < 0 || spriteIndex >= fingerSprites.Length) return null;

        return fingerSprites[spriteIndex];
    }

#if UNITY_EDITOR
    public void Setup(EEnemyHandType _handId, Sprite _handSprite, Sprite[] _fingerSprites)
    {
        handId = _handId;
        handSprite = _handSprite;
        fingerSprites = _fingerSprites;
    }
#endif

    static int FingerIndex(EFingerType _fingerType) => _fingerType switch
    {
        EFingerType.Thumb => 0,
        EFingerType.Index => 1,
        EFingerType.Middle => 2,
        EFingerType.Ring => 3,
        EFingerType.Pinky => 4,
        _ => -1,
    };
}
