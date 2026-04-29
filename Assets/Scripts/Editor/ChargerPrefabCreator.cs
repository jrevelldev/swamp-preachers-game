using UnityEngine;
using UnityEditor;
using SwampPreachers.Enemies;

namespace SwampPreachers.Editor
{
	[InitializeOnLoad]
	public class ChargerPrefabCreator
	{
		static ChargerPrefabCreator()
		{
			EditorApplication.delayCall += CreateChargerEnemy;
		}

		private static void CreateChargerEnemy()
		{
			string path = "Assets/Prefabs/Enemies/ChargerEnemy.prefab";
			if (System.IO.File.Exists(path)) return;

			// Ensure directory exists
			string dir = System.IO.Path.GetDirectoryName(path);
			if (!System.IO.Directory.Exists(dir))
			{
				System.IO.Directory.CreateDirectory(dir);
			}

			// Create main object
			GameObject go = new GameObject("ChargerEnemy");
			
			// Add Components
			Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
			rb.bodyType = RigidbodyType2D.Dynamic;
			rb.gravityScale = 3f;
			rb.freezeRotation = true;
			rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

			BoxCollider2D col = go.AddComponent<BoxCollider2D>();
			col.size = new Vector2(0.8f, 0.8f);

			EnemyStats stats = go.AddComponent<EnemyStats>();
			ChargerEnemy charger = go.AddComponent<ChargerEnemy>();

			// Create Visuals child
			GameObject visuals = new GameObject("Visuals");
			visuals.transform.SetParent(go.transform);
			visuals.transform.localPosition = Vector3.zero;

			SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
			// Load placeholder sprite
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Enemies/Charger/Body.png");
			if (sprite != null) sr.sprite = sprite;

			Animator anim = visuals.AddComponent<Animator>();
			RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/Enemies/ChargerEnemy.controller");
			if (controller != null) anim.runtimeAnimatorController = controller;

			bool success = false;
			PrefabUtility.SaveAsPrefabAsset(go, path, out success);

			if (success)
			{
				Debug.Log("ChargerPrefab created successfully at " + path);
			}
			else
			{
				Debug.LogError("Failed to create ChargerPrefab");
			}

			// Clean up scene object
			Object.DestroyImmediate(go);
		}
	}
}
