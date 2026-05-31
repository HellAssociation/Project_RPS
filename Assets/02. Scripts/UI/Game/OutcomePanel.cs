using SystemEnums;
using TMPro;
using UnityEngine;

public class OutcomePanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;
    public override bool CanCloseWithESC => false;
    public override bool IsStackable => false;
    public override EUIType UIType => EUIType.OutCome;
    #endregion

    [SerializeField] TextMeshProUGUI outcomeTMP;

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined += ShowOutcome;
        inGame.OnRoundStarted += HideOutcome;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined -= ShowOutcome;
        inGame.OnRoundStarted -= HideOutcome;
    }

    void ShowOutcome(EOutcome outcome)
    {
        outcomeTMP.text = outcome switch
        {
            EOutcome.Win  => "승리",
            EOutcome.Lose => "패배",
            EOutcome.Draw => "무승부",
            _             => string.Empty,
        };

        OpenPanel();
    }

    void HideOutcome(float _)
    {
        ClosePanel();
    }
}
