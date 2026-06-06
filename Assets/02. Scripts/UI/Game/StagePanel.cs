using TMPro;
using UnityEngine;
using SystemEnums;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    [SerializeField] Sprite[] hpSprites;

    [Header("Cursor")]
    [SerializeField] RectTransform cursorRect;
    [SerializeField] TextMeshProUGUI hostNameTMP;

    Canvas _canvas;

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;

    protected override void Awake()
    {
        base.Awake();
        CacheStageSlots();
        _canvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        Players.OnPlayersChanged += RefreshHostName;
        RefreshHostName();
        RefreshHP();
    }

    void OnDestroy()
    {
        Players.OnPlayersChanged -= RefreshHostName;
    }

    public override void OpenPanel()
    {
        base.OpenPanel();
        RefreshHostName();
        RefreshHP();
    }

    void Update()
    {
        if (!_panelGameObject.activeSelf || _canvas == null) return;

        // Host: write mouse position to networked property
        if (Network.IsServerHost && Players.TryGetLocal(out PlayerNetworkObject localObj))
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            localObj.CursorScreenPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);
        }

        // All players: update cursorRect from host's networked position
        if (cursorRect != null && Players.TryGetHost(out PlayerNetworkObject hostObj))
        {
            Vector2 n = hostObj.CursorScreenPos;
            Vector2 screenPos = new(n.x * Screen.width, n.y * Screen.height);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, screenPos, cam, out Vector2 localPos))
            {
                cursorRect.position = _canvas.transform.TransformPoint(localPos);
            }
        }
    }

    private void RefreshHostName()
    {
        if (hostNameTMP == null) 
            return;
        
        string name = string.Empty;
        if (Players.TryGetHost(out PlayerNetworkObject hostObj))
            name = hostObj.DisplayName.Value;

        hostNameTMP.text = name;
    }

    private void RefreshHP()
    {
        if (hpImage == null || hpSprites == null || hpSprites.Length == 0) return;
        int lives = App.SceneManager.InGame != null ? App.SceneManager.InGame.Lives : InGameManager.MAX_LIVES;
        int index = Mathf.Clamp(InGameManager.MAX_LIVES - lives, 0, hpSprites.Length - 1);
        hpImage.sprite = hpSprites[index];
    }

    private void CacheStageSlots()
    {
        if (stageSlots == null || stageSlots.Length == 0)
            stageSlots = GetComponentsInChildren<StageSlot>(true);

        int stageCount = stageSlots.Length;
        for (int index = 0; index < stageCount; index++)
            stageSlots[index].Init(index);
    }
}
