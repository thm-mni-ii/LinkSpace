using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPortalTraveller : PortalTraveller
{
    public Transform teleportOrigin;
    public List<GrabPortalTraveller> grabbedTravellers = new();
    public override Vector3 GetPosition()
    {
        return teleportOrigin.position;
    }
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        base.Teleport(pos, rot);
        foreach(GrabPortalTraveller traveller in grabbedTravellers)
        {
            traveller.markedForTeleport = !traveller.markedForTeleport;
        }
    }
    public override void EnterPortalThreshold(Portal portal)
    {
        base.EnterPortalThreshold(portal);
        foreach (GrabPortalTraveller traveller in grabbedTravellers)
        {
            if(traveller.fromPortal == null)
            {
                traveller.PrepareTeleportOnRelease(portal, portal.linkedPortal);
                portal.OnTravellerEnterPortal(traveller);
            }
        }

    }
    public void AddGrabbedTraveller(GrabPortalTraveller grabTraveller)
    {
        if (!grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Add(grabTraveller);
        }
    }

    public void RemoveGrabbedTraveller(GrabPortalTraveller grabTraveller)
    {
        if (grabbedTravellers.Contains(grabTraveller))
        {
            grabbedTravellers.Remove(grabTraveller);
        }
    }
}
