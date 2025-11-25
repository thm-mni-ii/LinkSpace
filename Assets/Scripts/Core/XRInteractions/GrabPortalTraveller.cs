using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Device;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent (typeof(Rigidbody))]
public class GrabPortalTraveller : PortalTraveller
{
    [HideInInspector]
    public XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    // Portal info for teleportation on release
    [HideInInspector]
    public Portal fromPortal;
    [HideInInspector]
    public Portal toPortal;

    private bool grabRayHasHit;
    [HideInInspector]
    public bool markedForTeleport;
    private Vector3 lastPos;


    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectExited.AddListener(OnRelease);
        grabInteractable.selectEntered.AddListener(OnGrab);
        
        rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
    }
    private void Update()
    {
        if (PortalManager.Instance.useRaycastTravel)
        {
            if (IsGrabbed())
            {
                RaycastHit hit;
                grabRayHasHit = Raycast(grabInteractable.interactorsSelecting[0].transform.position, transform.position, out hit);
                if (grabRayHasHit)
                {
                    Portal hitPortal = hit.collider.GetComponentInParent<Portal>();
                    if (hitPortal != null && fromPortal == null)
                    {
                        PrepareTeleportOnRelease(hitPortal, hitPortal.linkedPortal);
                        hitPortal.OnTravellerEnterPortal(this);
                        return;
                    }

                }
                else
                {
                    if (fromPortal != null)
                    {
                        fromPortal.ReleaseTraveller(this);
                    }
                }


            }
        }
        else
        {
            if(fromPortal!= null && IsGrabbed() && TraveledThroughScreen())
            {
                markedForTeleport = !markedForTeleport;
            } 
            lastPos = transform.position;
        }
    }
    

    private bool TraveledThroughScreen()
    {
        if(!fromPortal.SameSideOfPortal(transform.position, lastPos) &&
            fromPortal.enterCollider.ClosestPoint(transform.position) == transform.position){
            return true;
        }
        return false;
    }

    public bool IsGrabbed()
    {
        return grabInteractable != null && grabInteractable.isSelected;
    }
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        rb.position = pos;
        rb.rotation = rot;

        Vector3 localVel = fromPortal.transform.InverseTransformDirection(rb.linearVelocity);
        rb.linearVelocity = toPortal.transform.TransformDirection(localVel);

        // Transform angular velocity
        Vector3 localAngVel = fromPortal.transform.InverseTransformDirection(rb.angularVelocity);
        rb.angularVelocity = toPortal.transform.TransformDirection(localAngVel);
       

    }
    public override void ExitPortalThreshold()
    {
        base.ExitPortalThreshold();
        if (!PortalManager.Instance.useRaycastTravel && IsGrabbed()) return;
        fromPortal = null;
        toPortal = null;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        PortalManager.Instance.player.RemoveGrabbedTraveller(this);
        if (fromPortal == null) return;
        if (PortalManager.Instance.useRaycastTravel)
        {
            

            if (grabRayHasHit)
            {

                Matrix4x4 m = toPortal.transform.localToWorldMatrix *
                  fromPortal.transform.worldToLocalMatrix *
                  transform.localToWorldMatrix;

                fromPortal.ReleaseTraveller(this);
                
                rb.position = m.GetColumn(3);
                rb.rotation = m.rotation;
                StartCoroutine(ApplyVelocity(m));
            }
            else
            {
                fromPortal.ReleaseTraveller(this);

            }
        }
        else
        {
            if (markedForTeleport)
            {
                Matrix4x4 m = toPortal.transform.localToWorldMatrix *
                  fromPortal.transform.worldToLocalMatrix *
                  transform.localToWorldMatrix;


                fromPortal.ReleaseTraveller(this);

                rb.position = m.GetColumn(3);
                rb.rotation = m.rotation;
                StartCoroutine(ApplyVelocity(m));

            }
            else
            {

                fromPortal.ReleaseTraveller(this);                
                

            }
            fromPortal = null;
            toPortal = null;
            markedForTeleport = false;
        }
    }
    private IEnumerator ApplyVelocity(Matrix4x4 m)
    {
        yield return null;
        rb.linearVelocity = m.MultiplyVector(rb.linearVelocity);
        rb.angularVelocity = m.MultiplyVector(rb.angularVelocity);
    }
    private void OnGrab(SelectEnterEventArgs arg0)
    {
        PortalManager.Instance.player.AddGrabbedTraveller(this);
        if (fromPortal == null) return;

        fromPortal.ReleaseTraveller(this);
    }


    public void PrepareTeleportOnRelease(Portal from, Portal to)
    {
        fromPortal = from;
        toPortal = to;
    }

    private bool Raycast(Vector3 startPos, Vector3 endPos, out RaycastHit hit)
    {
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        int portalLayer = LayerMask.NameToLayer("PortalScreen");
        if (portalLayer == -1)
        {
            Debug.LogWarning("Layer 'PortalScreen' does not exist!");
            hit = default; // make sure hit is valid
            return false;
        }
        int layerMask = 1 << portalLayer;

        bool didHit = Physics.Raycast(startPos, direction.normalized, out hit, distance, layerMask);

        // --- Visualization ---
        if (didHit)
        {
            Debug.DrawLine(startPos, hit.point, Color.green, 0.1f);
        }
        else
        {
            Debug.DrawLine(startPos, endPos, Color.red, 0.1f);
        }

        return didHit;
    }
    public override Vector3 GetPosition()
    {
        return rb.position;
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectExited.RemoveListener(OnRelease);
            grabInteractable.selectEntered.RemoveListener(OnGrab);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = !markedForTeleport ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 1); // Wireframe
        // Gizmos.DrawSphere(position, radius);  // Solid
    }
}
