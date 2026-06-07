using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class HandSpriteDataGeneratorWindow : EditorWindow
{
    const string DEFAULT_SPRITE_FOLDER = "Assets/04. Image/Hand/Enemy";
    const string DEFAULT_OUTPUT_FOLDER = "Assets/05. Data/Hand/Enemy/Data";

    string spriteFolder = DEFAULT_SPRITE_FOLDER;
    string outputFolder = DEFAULT_OUTPUT_FOLDER;

    [MenuItem("Tools/Hand/Hand Sprite Data Generator")]
    public static void ShowWindow()
    {
        GetWindow<HandSpriteDataGeneratorWindow>("Hand Sprite Data Generator");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Hand Sprite Data Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        spriteFolder = EditorGUILayout.TextField("Sprite Search Folder", spriteFolder);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();
        HandleDragAndDrop();

        EditorGUILayout.HelpBox(
            "Drag and drop HandSpriteData json files here.\n" +
            "Each json must contain an \"id\" (matching EEnemyHandType), a \"handSpriteName\" and 10 \"spriteNames\" (Thumb -> Pinky, Open / Close).\n" +
            "Sprites are looked up by name inside the Sprite Search Folder and linked automatically.",
            MessageType.Info);
    }

    void HandleDragAndDrop()
    {
        Event currentEvent = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0f, 60f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop JSON Files Here");

        if (!dropArea.Contains(currentEvent.mousePosition)) return;

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                currentEvent.Use();
                break;

            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is TextAsset jsonAsset)
                        GenerateFromJson(jsonAsset);
                }
                currentEvent.Use();
                break;
        }
    }

    void GenerateFromJson(TextAsset _jsonAsset)
    {
        HandSpriteJson json = JsonUtility.FromJson<HandSpriteJson>(_jsonAsset.text);

        if (json == null || string.IsNullOrEmpty(json.id) || json.spriteNames == null)
        {
            Debug.LogError($"[HandSpriteDataGenerator] Invalid json: {_jsonAsset.name}");
            return;
        }

        if (!System.Enum.TryParse(json.id, out EEnemyHandType handId))
        {
            Debug.LogError($"[HandSpriteDataGenerator] '{_jsonAsset.name}' has an unknown id '{json.id}'. It must match a value of EEnemyHandType.");
            return;
        }

        if (json.spriteNames.Length != HandSpriteData.FINGER_SPRITE_COUNT)
        {
            Debug.LogError($"[HandSpriteDataGenerator] '{_jsonAsset.name}' must contain {HandSpriteData.FINGER_SPRITE_COUNT} sprite names, found {json.spriteNames.Length}.");
            return;
        }

        EnsureFolderExists(outputFolder);

        string assetPath = $"{outputFolder}/{json.id}.asset";
        HandSpriteData data = AssetDatabase.LoadAssetAtPath<HandSpriteData>(assetPath);
        bool isNew = data == null;

        if (isNew)
            data = CreateInstance<HandSpriteData>();

        Sprite handSprite = FindSprite(json.handSpriteName);

        Sprite[] sprites = new Sprite[HandSpriteData.FINGER_SPRITE_COUNT];
        for (int i = 0; i < json.spriteNames.Length; i++)
            sprites[i] = FindSprite(json.spriteNames[i]);

        data.Setup(handId, handSprite, sprites);

        if (isNew)
            AssetDatabase.CreateAsset(data, assetPath);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Debug.Log($"[HandSpriteDataGenerator] Generated '{assetPath}' from '{_jsonAsset.name}'.");
    }

    Sprite FindSprite(string _spriteName)
    {
        if (string.IsNullOrEmpty(_spriteName)) return null;

        string[] guids = AssetDatabase.FindAssets($"t:Sprite {_spriteName}", new[] { spriteFolder });

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

        Debug.LogWarning($"[HandSpriteDataGenerator] Sprite not found: {_spriteName}");
        return null;
    }

    static void EnsureFolderExists(string _folderPath)
    {
        if (AssetDatabase.IsValidFolder(_folderPath)) return;

        string[] parts = _folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    [System.Serializable]
    class HandSpriteJson
    {
        public string id;
        public string handSpriteName;
        public string[] spriteNames;
    }
}
