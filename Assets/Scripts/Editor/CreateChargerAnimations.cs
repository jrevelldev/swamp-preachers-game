using UnityEngine;
using UnityEditor;

namespace SwampPreachers.Editor
{
    [InitializeOnLoad]
    public class CreateChargerAnimations
    {
        static CreateChargerAnimations()
        {
            EditorApplication.delayCall += CreateAnims;
        }

        private static void CreateAnims()
        {
            string dir = "Assets/Art/Animations/Enemies/Charger";
            if (System.IO.Directory.Exists(dir)) return; // Only run if dir doesn't exist to avoid overwriting constantly?
            
            // Actually, if dir exists but files don't? Let's check for specific file
            if (System.IO.File.Exists(dir + "/Idle.anim")) return;

            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            CreateClip(dir + "/Idle.anim");
            CreateClip(dir + "/Windup.anim");
            CreateClip(dir + "/Charge.anim");
            CreateClip(dir + "/Stunned.anim");
            CreateClip(dir + "/Hit.anim");
            CreateClip(dir + "/Die.anim");

            AssetDatabase.Refresh();
            Debug.Log("Created Charger Animations in " + dir);
        }

        private static void CreateClip(string path)
        {
            AnimationClip clip = new AnimationClip();
            Keyframe[] keysX = new Keyframe[2];
            keysX[0] = new Keyframe(0f, 1f); // Scale 1 at start
            keysX[1] = new Keyframe(0.1f, 1f); // Scale 1 at end

            AnimationCurve curve = new AnimationCurve(keysX);
            clip.SetCurve("", typeof(Transform), "m_LocalScale.x", curve);
            clip.SetCurve("", typeof(Transform), "m_LocalScale.y", curve);
            clip.SetCurve("", typeof(Transform), "m_LocalScale.z", curve);

            AssetDatabase.CreateAsset(clip, path);
        }
    }
}
