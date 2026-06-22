using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class NextIconSlot : MonoBehaviour
{
    [SerializeField] RectTransform _rect;
    [SerializeField] Image _iconImage;

    public RectTransform Rect => _rect;
    public EHandPosition HandPosition { get; private set; }
    public EHandPosition ResolvedHandPosition { get; private set; }

    public void Init()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        if (_iconImage == null)
            _iconImage = GetComponentInChildren<Image>();
    }

    public void SetSlot(EHandPosition handPosition, EHandPosition resolvedHandPosition, Sprite sprite)
    {
        HandPosition = handPosition;
        ResolvedHandPosition = resolvedHandPosition;
        if (_iconImage != null)
            _iconImage.sprite = sprite;
    }
}
