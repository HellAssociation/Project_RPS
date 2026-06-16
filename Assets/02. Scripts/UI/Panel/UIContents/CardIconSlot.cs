using UnityEngine;
using UnityEngine.UI;

public class CardIconSlot : MonoBehaviour
{
    [SerializeField] Image _icon;
    [SerializeField] Image _background;

    public void SetIcon(Sprite icon, bool isBoon)
    {
        _icon.sprite = icon;
        _background.color = isBoon ? Color.green : Color.red;
    }
}
