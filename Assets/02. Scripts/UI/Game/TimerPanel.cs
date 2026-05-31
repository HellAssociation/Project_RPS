using TMPro;
using SystemEnums;
using UnityEngine;
using System.Text;

public class TimerPanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;

    public override bool CanCloseWithESC => false;

    public override bool IsStackable => false;

    public override EUIType UIType => EUIType.Timer;
    #endregion

    [SerializeField] TextMeshProUGUI _timerTMP;

    readonly StringBuilder _stringBuilder = new(8);

    void OnEnable()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame != null)
            inGame.OnRoundTimerUpdated += UpdateTimer;
    }

    void OnDisable()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame != null)
            inGame.OnRoundTimerUpdated -= UpdateTimer;
    }

    void UpdateTimer(float time)
    {
        _stringBuilder.Clear();
        int seconds = Mathf.FloorToInt(time);
        int centiseconds = Mathf.FloorToInt((time - seconds) * 100f);
        _stringBuilder.Append(seconds);
        _stringBuilder.Append('.');
        if (centiseconds < 10) _stringBuilder.Append('0');
        _stringBuilder.Append(centiseconds);
        _timerTMP.SetText(_stringBuilder);
    }
}
