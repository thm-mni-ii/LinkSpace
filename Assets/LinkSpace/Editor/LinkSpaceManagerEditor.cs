using UnityEngine;
using UnityEditor;

public class LinkSpaceManagerEditor : EditorWindow
{
    // --- Input Fields ---
    public SpatialView viewA;
    public SpatialView viewB;
    public Transform transformRef;
    public Vector3 pos;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 eulerRotation;

    [MenuItem("Tools/LinkSpace Manager Debugger")]
    public static void ShowWindow()
    {
        GetWindow<LinkSpaceManagerEditor>("LinkSpace Debugger");
    }

    private void OnGUI()
    {
        GUILayout.Label("LinkSpace Manager Debug Utility", EditorStyles.boldLabel);

        if (LinkSpaceManager.Instance == null)
        {
            EditorGUILayout.HelpBox("LinkSpaceManager.Instance is NULL in the scene.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        GUILayout.Label("Spatial View References", EditorStyles.boldLabel);
        viewA = (SpatialView)EditorGUILayout.ObjectField("Spatial View A", viewA, typeof(SpatialView), true);
        viewB = (SpatialView)EditorGUILayout.ObjectField("Spatial View B", viewB, typeof(SpatialView), true);
        transformRef = (Transform)EditorGUILayout.ObjectField("Transform", transformRef, typeof(Transform), true);

        EditorGUILayout.Space();
        GUILayout.Label("Position & Rotation Inputs", EditorStyles.boldLabel);

        pos = EditorGUILayout.Vector3Field("Position", pos);
        eulerRotation = EditorGUILayout.Vector3Field("Euler Rotation", eulerRotation);

        EditorGUILayout.Space();
        GUILayout.Label("Create SpatialView", EditorStyles.boldLabel);

        if (GUILayout.Button("CreateSpatialView(viewA, pos, rot)") && viewA != null)
        {
            LinkSpaceManager.Instance.CreateSpatialView(viewA, pos, Quaternion.Euler(eulerRotation));
        }

        if (GUILayout.Button("CreateSpatialView(viewA, Transform)") && viewA != null && transformRef != null)
        {
            LinkSpaceManager.Instance.CreateSpatialView(viewA, transformRef);
        }

        if (GUILayout.Button("CreateSpatialView(viewA, pos, euler)") && viewA != null)
        {
            LinkSpaceManager.Instance.CreateSpatialView(viewA, pos, eulerRotation);
        }

        if (GUILayout.Button("DestroySpatialView (viewA)") && viewA != null)
        {
            LinkSpaceManager.Instance.DestroySpatialView(viewA);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Modify SpatialView", EditorStyles.boldLabel);

        if (GUILayout.Button("Set Position (viewA)") && viewA != null)
        {
            LinkSpaceManager.Instance.SetSpatialViewPosition(viewA, pos);
        }
        if (GUILayout.Button("Move SpatialView (viewA)") && viewA != null)
        {
            LinkSpaceManager.Instance.MoveSpatialView(viewA, pos);
        }
        if (GUILayout.Button("Set Rotation (viewA)") && viewA != null)
        {
            LinkSpaceManager.Instance.SetSpatialViewRotation(viewA, Quaternion.Euler(eulerRotation));
        }
        if (GUILayout.Button("Rotate SpatialView (viewA)") && viewA != null)
        {
            LinkSpaceManager.Instance.RotateSpatialView(viewA, Quaternion.Euler(eulerRotation));
        }
        if (GUILayout.Button("Set Transform (viewA)") && viewA != null && transformRef != null)
        {
            LinkSpaceManager.Instance.SetSpatialViewTransform(viewA, transformRef);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Link / Unlink", EditorStyles.boldLabel);

        if (GUILayout.Button("Link Spatial Views A <-> B") && viewA != null && viewB != null)
        {
            LinkSpaceManager.Instance.LinkSpatialViews(viewA, viewB);
        }

        if (GUILayout.Button("Unlink Spatial View A") && viewA != null)
        {
            LinkSpaceManager.Instance.UnlinkSpatialViews(viewA);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Destroy", EditorStyles.boldLabel);

        if (GUILayout.Button("Destroy Spatial View A") && viewA != null)
        {
            LinkSpaceManager.Instance.DestroySpatialView(viewA);
        }
    }
}
