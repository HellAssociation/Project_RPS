using UnityEngine;
using UnityEngine.UI;

public class StageSlot : SlotHoverScale
{
    [SerializeField] Button _button;

    private int _stageIndex = -1;
    
    public void Init(int stageIndex)
    {
        _stageIndex = stageIndex;
        // to do

        if(_button == null)
            _button = GetComponent<Button>();

        if(_button == null)
        {
            Debug.LogError("[Error] Can't find button!");
            return;
        }

        _button.onClick.AddListener(OnClickButton);
    }

    private void OnClickButton()
    {
        if (!App.SystemManager.Network.IsServerHost) return;
        App.SystemManager.Network.ServerBroadcastStageSelected();
    }
}
