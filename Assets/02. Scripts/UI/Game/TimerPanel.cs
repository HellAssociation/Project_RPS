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
    [SerializeField] TextMeshProUGUI _roundTMP;

    readonly StringBuilder _sb = new(16);

    void OnEnable()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnWaveTimerUpdated  += UpdateTimer;
        inGame.OnCurrentRoundChanged += UpdateRound;
        UpdateRound(inGame.CurrentRound);
    }

    void OnDisable()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnWaveTimerUpdated  -= UpdateTimer;
        inGame.OnCurrentRoundChanged -= UpdateRound;
    }

    void UpdateTimer(float time)
    {
        _sb.Clear();
        int seconds      = Mathf.FloorToInt(time);
        int centiseconds = Mathf.FloorToInt((time - seconds) * 100f);
        _sb.Append(seconds);
        _sb.Append('.');
        if (centiseconds < 10) _sb.Append('0');
        _sb.Append(centiseconds);
        _timerTMP.SetText(_sb);
    }

    void UpdateRound(int round)
    {
        if (_roundTMP == null) return;
        _sb.Clear();
        _sb.Append("Round ");
        _sb.Append(round);
        _roundTMP.SetText(_sb);
    }
}
