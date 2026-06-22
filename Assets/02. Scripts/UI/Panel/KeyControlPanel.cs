using System.Collections.Generic;
using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyControlPanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.KeyControl;
    #endregion

    const int SingleControlCount = 5;

    static readonly EFingerType[] FingerOrder =
    {
        EFingerType.Thumb,
        EFingerType.Index,
        EFingerType.Middle,
        EFingerType.Ring,
        EFingerType.Pinky,
    };

    [SerializeField] KeyControlSlot slotRoot;
    [SerializeField] Transform slotContainer;

    readonly List<KeyControlSlot> _slots = new();

    InputManager Input => App.SystemManager.Input;

    protected override void Awake()
    {
        base.Awake();

        if (slotContainer == null && slotRoot != null)
            slotContainer = slotRoot.transform.parent;
    }

    void Start()
    {
        BuildSlots();
        RefreshBindings();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null)
            return;

        inGame.OnStageOpened += HandleStageOpened;
        inGame.OnWaveStarted += HandleWaveStarted;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null)
            return;

        inGame.OnStageOpened -= HandleStageOpened;
        inGame.OnWaveStarted -= HandleWaveStarted;
    }

    void HandleStageOpened(int _) => RefreshBindings();
    void HandleWaveStarted(float _) => RefreshBindings();

    void BuildSlots()
    {
        ClearClones();

        if (slotRoot == null)
            return;

        int count = ModeData.IsSingleControl ? SingleControlCount : 1;
        Transform parent = slotContainer != null ? slotContainer : slotRoot.transform.parent;

        for (int i = 0; i < count; i++)
        {
            KeyControlSlot slot = i == 0 ? slotRoot : Instantiate(slotRoot, parent);
            slot.transform.SetSiblingIndex(i);
            slot.gameObject.SetActive(true);
            slot.Init();
            _slots.Add(slot);
        }
    }

    void ClearClones()
    {
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            KeyControlSlot slot = _slots[i];
            if (slot != null && slot != slotRoot)
                Destroy(slot.gameObject);
        }

        _slots.Clear();
    }

    void RefreshBindings()
    {
        if (_slots.Count == 0)
            return;

        if (ModeData.IsSingleControl)
        {
            for (int i = 0; i < _slots.Count && i < FingerOrder.Length; i++)
            {
                EFingerType finger = FingerOrder[i];
                if (!Input.SingleControlKeyBindings.TryGetValue(finger, out Key key))
                    continue;

                _slots[i].SetKey(key);
                _slots[i].SetLabel(KeyControlSlot.GetKeyLabel(key));
            }

            return;
        }

        _slots[0].SetKey(Key.Space);
        _slots[0].SetLabel(KeyControlSlot.GetKeyLabel(Key.Space));
    }
}
