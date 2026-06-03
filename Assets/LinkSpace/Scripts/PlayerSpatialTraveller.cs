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

    /// <summary>
    /// Returns the position of the teleport origin instead of the transform.
    /// </summary>
    public override Vector3 GetPosition()
    {
        return teleportOrigin.position;
    }

    /// <summary>
    /// Teleports the player and toggles teleportation flags on grabbed travellers.
    /// </summary>
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        base.Teleport(pos, rot);

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
