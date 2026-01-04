using UnityEngine;
using UnityEditor;

public class ForceImport
{
    public static void Import()
    {
        AssetDatabase.ImportAsset("Assets/InputSystem_Actions.inputactions", ImportAssetOptions.ForceUpdate);
        Debug.Log("Imported Assets/InputSystem_Actions.inputactions");
    }
}
