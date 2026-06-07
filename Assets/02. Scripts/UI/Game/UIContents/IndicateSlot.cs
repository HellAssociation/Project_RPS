using TMPro;
using UnityEngine;

public class IndicateSlot : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameTMP;
    [SerializeField] TextMeshProUGUI descriptionTMP;
    [SerializeField] RectTransform   rect;
    [SerializeField] IndicateType    indicateType;

    public IndicateType IndicateType => indicateType;

    public IndicateType Init()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (indicateType == IndicateType.OnlyDescription)
        {
            if (descriptionTMP == null)
                Debug.LogError("[IndicateSlot] descriptionTMP is missing!");
        }
        else if (nameTMP == null || descriptionTMP == null)
            Debug.LogError("[IndicateSlot] nameTMP or descriptionTMP is missing!");

        gameObject.SetActive(false);
        return indicateType;
    }

    public void Show(string name, string description)
    {
        gameObject.SetActive(true);

        // Uncomment when buff data table is linked:
        // if (indicateType != IndicateType.OnlyDescription && nameTMP != null)
        //     nameTMP.text = name;
        // if (descriptionTMP != null)
        //     descriptionTMP.text = description;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        if (rect != null)
            rect.anchoredPosition = anchoredPosition;
    }
}
