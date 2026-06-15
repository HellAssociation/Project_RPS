using UnityEngine;
using System.Collections;

public class FrameChecker : CommonManagerBase
{
    const float updateInterval = 0.2f;

    GUIStyle style;
    Rect rect;
    float msec;
    float fps;
    float deltaTime;
    int frameCount;
    string text;

    protected override void Awake()
    {
        base.Awake();

        text = string.Empty;
        frameCount = -1;
        deltaTime = -1f;

        UpdateStyle();
    }

    private void OnEnable()
    {
        frameCount = 1;
        deltaTime = updateInterval;
        UpdateText();
    }

    void Update()
    {
        if (frameCount < 0)
        {
            return;
        }

        deltaTime += Time.unscaledDeltaTime;
        ++frameCount;
    }

    private void UpdateStyle()
    {
        rect = new Rect(0, 0, Screen.width, Screen.height);
        style = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            fontSize = 32,
#else
            fontSize = 24,
#endif
        };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
    }

    private void UpdateText()
    {
        msec = deltaTime * 1000f / frameCount;
        fps = frameCount / deltaTime;  
        text = fps.ToString("F1") + " (" + msec.ToString("F1") + "ms)";
        //text += "\nPing: " + PhotonNetwork.GetPing();

        frameCount = 0;
        deltaTime = 0f;
    }

    void OnGUI()
    {
        if (frameCount < 0)
        {
            return;
        }

        if (deltaTime < updateInterval)
        {
            GUI.Label(rect, text, style);
            return;
        }

        UpdateText();
        GUI.Label(rect, text, style);
    }
}
