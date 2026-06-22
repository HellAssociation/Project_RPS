using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// Registers every AudioClip under 09. Audio/BGM and 09. Audio/SFX as an Addressable (address = file name).
public static class AudioAddressableSetup
{
    static readonly string[] Folders =
    {
        "Assets/09. Audio/BGM",
        "Assets/09. Audio/SFX",
    };

    [MenuItem("Tools/RPS/Setup Audio Addressables")]
    public static void Setup()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AudioAddressableSetup] No AddressableAssetSettings found.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", Folders);
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string address = Path.GetFileNameWithoutExtension(path);

            RemoveDuplicateAddress(settings, address, guid);

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = address;
            count++;

            Debug.Log($"[AudioAddressableSetup] {address} <- {path}");
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioAddressableSetup] Registered {count} audio clip(s) in group '{settings.DefaultGroup.Name}'.");
    }

    static void RemoveDuplicateAddress(AddressableAssetSettings settings, string address, string keepGuid)
    {
        var duplicates = new List<string>();
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            foreach (AddressableAssetEntry entry in group.entries)
                if (entry.address == address && entry.guid != keepGuid)
                    duplicates.Add(entry.guid);
        }

        foreach (string dupGuid in duplicates)
            settings.RemoveAssetEntry(dupGuid);
    }
}
