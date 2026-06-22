using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

// Dev-only: registers the temporary Data_Deviation test JSON as an Addressable so the card
// vote can offer the test deviation cards. Remove once real card data is authored.
public static class TempDeviationAddressableSetup
{
    const string DataPath = "Assets/07. Data/Data_Deviation.json";
    const string Address = "Data_Deviation";

    [MenuItem("Tools/RPS/Setup Temp Deviation Data")]
    public static void Setup()
    {
        AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceUpdate);

        string guid = AssetDatabase.AssetPathToGUID(DataPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"[TempDeviationAddressableSetup] Asset not found at {DataPath}");
            return;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[TempDeviationAddressableSetup] No AddressableAssetSettings found.");
            return;
        }

        var duplicates = new List<string>();
        foreach (var g in settings.groups)
        {
            if (g == null) continue;
            foreach (var e in g.entries)
                if (e.address == Address && e.guid != guid)
                    duplicates.Add(e.guid);
        }
        foreach (string dupGuid in duplicates)
            settings.RemoveAssetEntry(dupGuid);

        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = Address;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TempDeviationAddressableSetup] Registered '{entry.address}' (guid {guid}) in group '{settings.DefaultGroup.Name}', removed {duplicates.Count} duplicate(s).");
    }
}
