using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Singleton manager for creating, moving, rotating, linking, and destroying portals.
/// </summary>
public class PortalManager : MonoBehaviour
{
    #region Singleton

    public static PortalManager Instance { get; private set; }

    #endregion

    #region Inspector Variables

    [Tooltip("The player portal traveller.")]
    public PlayerPortalTraveller player;

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Initializes the singleton instance.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    #region Public Methods - Portal Creation & Destruction

    /// <summary>
    /// Instantiates a portal at the given position and rotation.
    /// </summary>
    public void CreatePortal(Portal portal, Vector3 pos, Quaternion rot)
    {
        Portal portalInstance = Instantiate(portal);
        portalInstance.transform.position = pos;
        portalInstance.transform.rotation = rot;
    }

    /// <summary>
    /// Instantiates a portal at the given transform's position and rotation.
    /// </summary>
    public void CreatePortal(Portal portal, Transform transform)
    {
        CreatePortal(portal, transform.position, transform.rotation);
    }

    /// <summary>
    /// Instantiates a portal at the given position and Euler rotation.
    /// </summary>
    public void CreatePortal(Portal portal, Vector3 pos, Vector3 rot)
    {
        CreatePortal(portal, pos, Quaternion.Euler(rot));
    }

    /// <summary>
    /// Destroys the specified portal and unlinks it.
    /// </summary>
    public void DestroyPortal(Portal portal)
    {
        UnlinkPortals(portal);
        Destroy(portal.gameObject);
    }

    #endregion

    #region Public Methods - Portal Transform

    /// <summary>
    /// Sets the portal's position.
    /// </summary>
    public void SetPortalPosition(Portal portal, Vector3 pos)
    {
        portal.transform.position = pos;
    }

    /// <summary>
    /// Sets the portal's rotation.
    /// </summary>
    public void SetPortalRotation(Portal portal, Quaternion rot)
    {
        portal.transform.rotation = rot;
    }

    /// <summary>
    /// Sets the portal's transform to match another transform.
    /// </summary>
    public void SetPortalTransform(Portal portal, Transform t)
    {
        portal.transform.position = t.position;
        portal.transform.rotation = t.rotation;
    }

    /// <summary>
    /// Moves the portal by the given offset vector.
    /// </summary>
    public void MovePortal(Portal portal, Vector3 moveVector)
    {
        SetPortalPosition(portal, portal.transform.position + moveVector);
    }

    /// <summary>
    /// Rotates the portal by the given quaternion.
    /// </summary>
    public void RotatePortal(Portal portal, Quaternion quaternion)
    {
        SetPortalRotation(portal, portal.transform.rotation * quaternion);
    }

    /// <summary>
    /// Rotates the portal by the given Euler angles.
    /// </summary>
    public void RotatePortal(Portal portal, Vector3 angles)
    {
        SetPortalRotation(portal, portal.transform.rotation * Quaternion.Euler(angles));
    }

    #endregion

    #region Public Methods - Portal Linking

    /// <summary>
    /// Links two portals together.
    /// </summary>
    public void LinkPortals(Portal p1, Portal p2)
    {
        p1.Unlink();
        p1.Link(p2);
    }

    /// <summary>
    /// Unlinks the specified portal.
    /// </summary>
    public void UnlinkPortals(Portal p1)
    {
        p1.Unlink();
    }

    #endregion
}
