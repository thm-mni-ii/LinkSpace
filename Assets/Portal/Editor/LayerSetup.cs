#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class LayerSetup
{
    private const string SetupKey = "PortalPackage_LayerSetupDone";
    private const string LayerName = "PortalScreen";

    static LayerSetup()
    {
        if (EditorPrefs.GetBool(SetupKey))
            return;
        int layerIndex = EnsureLayerExists(LayerName);
        DisableAllCollisionsForLayer(layerIndex);

        EditorPrefs.SetBool(SetupKey, true);
    }

    private static int EnsureLayerExists(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layersProp = tagManager.FindProperty("layers");

        // Check if the layer already exists
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                return i;
        }

        // Find an empty user layer slot (8–31)
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerSP.stringValue))
            {
                layerSP.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                // **Immediately fetch the actual layer index**
                int actualIndex = LayerMask.NameToLayer(layerName);
                if (actualIndex < 0)
                {
                    Debug.LogError("[Portal Package] Failed to get layer index after creation!");
                    return -1;
                }

                Debug.Log($"[Portal Package] Added layer: {layerName} at index {actualIndex}");
                return actualIndex;
            }
        }

        Debug.LogWarning($"[Portal Package] No empty layer slots available for '{layerName}'");
        return -1;
    }


    private static void DisableAllCollisionsForLayer(int layer)

    {
        if (layer < 0 || layer >= 32)
            return;

        SerializedObject physicsManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);

        SerializedProperty matrix =
            physicsManager.FindProperty("m_LayerCollisionMatrix");

        if (matrix == null || !matrix.isArray)
        {
            Debug.LogError("[Portal Package] Collision matrix not found.");
            return;
        }

        // Ensure the matrix has at least 32 entries
        if (matrix.arraySize < 32)
            matrix.arraySize = 32;

        for (int other = 0; other < 32; other++)
        {
            // Disable layer -> other
            int rowMask = matrix.GetArrayElementAtIndex(layer).intValue;
            rowMask &= ~(1 << other);
            matrix.GetArrayElementAtIndex(layer).intValue = rowMask;

            // Disable other -> layer (symmetry)
            int otherMask = matrix.GetArrayElementAtIndex(other).intValue;
            otherMask &= ~(1 << layer);
            matrix.GetArrayElementAtIndex(other).intValue = otherMask;
        }

        physicsManager.ApplyModifiedProperties();
        EditorUtility.SetDirty(physicsManager.targetObject);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Portal Package] Disabled all collisions for layer '{LayerName}'");
    }
}
#endif
