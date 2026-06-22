using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageSlot : SlotHoverScale
{
    [SerializeField] RectTransform rect;
    [SerializeField] Image rockImage;
    [SerializeField] TextMeshProUGUI stageTMP; 

    private int _stageIndex = -1;
    private Button button;
    private const string STAGE_TEXT = "Stage ";
    public void Init(int stageIndex)
    {
        _stageIndex = stageIndex;

        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (stageTMP == null)
            stageTMP = GetComponentInChildren<TextMeshProUGUI>();

        if (button == null)
            button = GetComponent<Button>();

        if(button == null)
        {
            Debug.LogError("[Error] Can't find button!");
            return;
        }

        button.onClick.AddListener(OnClickButton);
        stageTMP.text = STAGE_TEXT + (stageIndex + 1);
    }

    private void OnClickButton()
    {
        if (!App.SystemManager.Network.IsServerHost) return;
        App.SystemManager.Network.ServerBroadcastStageSelected(_stageIndex);
    }
}
