using UnityEngine;
using UnityEditor;

public class PortalManagerEditor : EditorWindow
{
    // --- Input Fields ---
    public Portal portalA;
    public Portal portalB;
    public Transform transformRef;
    public Vector3 pos;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 eulerRotation;

    [MenuItem("Tools/Portal Manager Debugger")]
    public static void ShowWindow()
    {
        GetWindow<PortalManagerEditor>("Portal Debugger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Portal Manager Debug Utility", EditorStyles.boldLabel);

        if (PortalManager.Instance == null)
        {
            EditorGUILayout.HelpBox("PortalManager.Instance is NULL in the scene.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        GUILayout.Label("Portal References", EditorStyles.boldLabel);
        portalA = (Portal)EditorGUILayout.ObjectField("Portal A", portalA, typeof(Portal), true);
        portalB = (Portal)EditorGUILayout.ObjectField("Portal B", portalB, typeof(Portal), true);
        transformRef = (Transform)EditorGUILayout.ObjectField("Transform", transformRef, typeof(Transform), true);

        EditorGUILayout.Space();
        GUILayout.Label("Position & Rotation Inputs", EditorStyles.boldLabel);

        pos = EditorGUILayout.Vector3Field("Position", pos);
        eulerRotation = EditorGUILayout.Vector3Field("Euler Rotation", eulerRotation);

        EditorGUILayout.Space();
        GUILayout.Label("Create Portal", EditorStyles.boldLabel);

        if (GUILayout.Button("CreatePortal(portalA, pos, rot)") && portalA != null)
        {
            PortalManager.Instance.CreatePortal(portalA, pos, Quaternion.Euler(eulerRotation));
        }

        if (GUILayout.Button("CreatePortal(portalA, Transform)") && portalA != null && transformRef != null)
        {
            PortalManager.Instance.CreatePortal(portalA, transformRef);
        }

        if (GUILayout.Button("CreatePortal(portalA, pos, euler)") && portalA != null)
        {
            PortalManager.Instance.CreatePortal(portalA, pos, eulerRotation);
        }

        if (GUILayout.Button("DestroyPortal (portalA)") && portalA != null)
        {
            PortalManager.Instance.DestroyPortal(portalA);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Modify Portal", EditorStyles.boldLabel);

        if (GUILayout.Button("Set Position (portalA)") && portalA != null)
        {
            PortalManager.Instance.SetPortalPosition(portalA, pos);
        }
        if (GUILayout.Button("Move Portal (portalA)") && portalA != null)
        {
            PortalManager.Instance.MovePortal(portalA, pos);
        }
        if (GUILayout.Button("Set Rotation (portalA)") && portalA != null)
        {
            PortalManager.Instance.SetPortalRotation(portalA, Quaternion.Euler(eulerRotation));
        }
        if (GUILayout.Button("Rotate Portal (portalA)") && portalA != null)
        {
            PortalManager.Instance.RotatePortal(portalA, Quaternion.Euler(eulerRotation));
        }
        if (GUILayout.Button("Set Transform (portalA)") && portalA != null && transformRef != null)
        {
            PortalManager.Instance.SetPortalTransform(portalA, transformRef);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Link / Unlink", EditorStyles.boldLabel);

        if (GUILayout.Button("Link Portals A <-> B") && portalA != null && portalB != null)
        {
            PortalManager.Instance.LinkPortals(portalA, portalB);
        }

        if (GUILayout.Button("Unlink Portal A") && portalA != null)
        {
            PortalManager.Instance.UnlinkPortals(portalA);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Destroy", EditorStyles.boldLabel);

        if (GUILayout.Button("Destroy Portal A") && portalA != null)
        {
            PortalManager.Instance.DestroyPortal(portalA);
        }
    }
}
