using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SwampPreachers.Enemies;

namespace SwampPreachers.Editor
{
	[InitializeOnLoad]
	public class ChargerSceneSetup
	{
		static ChargerSceneSetup()
		{
			EditorApplication.delayCall += SetupScene;
		}

		private static void SetupScene()
		{
			// Check if we already ran setup (avoid infinite loop)
			if (GameObject.Find("Charger_Patrol_Setup") != null) return;

			// Ensure Prefab Exists (or Create it)
			string prefabPath = "Assets/Prefabs/Enemies/ChargerEnemy.prefab";
			GameObject prefab =  AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			
			if (prefab == null)
			{
				// Fallback creation logic
				string dir = "Assets/Prefabs/Enemies";
				if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

				GameObject go = new GameObject("ChargerEnemy");
				go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
				// Add other components to temp object just to save as prefab
				go.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
				go.AddComponent<EnemyStats>();
				go.AddComponent<ChargerEnemy>();
				
				GameObject vis = new GameObject("Visuals");
				vis.transform.SetParent(go.transform);
				vis.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Enemies/Charger/Body.png");
				vis.AddComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/Enemies/ChargerEnemy.controller");

				bool success;
				PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);
				GameObject.DestroyImmediate(go);
				
				if (success) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			}

			if (prefab == null)
			{
				Debug.LogError("Failed to load or create ChargerEnemy prefab.");
				return;
			}

			// Scene Setup
			GameObject setupRoot = new GameObject("Charger_Patrol_Setup");
			setupRoot.transform.position = new Vector3(0, 0, 0); // Adjust as needed based on level geometry?
			// Let's find a good spot. Maybe (0,0) is safe, or find the Player and put it nearby?
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
			// We need to use SerializedObject to modify the prefab instance properties reliably
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
