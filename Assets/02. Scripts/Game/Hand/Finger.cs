using SystemEnums;
using UnityEngine;

public class Finger : MonoBehaviour
{
    [SerializeField] EFingerType fingerType;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] FingerSpriteData fingerSpriteData;

    public EFingerType FingerType => fingerType;

    bool isOpen = false;
    public bool IsOpen => isOpen;

    public void Init()
    {
        if(spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        SetOpen(false);
    }

    public void SetOpen(bool _isOpen)
    {
        isOpen = _isOpen;
        ChangeSprite();
    }

    void ChangeSprite()
    {
        if(spriteRenderer != null)
            spriteRenderer.sprite = fingerSpriteData.GetFingerSprite(isOpen);
    }
}

[System.Serializable]
public struct FingerSpriteData
{
    [Header("Open / Close")]
    [SerializeField] Sprite[] fingerSprites;

    public Sprite GetFingerSprite(bool _isOpen) => fingerSprites[_isOpen ? 0 : 1];
}
