#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LinkSpaceLayerSetup
{
    private const string LayerName = "SpatialViewScreen";

    static LinkSpaceLayerSetup()
    {
        EditorApplication.delayCall += RunSetup;
    }

    [MenuItem("Tools/LinkSpace/Run Layer Setup")]
    public static void RunSetup()
    {
        int layerIndex = EnsureLayerExists(LayerName);

        if (layerIndex >= 0)
        {
            DisableAllCollisionsForLayer(layerIndex);
        }
    }

    private static int EnsureLayerExists(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
            return existingLayer;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            SerializedProperty layer = layersProp.GetArrayElementAtIndex(i);

            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log($"[LinkSpace Package] Added layer '{layerName}' at index {i}");
                return i;
            }
        }

        Debug.LogError($"[LinkSpace Package] No empty layer slot available for '{layerName}'.");
        return -1;
    }

    private static void DisableAllCollisionsForLayer(int layer)
    {
        if (layer < 0 || layer >= 32)
            return;

        SerializedObject physicsManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);

        SerializedProperty matrix = physicsManager.FindProperty("m_LayerCollisionMatrix");

        if (matrix == null || !matrix.isArray)
        {
            Debug.LogError("[LinkSpace Package] Collision matrix not found.");
            return;
        }

        for (int other = 0; other < 32; other++)
        {
            Physics.IgnoreLayerCollision(layer, other, true);
        }

        DebugUtilityMarkDirty(physicsManager);

        Debug.Log($"[LinkSpace Package] Disabled all collisions for layer '{LayerName}'.");
    }

    private static void DebugUtilityMarkDirty(SerializedObject serializedObject)
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(serializedObject.targetObject);
        AssetDatabase.SaveAssets();
    }
}
#endif