using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Portal))]
public class PortalInspector : Editor
{
    private SerializedProperty linkedPortal;
    private SerializedProperty teleportationOffset;
    private SerializedProperty isEndpoint;

    private SerializedProperty useMeshAsScreen;
    private SerializedProperty screen;
    private SerializedProperty screenMaterial;

    private SerializedProperty walkable;
    private SerializedProperty teleportable;

    private SerializedProperty travelMode;

    private SerializedProperty onObjectTravel;

    private SerializedProperty nearClipOffset;
    private SerializedProperty nearClipLimit;

    // Foldout states
    private bool foldMain = true;
    private bool foldCreation = true;
    private bool foldNavigation = true;
    private bool foldObjectTravel = true;
    private bool foldEvents = true;
    private bool foldAdvanced = true;

    private void OnEnable()
    {
        // Main Settings
        linkedPortal = serializedObject.FindProperty("linkedPortal");
        teleportationOffset = serializedObject.FindProperty("teleportationOffset");
        isEndpoint = serializedObject.FindProperty("isEndpoint");

        // Portal Creation
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

        // Advanced
        nearClipOffset = serializedObject.FindProperty("nearClipOffset");
        nearClipLimit = serializedObject.FindProperty("nearClipLimit");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMainSettings();
        DrawPortalCreation();
        DrawNavigation();
        DrawObjectTravel();
        DrawEvents();
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
            EditorGUILayout.PropertyField(linkedPortal);
            EditorGUILayout.PropertyField(teleportationOffset);
            EditorGUILayout.PropertyField(isEndpoint);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
    }

    private void DrawPortalCreation()
    {
        foldCreation = EditorGUILayout.BeginFoldoutHeaderGroup(foldCreation, "Portal Creation");
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
