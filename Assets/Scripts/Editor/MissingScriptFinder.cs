using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class MissingScriptFinder : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void FindMissingScripts()
    {
        string[] scenePaths = new string[] 
        { 
            "Assets/Scenes/Demo.unity", 
            "Assets/Scenes/Demo Room.unity" 
        };

        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject go in roots)
            {
                CheckGameObject(go, scenePath);
            }
            EditorSceneManager.CloseScene(scene, true);
        }
        
        Debug.Log("Finished searching for missing scripts.");
    }

    private static void CheckGameObject(GameObject go, string context)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.Log($"[Missing Script] Found on GameObject '{go.name}' in '{context}' (Index: {i})");
            }
        }

        foreach (Transform child in go.transform)
        {
            CheckGameObject(child.gameObject, context);
        }
    }
}
