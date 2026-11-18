using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SwampPreachers.Enemies;

namespace SwampPreachers.Editor
{
	public class ChargerSceneSetup
	{
		[MenuItem("Tools/Enemies/Setup Charger in Current Scene")]
		public static void SetupScene()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				Debug.LogWarning("Cannot setup scene while in Play mode.");
				return;
			}

			// Check if we already ran setup (avoid duplicates)
			if (GameObject.Find("Charger_Patrol_Setup") != null)
			{
				Debug.Log("Charger_Patrol_Setup already exists in scene.");
				return;
			}

			// Ensure Prefab Exists
			string prefabPath = "Assets/Prefabs/Enemies/ChargerEnemy.prefab";
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			
			if (prefab == null)
			{
				Debug.LogError("Failed to load ChargerEnemy prefab at " + prefabPath);
				return;
			}

			// Scene Setup
			GameObject setupRoot = new GameObject("Charger_Patrol_Setup");
			setupRoot.transform.position = Vector3.zero;

			GameObject player = GameObject.FindWithTag("Player");
			if (player != null) 
			{
				setupRoot.transform.position = player.transform.position + new Vector3(5, 0, 0);
			}

			GameObject p1 = new GameObject("PatrolPoint_A");
			p1.transform.SetParent(setupRoot.transform);
			p1.transform.localPosition = new Vector3(-3, 0, 0);

			GameObject p2 = new GameObject("PatrolPoint_B");
			p2.transform.SetParent(setupRoot.transform);
			p2.transform.localPosition = new Vector3(3, 0, 0);

			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			instance.transform.SetParent(setupRoot.transform);
			instance.transform.localPosition = Vector3.zero;

			// Assign Patrol Points
			ChargerEnemy enemyScript = instance.GetComponent<ChargerEnemy>();
			if (enemyScript != null)
			{
				SerializedObject so = new SerializedObject(enemyScript);
				SerializedProperty pointsProp = so.FindProperty("patrolPoints");
				if (pointsProp != null)
				{
					pointsProp.arraySize = 2;
					pointsProp.GetArrayElementAtIndex(0).objectReferenceValue = p1.transform;
					pointsProp.GetArrayElementAtIndex(1).objectReferenceValue = p2.transform;
					so.ApplyModifiedProperties();
				}
			}

			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			Debug.Log("Charger Scene Setup Complete!");
		}
	}
}
