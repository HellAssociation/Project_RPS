using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject hostBadge;
    [SerializeField] private Image readyBadge;

    public void Bind(LobbyPlayer player)
    {
        string suffix = player.IsLocal ? " (나)" : string.Empty;
        nameText.text = $"{player.DisplayName}{suffix}";

        if (hostBadge != null)
        {
            hostBadge.SetActive(player.IsHost);
        }

        SetReady(!player.IsHost && player.IsReady);
    }

    public void SetReady(bool isReady)
    {
        if (readyBadge != null)
        {
            readyBadge.gameObject.SetActive(isReady);
        }
    }
}
