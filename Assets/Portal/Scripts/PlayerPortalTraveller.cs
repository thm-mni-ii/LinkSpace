using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A specialized PortalTraveller that represents the player.
/// Handles teleportation of grabbed objects along with the player.
/// </summary>
public class PlayerPortalTraveller : PortalTraveller
{
    #region Inspector Variables
    [HideInInspector]
    [Tooltip("Transform representing the origin point for teleportation.")]
    public Transform teleportOrigin;

    #endregion

    #region Public Variables
    [HideInInspector]
    [Tooltip("List of portal travellers currently grabbed by the player.")]
    public List<GrabPortalTraveller> grabbedTravellers = new();

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

        foreach (GrabPortalTraveller traveller in grabbedTravellers)
        {
            traveller.markedForTeleport = !traveller.markedForTeleport;
        }
    }

    /// <summary>
    /// Called when the player enters a portal.
    /// Ensures grabbed travellers are prepared for teleportation.
    /// </summary>
    public override void EnterPortalThreshold(Portal portal)
    {
        base.EnterPortalThreshold(portal);

        if (portal.isEndpoint) return;

        foreach (GrabPortalTraveller traveller in grabbedTravellers)
        {
            if (traveller.fromPortal == null)
            {
                traveller.PrepareTeleportOnRelease(portal, portal.linkedPortal);
                portal.OnTravellerEnterPortal(traveller);
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a traveller to the grabbed list if not already present.
    /// </summary>
    public void AddGrabbedTraveller(GrabPortalTraveller grabTraveller)
    {
        if (!grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Add(grabTraveller);
        }
    }

    /// <summary>
    /// Removes a traveller from the grabbed list if present.
    /// </summary>
    public void RemoveGrabbedTraveller(GrabPortalTraveller grabTraveller)
    {
        if (grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Remove(grabTraveller);
        }
    }

    #endregion
}
