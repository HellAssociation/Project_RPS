using System;
using System.Text;
using UnityEngine;

public static class LobbyCodeUtility
{
    const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const int DefaultCodeLength = 6;

    public static string GenerateCode(int length = DefaultCodeLength)
    {
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            int index = UnityEngine.Random.Range(0, CodeChars.Length);
            builder.Append(CodeChars[index]);
        }

        return builder.ToString();
    }

    public static bool IsValidCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalized = NormalizeCode(code);
        if (normalized.Length < 4 || normalized.Length > 12)
        {
            return false;
        }

        for (int i = 0; i < normalized.Length; i++)
        {
            if (CodeChars.IndexOf(normalized[i]) < 0)
            {
                return false;
            }
        }

        return true;
    }

    public static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
