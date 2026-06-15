using SystemEnums;
using UnityEngine;

public class WavePanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.Wave;
    #endregion

    [SerializeField] WaveSlot[] slots;

    int _markedCount;

    protected override void Awake()
    {
        base.Awake();
        InitSlots();
    }

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined += HandleOutcome;
        inGame.OnStageOpened += HandleStageOpened;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined -= HandleOutcome;
        inGame.OnStageOpened -= HandleStageOpened;
    }

    void HandleStageOpened(int stageIndex)
    {
        _markedCount = 0;
        foreach (WaveSlot slot in slots)
            slot.Clear();
    }

    void InitSlots()
    {
        int count = slots.Length;
        for (int i = 0; i < count; i++)
        {
            WaveType type = (i == count - 1) ? WaveType.Boss : WaveType.Normal;
            slots[i].Init(type);
        }
    }

    void HandleOutcome(EOutcome outcome)
    {
        if (outcome != EOutcome.Win)    return;
        if (_markedCount >= slots.Length) return;

        slots[_markedCount].MarkDefeated();
        _markedCount++;
    }
}
