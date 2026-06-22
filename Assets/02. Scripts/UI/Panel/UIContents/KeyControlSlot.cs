using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyControlSlot : MonoBehaviour
{
    [SerializeField] Image keyImage;
    [SerializeField] TextMeshProUGUI keyText;
    [SerializeField] Sprite defaultSprite;
    [SerializeField] Sprite pressedSprite;

    Key _targetKey = Key.None;
    bool _isPressed;

    public void Init()
    {
        if (keyImage == null)
            keyImage = GetComponentInChildren<Image>();

        if (keyText == null)
            keyText = GetComponentInChildren<TextMeshProUGUI>();

        SetPressed(false);
    }

    void Update()
    {
        if (_targetKey == Key.None)
            return;

        SetPressed(IsKeyHeld(_targetKey));
    }

    public void SetLabel(string label)
    {
        if (keyText != null)
            keyText.text = label ?? string.Empty;
    }

    public void SetKey(Key key)
    {
        _targetKey = key;
        if (!_isPressed)
            ApplyVisual(false);
    }

    public void SetPressed(bool pressed)
    {
        if (_isPressed == pressed)
            return;

        _isPressed = pressed;
        ApplyVisual(pressed);
    }

    void ApplyVisual(bool pressed)
    {
        if (keyImage == null)
            return;

        keyImage.sprite = pressed ? pressedSprite : defaultSprite;
    }

    public static string GetKeyLabel(Key key) => key switch
    {
        Key.Space  => "Space",
        Key.Digit1 => "1",
        Key.Digit2 => "2",
        Key.Digit3 => "3",
        Key.Digit4 => "4",
        Key.Digit5 => "5",
        _          => key.ToString(),
    };

    static bool IsKeyHeld(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (keyboard[key].isPressed)
            return true;

        Key numpadKey = ToNumpadKey(key);
        return numpadKey != key && keyboard[numpadKey].isPressed;
    }

    static Key ToNumpadKey(Key digitKey) => digitKey switch
    {
        Key.Digit1 => Key.Numpad1,
        Key.Digit2 => Key.Numpad2,
        Key.Digit3 => Key.Numpad3,
        Key.Digit4 => Key.Numpad4,
        Key.Digit5 => Key.Numpad5,
        Key.Space  => Key.Space,
        _          => digitKey,
    };
}
