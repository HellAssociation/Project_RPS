using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class SceneFastLauncher : EditorWindow
{
    private List<SceneAsset> sceneAssets = new List<SceneAsset>();
    private Vector2 scrollPosition;

    [MenuItem("Window/Scene Fast Launcher")]
    public static void ShowWindow()
    {
        GetWindow<SceneFastLauncher>("Scene Launcher");
    }

    private void OnEnable()
    {
        LoadScenes();
    }

    private void OnGUI()
    {
        HandleDragAndDrop();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < sceneAssets.Count; i++)
        {
            if (sceneAssets[i] == null)
            {
                sceneAssets.RemoveAt(i);
                i--;
                continue;
            }

            EditorGUILayout.BeginHorizontal("box");

            if (GUILayout.Button(sceneAssets[i].name, GUILayout.Height(25)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    string path = AssetDatabase.GetAssetPath(sceneAssets[i]);
                    EditorSceneManager.OpenScene(path);
                }
            }

            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(25)))
            {
                sceneAssets.RemoveAt(i);
                SaveScenes();
                Event.current.Use();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (sceneAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("Drag and drop Scene assets here.", MessageType.Info);
        }
    }

    private void HandleDragAndDrop()
    {
        Event currentEvent = Event.current;
        Rect dropArea = new Rect(0, 0, position.width, position.height);

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
                    if (draggedObject is SceneAsset sceneAsset && !sceneAssets.Contains(sceneAsset))
                    {
                        sceneAssets.Add(sceneAsset);
                    }
                }
                SaveScenes();
                currentEvent.Use();
                break;
        }
    }

    private void SaveScenes()
    {
        List<string> paths = new List<string>();
        foreach (SceneAsset asset in sceneAssets)
        {
            if (asset != null)
            {
                paths.Add(AssetDatabase.GetAssetPath(asset));
            }
        }
        string joinedPaths = string.Join(";", paths);
        EditorPrefs.SetString("SceneFastLauncher_Paths", joinedPaths);
    }

    private void LoadScenes()
    {
        sceneAssets.Clear();
        string savedData = EditorPrefs.GetString("SceneFastLauncher_Paths", string.Empty);
        if (string.IsNullOrEmpty(savedData)) return;

        string[] paths = savedData.Split(';');
        foreach (string path in paths)
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (asset != null)
            {
                sceneAssets.Add(asset);
            }
        }
    }
}