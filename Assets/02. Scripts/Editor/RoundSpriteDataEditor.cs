using SystemEnums;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(RoundSpriteData))]
public class RoundSpriteDataEditor : Editor
{
    const string HAND_ROOT = "Assets/03. Image/Hand";
    const int FINGER_COUNT = 5;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Auto-Map Sprites From Folders"))
            AutoMap((RoundSpriteData)target);
    }

    [MenuItem("Tools/Hand/Auto-Map All RoundSpriteData")]
    static void AutoMapAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RoundSpriteData"))
            AutoMap(AssetDatabase.LoadAssetAtPath<RoundSpriteData>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    static void AutoMap(RoundSpriteData _data)
    {
        var types = (EEnemyHandType[])System.Enum.GetValues(typeof(EEnemyHandType));
        var appearances = new HandAppearance[types.Length];

        for (int i = 0; i < types.Length; i++)
        {
            EEnemyHandType type = types[i];
            string folder = $"{HAND_ROOT}/{type}";

            Sprite handSprite = FindSprite($"{type}_Hand", folder);
            Sprite[] fingerSprites = new Sprite[HandAppearance.FINGER_SPRITE_COUNT];

            for (int f = 1; f <= FINGER_COUNT; f++)
            {
                int baseIndex = (f - 1) * 2;
                fingerSprites[baseIndex]     = FindSprite($"{type}_Finger_0{f}_Open", folder);
                fingerSprites[baseIndex + 1] = FindSprite($"{type}_Finger_0{f}_Close", folder);
            }

            var appearance = new HandAppearance();
            appearance.EditorSetup(type, handSprite, fingerSprites);
            appearances[i] = appearance;
        }

        _data.EditorSetAppearances(appearances);
        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();

        Debug.Log($"[RoundSpriteData] Auto-mapped {appearances.Length} hand appearances.");
    }

    static Sprite FindSprite(string _spriteName, string _folder)
    {
        if (!AssetDatabase.IsValidFolder(_folder))
        {
            Debug.LogWarning($"[RoundSpriteData] Folder not found: {_folder}");
            return null;
        }

        string[] guids = AssetDatabase.FindAssets($"t:Sprite {_spriteName}", new[] { _folder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            foreach (Object representation in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (representation is Sprite subSprite && subSprite.name == _spriteName)
                    return subSprite;
            }

            Sprite mainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (mainSprite != null && mainSprite.name == _spriteName)
                return mainSprite;
        }

        Debug.LogWarning($"[RoundSpriteData] Sprite not found: {_spriteName} in {_folder}");
        return null;
    }
}
