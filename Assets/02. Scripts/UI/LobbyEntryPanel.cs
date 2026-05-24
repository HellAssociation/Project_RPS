using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyEntryPanel : PanelBase
{
    public override bool IsOpened => _panelGameObject.activeSelf;

    public override bool CanCloseWithESC => false;

    public override bool IsStackable => false;

    public override EUIType UIType => EUIType.LobbyEntry;

    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private TMP_Text statusText;

    LobbyManager _lobbyManager;
    bool _isBusy;

    protected override void Awake()
    {
        base.Awake();

        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
    }

    void Start()
    {
        _lobbyManager = App.SceneManager.Lobby;
    }

    void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
    }

    void OnCreateRoomClicked()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        SetInteractable(false);
        SetStatus("방 생성 중...");

        _lobbyManager.CreateSession(null, result =>
        {
            _isBusy = false;
            SetInteractable(true);

            if (!result.IsSuccess)
            {
                SetStatus(result.ErrorMessage);
            }
            else
            {
                SetStatus(string.Empty);
            }
        });
    }

    void OnJoinRoomClicked()
    {
        if (_isBusy)
        {
            return;
        }

        string code = joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("방 코드를 입력해 주세요.");
            return;
        }

        _isBusy = true;
        SetInteractable(false);
        SetStatus("방 입장 중...");

        _lobbyManager.JoinSession(code, result =>
        {
            _isBusy = false;
            SetInteractable(true);

            if (!result.IsSuccess)
            {
                SetStatus(result.ErrorMessage);
            }
            else
            {
                SetStatus(string.Empty);
            }
        });
    }

    void SetInteractable(bool interactable)
    {
        createRoomButton.interactable = interactable;
        joinRoomButton.interactable = interactable;
        joinCodeInput.interactable = interactable;
    }

    void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }
}
