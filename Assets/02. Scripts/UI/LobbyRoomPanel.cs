using System.Collections.Generic;
using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRoomPanel : PanelBase
{
    public override bool IsOpened => _panelGameObject.activeSelf;

    public override bool CanCloseWithESC => false;

    public override bool IsStackable => false;

    public override EUIType UIType => EUIType.LobbyRoom;

    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Transform playerListRoot;
    [SerializeField] private LobbyPlayerListItem playerListItemPrefab;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button readyOrStartButton;
    [SerializeField] private TMP_Text readyOrStartButtonLabel;
    [SerializeField] private TMP_Text statusText;

    readonly List<LobbyPlayerListItem> _spawnedItems = new();

    LobbyManager _lobbyManager;
    bool _isStartingGame;

    protected override void Awake()
    {
        base.Awake();

        leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        readyOrStartButton.onClick.AddListener(OnReadyOrStartClicked);
    }

    void Start()
    {
        _lobbyManager = App.SceneManager.Lobby;
        _lobbyManager.OnLobbyError += HandleLobbyError;
    }

    void OnDestroy()
    {
        if (_lobbyManager != null)
        {
            _lobbyManager.OnLobbyError -= HandleLobbyError;
        }

        leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
        readyOrStartButton.onClick.RemoveListener(OnReadyOrStartClicked);
    }

    void HandleLobbyError(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            statusText.text = message;
        }
    }

    void OnLeaveRoomClicked()
    {
        App.SceneManager.Lobby.LeaveSession();
    }

    void OnReadyOrStartClicked()
    {
        if (_isStartingGame)
        {
            return;
        }

        if (_lobbyManager.IsHost)
        {
            if (!_lobbyManager.CanStartGame())
            {
                SetStatus("아직 시작할 수 없습니다.");
                return;
            }

            _isStartingGame = true;
            RefreshActionButton();
            SetStatus("게임 시작 중...");

            _lobbyManager.StartGame(result =>
            {
                _isStartingGame = false;

                if (!result.IsSuccess)
                {
                    SetStatus(result.ErrorMessage);
                    RefreshActionButton();
                    return;
                }

                // 성공 시 인게임 씬으로 넘어가며 로비 오브젝트가 파괴됩니다. 로비 UI 갱신 생략.
            });
            return;
        }

        bool nextReady = !_lobbyManager.IsLocalReady;
        _lobbyManager.SetLocalReady(nextReady);
        RefreshActionButton();
    }

    public void RefreshContent()
    {
        if (!_lobbyManager.IsInLobby)
        {
            return;
        }

        roomCodeText.text = _lobbyManager.SessionCode;
        RefreshPlayerList(_lobbyManager.Players);
        RefreshActionButton();
    }

    void RefreshActionButton()
    {
        if (_lobbyManager.IsHost)
        {
            readyOrStartButtonLabel.text = "시작";
            readyOrStartButton.interactable = !_isStartingGame && _lobbyManager.CanStartGame();
            return;
        }

        bool isReady = _lobbyManager.IsLocalReady;
        readyOrStartButtonLabel.text = isReady ? "준비 취소" : "준비";
        readyOrStartButton.interactable = !_isStartingGame;
    }

    void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }

    void RefreshPlayerList(IReadOnlyList<LobbyPlayer> players)
    {
        ClearPlayerList();

        foreach (LobbyPlayer player in players)
        {
            LobbyPlayerListItem item = Instantiate(playerListItemPrefab, playerListRoot);
            item.gameObject.SetActive(true);
            item.Bind(player);
            _spawnedItems.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)playerListRoot);
    }

    void ClearPlayerList()
    {
        foreach (LobbyPlayerListItem item in _spawnedItems)
        {
            Destroy(item.gameObject);
        }

        _spawnedItems.Clear();
    }
}
