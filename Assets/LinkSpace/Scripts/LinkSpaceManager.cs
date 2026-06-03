using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Singleton manager for creating, moving, rotating, linking, and destroying spatialViews.
/// </summary>
public class LinkSpaceManager : MonoBehaviour
{
    #region Singleton

    public static LinkSpaceManager Instance { get; private set; }

    #endregion

    #region Inspector Variables

    [Tooltip("The player spatialView traveller.")]
    public PlayerSpatialTraveller player;

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

    #region Public Methods - Spatial View Creation & Destruction

    /// <summary>
    /// Instantiates a spatialView at the given position and rotation.
    /// </summary>
    public void CreateSpatialView(SpatialView spatialView, Vector3 pos, Quaternion rot)
    {
        SpatialView viewInstance = Instantiate(spatialView);
        viewInstance.transform.position = pos;
        viewInstance.transform.rotation = rot;
    }

    /// <summary>
    /// Instantiates a spatialView at the given transform's position and rotation.
    /// </summary>
    public void CreateSpatialView(SpatialView spatialView, Transform transform)
    {
        CreateSpatialView(spatialView, transform.position, transform.rotation);
    }

    /// <summary>
    /// Instantiates a spatialView at the given position and Euler rotation.
    /// </summary>
    public void CreateSpatialView(SpatialView spatialView, Vector3 pos, Vector3 rot)
    {
        CreateSpatialView(spatialView, pos, Quaternion.Euler(rot));
    }

    /// <summary>
    /// Destroys the specified spatialView and unlinks it.
    /// </summary>
    public void DestroySpatialView(SpatialView spatialView)
    {
        UnlinkSpatialViews(spatialView);
        Destroy(spatialView.gameObject);
    }

    #endregion

    #region Public Methods - SpatialView Transform

    /// <summary>
    /// Sets the spatialView's position.
    /// </summary>
    public void SetSpatialViewPosition(SpatialView spatialView, Vector3 pos)
    {
        spatialView.transform.position = pos;
    }

    /// <summary>
    /// Sets the spatialView's rotation.
    /// </summary>
    public void SetSpatialViewRotation(SpatialView spatialView, Quaternion rot)
    {
        spatialView.transform.rotation = rot;
    }

    /// <summary>
    /// Sets the spatialView's transform to match another transform.
    /// </summary>
    public void SetSpatialViewTransform(SpatialView spatialView, Transform t)
    {
        spatialView.transform.position = t.position;
        spatialView.transform.rotation = t.rotation;
    }

    /// <summary>
    /// Moves the spatialView by the given offset vector.
    /// </summary>
    public void MoveSpatialView(SpatialView spatialView, Vector3 moveVector)
    {
        SetSpatialViewPosition(spatialView, spatialView.transform.position + moveVector);
    }

    /// <summary>
    /// Rotates the spatialView by the given quaternion.
    /// </summary>
    public void RotateSpatialView(SpatialView spatialView, Quaternion quaternion)
    {
        SetSpatialViewRotation(spatialView, spatialView.transform.rotation * quaternion);
    }

    /// <summary>
    /// Rotates the spatialView by the given Euler angles.
    /// </summary>
    public void RotateSpatialView(SpatialView spatialView, Vector3 angles)
    {
        SetSpatialViewRotation(spatialView, spatialView.transform.rotation * Quaternion.Euler(angles));
    }

    #endregion

    #region Public Methods - SpatialView Linking

    /// <summary>
    /// Links two spatialViews together.
    /// </summary>
    public void LinkSpatialViews(SpatialView p1, SpatialView p2)
    {
        p1.Unlink();
        p1.Link(p2);
    }

    /// <summary>
    /// Unlinks the specified spatialView.
    /// </summary>
    public void UnlinkSpatialViews(SpatialView p1)
    {
        p1.Unlink();
    }

    #endregion
}
