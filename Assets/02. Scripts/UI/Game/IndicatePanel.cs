using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

public class IndicatePanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.Indicate;
    #endregion

    [SerializeField] IndicateSlot[] slots;

    Dictionary<IndicateType, IndicateSlot> slotTable;
    IndicateSlot                           _activeSlot;
    Canvas                                 _canvas;

    protected override void Awake()
    {
        base.Awake();
        _canvas = GetComponentInParent<Canvas>();
        InitSlots();
    }

    void InitSlots()
    {
        if (slots == null || slots.Length == 0)
            slots = GetComponentsInChildren<IndicateSlot>(true);

        slotTable = new(slots.Length);
        foreach (IndicateSlot slot in slots)
        {
            IndicateType type = slot.Init();
            if (!slotTable.ContainsKey(type))
                slotTable[type] = slot;
        }
    }

    public void Show(IndicateType type, string name, string description)
    {
        if (_activeSlot != null)
            _activeSlot.Hide();

        if (!slotTable.TryGetValue(type, out _activeSlot))
            return;

        _activeSlot.Show(name, description);
    }

    public void Hide()
    {
        if (_activeSlot == null) return;
        _activeSlot.Hide();
        _activeSlot = null;
    }

    public void SetMousePosition(Vector2 screenPos)
    {
        if (_activeSlot == null || _canvas == null) return;

        if (TryGetCanvasLocalPoint(_canvas, screenPos, out Vector2 localPos))
            _activeSlot.SetPosition(localPos);
    }
}

public enum IndicateType
{
    None            = 0,
    OnlyDescription,
    Description,
}
