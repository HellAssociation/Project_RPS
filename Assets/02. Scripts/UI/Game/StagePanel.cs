using TMPro;
using SystemEnums;
using UnityEngine;
using System.Text;
using UnityEngine.UI;

public class StagePanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;

    public override bool CanCloseWithESC => false;

    public override bool IsStackable => false;

    public override EUIType UIType => EUIType.Stage;
    #endregion

    [Header("Stage")]
    [SerializeField] StageSlot[] stageSlots;

    [Header("HP")]
    [SerializeField] Image hpImage;

    protected override void Awake()
    {
        base.Awake();
        CacheStageSlots();
    }

    private void CacheStageSlots()
    {
        if (stageSlots == null || stageSlots.Length == 0)
            stageSlots = GetComponentsInChildren<StageSlot>(true);

        int stageCount = stageSlots.Length;
        for(int index=0; index<stageCount; index++)
            stageSlots[index].Init(index);
    }
}
