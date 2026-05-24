using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitlePanel : PanelBase
{
    public override bool IsOpened => _panelGameObject.activeSelf;

    public override bool CanCloseWithESC => false;

    public override bool IsStackable => false;

    public override EUIType UIType => EUIType.Title;

    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text statusText;

    TitleManager _titleManager;
    bool _isConnecting;

    protected override void Awake()
    {
        base.Awake();

        _titleManager = App.SceneManager.Title;

        if (!string.IsNullOrWhiteSpace(PlayerProfile.DisplayName))
        {
            nicknameInput.text = PlayerProfile.DisplayName;
        }

        connectButton.onClick.AddListener(OnConnectClicked);
    }

    void OnDestroy()
    {
        connectButton.onClick.RemoveListener(OnConnectClicked);
    }

    void OnConnectClicked()
    {
        if (_isConnecting)
        {
            return;
        }

        string nickname = nicknameInput.text;
        _isConnecting = true;
        SetInteractable(false);
        SetStatus("접속 중...");

        _titleManager.Connect(nickname, result =>
        {
            _isConnecting = false;
            SetInteractable(true);

            if (!result.IsSuccess)
            {
                SetStatus(result.ErrorMessage);
            }
        });
    }

    void SetInteractable(bool interactable)
    {
        connectButton.interactable = interactable;
        nicknameInput.interactable = interactable;
    }

    void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }
}
