using UnityEngine;
using UnityEngine.EventSystems;

public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] IndicateType indicateType;

    IndicatePanel _indicatePanel;

    void Start()
    {
        if (!App.UI.InGame.TryGetPanel(out _indicatePanel))
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Tooltip] IndicatePanel not found.");
#endif
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_indicatePanel == null) return;

        // Uncomment when buff data table is linked:
        // string name        = BuffDataTable.GetName(indicateType);
        // string description = BuffDataTable.GetDescription(indicateType);
        _indicatePanel.Show(indicateType, string.Empty, string.Empty);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_indicatePanel == null) return;
        _indicatePanel.SetMousePosition(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_indicatePanel == null) return;
        _indicatePanel.Hide();
    }
}
