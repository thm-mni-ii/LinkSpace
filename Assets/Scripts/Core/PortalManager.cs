using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    public bool useRaycastTravel = false;
    public PlayerPortalTraveller player;

    void Awake()
    {
        // Ensure only one instance exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Kill the duplicate
            return;
        }

        Instance = this;

        // Optional: Persist across scenes
        DontDestroyOnLoad(gameObject);


    }

    public void CreatePortal(Portal portal, Vector3 pos, Quaternion rot)
    {
        Portal portalInstance = Instantiate(portal);
        portalInstance.transform.position = pos;
        portalInstance.transform.rotation = rot;
    }
    public void CreatePortal(Portal portal, Transform transform)
    {
        CreatePortal(portal, transform.position, transform.rotation);
    }

    public void CreatePortal(Portal portal, Vector3 pos, Vector3 rot)
    {
        CreatePortal(portal, pos, Quaternion.Euler(rot));
    }

    public void DestroyPortal(Portal portal)
    {
        UnlinkPortals(portal);
        Destroy(portal.gameObject);
    }
    public void SetPortalPosition(Portal portal, Vector3 pos)
    {
        portal.transform.position = pos;
    }
    
    public void SetPortalRotation(Portal portal, Quaternion rot) 
    {
        portal.transform.rotation = rot;
    }
    public void SetPortalTransform(Portal portal, Transform t)
    {
        portal.transform.position = t.position;
        portal.transform.rotation = t.rotation;
    }
    public void MovePortal(Portal portal, Vector3 moveVector)
    {
        SetPortalPosition(portal, portal.transform.position + moveVector);
    }
    public void RotatePortal(Portal portal, Quaternion quaternion)
    {
       SetPortalRotation(portal, portal.transform.rotation * quaternion);
    }
    public void RotatePortal(Portal portal, Vector3 angles)
    {
        SetPortalRotation(portal, portal.transform.rotation * Quaternion.Euler(angles));
    }
    public void LinkPortals(Portal p1, Portal p2)
    {
        p1.Unlink();
        p1.Link(p2);
    }
    public void UnlinkPortals(Portal p1)
    {
        p1.Unlink();
    }
}

