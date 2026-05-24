using UnityEngine;

/// <summary>
/// 로컬 플레이어 닉네임·UserId 저장. 타이틀에서 설정하고 로비에서 사용합니다.
/// </summary>
public static class PlayerProfile
{
    const string KeyDisplayName = "project_paint.display_name";
    const string KeyUserId = "project_paint.user_id";

    public static string DisplayName { get; private set; } = "Player";
    public static string UserId { get; private set; }

    public static void Load()
    {
        DisplayName = PlayerPrefs.GetString(KeyDisplayName, "Player");
        UserId = PlayerPrefs.GetString(KeyUserId, string.Empty);
    }

    public static void SaveDisplayName(string displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
        PlayerPrefs.SetString(KeyDisplayName, DisplayName);
        PlayerPrefs.Save();
    }

    public static string GetOrCreateUserId()
    {
        if (!string.IsNullOrEmpty(UserId))
        {
            return UserId;
        }

        UserId = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(KeyUserId, UserId);
        PlayerPrefs.Save();
        return UserId;
    }

    public static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Player";
        }

        displayName = displayName.Trim();
        return displayName.Length > 16 ? displayName.Substring(0, 16) : displayName;
    }
}
