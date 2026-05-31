using System.Text;
using SystemEnums;
using TMPro;
using UnityEngine;

public class OutcomeCounter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _countTMP;

    int _winCount;

    readonly StringBuilder _sb = new(32);

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined += HandleOutcome;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined -= HandleOutcome;
    }

    void HandleOutcome(EOutcome outcome)
    {
        if (outcome == EOutcome.Win) _winCount++;
        Refresh();
    }

    void Refresh()
    {
        _sb.Clear();
        _sb.Append("<color=green>승리 : ");
        if (_winCount < 10) _sb.Append('0');
        _sb.Append(_winCount);
        _sb.Append("</color>");
        _countTMP.SetText(_sb);
    }
}
