using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A specialized SpatialTraveller that represents the player.
/// Handles teleportation of grabbed objects along with the player.
/// </summary>
public class PlayerSpatialTraveller : SpatialTraveller
{
    #region Inspector Variables
    [HideInInspector]
    [Tooltip("Transform representing the origin point for teleportation.")]
    public Transform teleportOrigin;

    #endregion

    #region Public Variables
    [HideInInspector]
    [Tooltip("List of spatialView travellers currently grabbed by the player.")]
    public List<GrabSpatialTraveller> grabbedTravellers = new();

    #endregion

    #region Overrides

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ResolveTeleportOrigin();
    }

    /// <summary>
    /// Returns the position of the teleport origin instead of the transform.
    /// </summary>
    public override Vector3 GetPosition()
    {
        ResolveTeleportOrigin();
        return teleportOrigin != null ? teleportOrigin.position : base.GetPosition();
    }

    /// <summary>
    /// Returns the world matrix of the point that should cross the spatialView.
    /// </summary>
    public Matrix4x4 GetTeleportOriginMatrix()
    {
        ResolveTeleportOrigin();

        if (teleportOrigin == null)
            return transform.localToWorldMatrix;

        return Matrix4x4.TRS(teleportOrigin.position, teleportOrigin.rotation, Vector3.one);
    }

    /// <summary>
    /// Returns the rotation of the point that should cross the spatialView.
    /// </summary>
    public Quaternion GetTeleportOriginRotation()
    {
        ResolveTeleportOrigin();
        return teleportOrigin != null ? teleportOrigin.rotation : transform.rotation;
    }

    /// <summary>
    /// Teleports the player and toggles teleportation flags on grabbed travellers.
    /// </summary>
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        ResolveTeleportOrigin();

        CharacterController characterController = GetComponent<CharacterController>();
        bool characterControllerWasEnabled = characterController != null && characterController.enabled;

        if (characterControllerWasEnabled)
            characterController.enabled = false;

        if (teleportOrigin == null)
        {
            base.Teleport(pos, rot);
        }
        else
        {
            float yawDelta = Mathf.DeltaAngle(teleportOrigin.rotation.eulerAngles.y, rot.eulerAngles.y);
            transform.RotateAround(teleportOrigin.position, Vector3.up, yawDelta);
            transform.position += pos - teleportOrigin.position;
            Physics.SyncTransforms();
        }

        if (characterControllerWasEnabled)
            characterController.enabled = true;

        foreach (GrabSpatialTraveller traveller in grabbedTravellers)
        {
            traveller.markedForTeleport = !traveller.markedForTeleport;
        }
    }

    /// <summary>
    /// Called when the player enters a spatialView.
    /// Ensures grabbed travellers are prepared for teleportation.
    /// </summary>
    public override void EnterSpatialViewThreshold(SpatialView spatialView)
    {
        base.EnterSpatialViewThreshold(spatialView);

        if (spatialView.isEndpoint) return;

        foreach (GrabSpatialTraveller traveller in grabbedTravellers)
        {
            if (traveller.fromView == null)
            {
                traveller.PrepareTeleportOnRelease(spatialView, spatialView.linkedView);
                spatialView.OnTravellerEnterView(traveller);
            }
        }
    }

    #endregion

    #region Private Methods

    private void ResolveTeleportOrigin()
    {
        if (teleportOrigin == null && Camera.main != null)
            teleportOrigin = Camera.main.transform;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a traveller to the grabbed list if not already present.
    /// </summary>
    public void AddGrabbedTraveller(GrabSpatialTraveller grabTraveller)
    {
        if (!grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Add(grabTraveller);
        }
    }

    /// <summary>
    /// Removes a traveller from the grabbed list if present.
    /// </summary>
    public void RemoveGrabbedTraveller(GrabSpatialTraveller grabTraveller)
    {
        if (grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Remove(grabTraveller);
        }
    }

    #endregion
}
