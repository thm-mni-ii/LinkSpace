using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpatialView))]
public class SpatialViewInspector : Editor
{
    private SerializedProperty linkedView;
    private SerializedProperty teleportationOffset;
    private SerializedProperty isEndpoint;

    private SerializedProperty mappingScaleMode;
    private SerializedProperty customMappingScale;
    private SerializedProperty scaleTravellerObjects;

    private SerializedProperty useMeshAsScreen;
    private SerializedProperty screen;
    private SerializedProperty screenMaterial;

    private SerializedProperty walkable;
    private SerializedProperty teleportable;

    private SerializedProperty travelMode;

    private SerializedProperty onObjectTravel;

    private SerializedProperty maxRecursionDepth;
    private SerializedProperty recursionTextureScale;

    private SerializedProperty nearClipOffset;
    private SerializedProperty nearClipLimit;

    // Foldout states
    private bool foldMain = true;
    private bool foldMappingScale = true;
    private bool foldCreation = true;
    private bool foldNavigation = true;
    private bool foldObjectTravel = true;
    private bool foldEvents = true;
    private bool foldRecursion = true;
    private bool foldAdvanced = true;

    private void OnEnable()
    {
        // Main Settings
        linkedView = serializedObject.FindProperty("linkedView");
        teleportationOffset = serializedObject.FindProperty("teleportationOffset");
        isEndpoint = serializedObject.FindProperty("isEndpoint");

        // Mapping Scale
        mappingScaleMode = serializedObject.FindProperty("mappingScaleMode");
        customMappingScale = serializedObject.FindProperty("customMappingScale");
        scaleTravellerObjects = serializedObject.FindProperty("scaleTravellerObjects");

        // Spatial View Creation
        useMeshAsScreen = serializedObject.FindProperty("useMeshAsScreen");
        screen = serializedObject.FindProperty("screen");
        screenMaterial = serializedObject.FindProperty("screenMaterial");

        // Navigation
        walkable = serializedObject.FindProperty("walkable");
        teleportable = serializedObject.FindProperty("teleportable");

        // Object Travel
        travelMode = serializedObject.FindProperty("travelMode");

        // Events
        onObjectTravel = serializedObject.FindProperty("onObjectTravel");

        // Recursion
        maxRecursionDepth = serializedObject.FindProperty("maxRecursionDepth");
        recursionTextureScale = serializedObject.FindProperty("recursionTextureScale");

        // Advanced
        nearClipOffset = serializedObject.FindProperty("nearClipOffset");
        nearClipLimit = serializedObject.FindProperty("nearClipLimit");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMainSettings();
        DrawMappingScale();
        DrawSpatialViewCreation();
        DrawNavigation();
        DrawObjectTravel();
        DrawEvents();
        DrawRecursion();
        DrawAdvancedSettings();

        serializedObject.ApplyModifiedProperties();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Foldout Sections
    // ─────────────────────────────────────────────────────────────────────────────

    private void DrawMainSettings()
    {
        foldMain = EditorGUILayout.BeginFoldoutHeaderGroup(foldMain, "Main Settings");
        if (foldMain)
        {
            EditorGUILayout.PropertyField(linkedView);
            EditorGUILayout.PropertyField(teleportationOffset);
            EditorGUILayout.PropertyField(isEndpoint);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawMappingScale()
    {
        foldMappingScale = EditorGUILayout.BeginFoldoutHeaderGroup(foldMappingScale, "Mapping Scale");
        if (foldMappingScale)
        {
            EditorGUILayout.PropertyField(mappingScaleMode);

            SpatialView.MappingScaleMode mode = (SpatialView.MappingScaleMode)mappingScaleMode.enumValueIndex;
            if (mode == SpatialView.MappingScaleMode.CustomScaleFactor)
            {
                EditorGUILayout.PropertyField(customMappingScale);
            }

            if (mode != SpatialView.MappingScaleMode.PreserveScale)
            {
                EditorGUILayout.PropertyField(scaleTravellerObjects);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawSpatialViewCreation()
    {
        foldCreation = EditorGUILayout.BeginFoldoutHeaderGroup(foldCreation, "Spatial View Creation");
        if (foldCreation)
        {
            EditorGUILayout.PropertyField(useMeshAsScreen);

            if (useMeshAsScreen.boolValue)
            {
                EditorGUILayout.PropertyField(screenMaterial);
            }
            else
            {
                EditorGUILayout.PropertyField(screen);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawNavigation()
    {
        foldNavigation = EditorGUILayout.BeginFoldoutHeaderGroup(foldNavigation, "Navigation");
        if (foldNavigation)
        {
            EditorGUILayout.PropertyField(walkable);
            EditorGUILayout.PropertyField(teleportable);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawObjectTravel()
    {
        foldObjectTravel = EditorGUILayout.BeginFoldoutHeaderGroup(foldObjectTravel, "Object Travel");
        if (foldObjectTravel)
        {
            EditorGUILayout.PropertyField(travelMode);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawEvents()
    {
        foldEvents = EditorGUILayout.BeginFoldoutHeaderGroup(foldEvents, "Unity Events");
        if (foldEvents)
        {
            EditorGUILayout.PropertyField(onObjectTravel);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawRecursion()
    {
        foldRecursion = EditorGUILayout.BeginFoldoutHeaderGroup(foldRecursion, "Recursion");
        if (foldRecursion)
        {
            EditorGUILayout.PropertyField(maxRecursionDepth);
            EditorGUILayout.PropertyField(recursionTextureScale);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawAdvancedSettings()
    {
        foldAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(foldAdvanced, "Advanced Settings");
        if (foldAdvanced)
        {
            EditorGUILayout.PropertyField(nearClipOffset);
            EditorGUILayout.PropertyField(nearClipLimit);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }
}
