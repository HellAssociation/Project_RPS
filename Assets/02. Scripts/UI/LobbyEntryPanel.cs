using System;
using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyEntryPanel : PanelBase
{
    public override EUIType UIType => EUIType.LobbyEntry;

    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private TMP_Text statusText;

    LobbyManager _lobbyManager;
    bool _isBusy;

    protected override void Awake()
    {
        base.Awake();

        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

        if (quickJoinButton != null)
            quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
    }

    void Start()
    {
        _lobbyManager = App.SceneManager.Lobby;
    }

    void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);

        if (quickJoinButton != null)
            quickJoinButton.onClick.RemoveListener(OnQuickJoinClicked);
    }

    void OnCreateRoomClicked()
        => RunBusyAction("방 생성 중...", callback => _lobbyManager.CreateSession(null, callback));

    void OnJoinRoomClicked()
    {
        if (_isBusy) return;

        string code = joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("방 코드를 입력해 주세요.");
            return;
        }

        RunBusyAction("방 입장 중...", callback => _lobbyManager.JoinSession(code, callback));
    }

    void OnQuickJoinClicked()
        => RunBusyAction("빠른 참가 중...", callback => _lobbyManager.QuickJoinSession(callback));

    // Shared busy-guard envelope: lock UI, run the request, then restore UI with the result status.
    void RunBusyAction(string statusMessage, Action<Action<LobbyRequestResult>> operation)
    {
        if (_isBusy) return;

        _isBusy = true;
        SetInteractable(false);
        SetStatus(statusMessage);

        operation(result =>
        {
            _isBusy = false;
            SetInteractable(true);
            SetStatus(result.IsSuccess ? string.Empty : result.ErrorMessage);
        });
    }

    void SetInteractable(bool interactable)
    {
        createRoomButton.interactable = interactable;
        joinRoomButton.interactable = interactable;
        joinCodeInput.interactable = interactable;

        if (quickJoinButton != null)
            quickJoinButton.interactable = interactable;
    }

    void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }
}
