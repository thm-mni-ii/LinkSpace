using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Portal;

/// <summary>
/// A portal traveller that can be grabbed by the player and interact with portals.
/// Handles raycast and traversal-based teleportation while grabbed.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class GrabPortalTraveller : PortalTraveller
{
    #region Inspector Variables

    [HideInInspector]
    [Tooltip("XRGrabInteractable component used for grabbing interactions.")]
    public XRGrabInteractable grabInteractable;

    #endregion

    #region Public Variables

    [HideInInspector]
    [Tooltip("Portal from which the traveller originates.")]
    public Portal fromPortal;

    [HideInInspector]
    [Tooltip("Portal to which the traveller will teleport.")]
    public Portal toPortal;

    [HideInInspector]
    [Tooltip("Marks if the traveller is scheduled for teleportation.")]
    public bool markedForTeleport;

    #endregion

    #region Private Variables

    private bool grabRayHasHit;
    private Vector3 lastPos;

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Initializes references and sets up event listeners.
    /// </summary>
    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectExited.AddListener(OnRelease);
        grabInteractable.selectEntered.AddListener(OnGrab);

        rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
    }

    /// <summary>
    /// Updates raycast and traversal checks while grabbed.
    /// </summary>
    private void Update()
    {
        if (IsGrabbed())
        {
            HandleRaycastTravel();
            HandleTraversionTravel();
        }
    }

    /// <summary>
    /// Cleans up event listeners when destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectExited.RemoveListener(OnRelease);
            grabInteractable.selectEntered.RemoveListener(OnGrab);
        }
    }

    /// <summary>
    /// Draws gizmos to visualize teleportation state.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = !markedForTeleport ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns the traveller's current position via Rigidbody.
    /// </summary>
    public override Vector3 GetPosition()
    {
        return rb.position;
    }

    /// <summary>
    /// Teleports the traveller and optionally transforms velocities through the portal.
    /// </summary>
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        rb.position = pos;
        rb.rotation = rot;

        if (fromPortal != null && toPortal != null)
        {
            Vector3 localVel = fromPortal.transform.InverseTransformDirection(rb.linearVelocity);
            rb.linearVelocity = toPortal.transform.TransformDirection(localVel);

            Vector3 localAngVel = fromPortal.transform.InverseTransformDirection(rb.angularVelocity);
            rb.angularVelocity = toPortal.transform.TransformDirection(localAngVel);
        }
        
    }

    /// <summary>
    /// Handles behaviour when exiting a portal threshold.
    /// </summary>
    public override void ExitPortalThreshold()
    {
        base.ExitPortalThreshold();
        //if (fromPortal.travelMode == ObjectTravelMode.Traversation && IsGrabbed()) return;
        fromPortal = null;
        toPortal = null;
    }

    /// <summary>
    /// Sets the portals for teleportation when the object is released.
    /// </summary>
    public void PrepareTeleportOnRelease(Portal from, Portal to)
    {
        fromPortal = from;
        toPortal = to;
    }

    /// <summary>
    /// Returns true if this traveller is currently being grabbed.
    /// </summary>
    public bool IsGrabbed()
    {
        return grabInteractable != null && grabInteractable.isSelected;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles teleportation via raycast mode when the object is grabbed.
    /// </summary>
    private void HandleRaycastTravel()
    {
        RaycastHit hit;
        grabRayHasHit = Raycast(grabInteractable.interactorsSelecting[0].transform.position, transform.position, out hit);

        if (grabRayHasHit)
        {
            Portal hitPortal = hit.collider.GetComponentInParent<Portal>();
            if (hitPortal != null && hitPortal.travelMode == ObjectTravelMode.Raycast && fromPortal == null)
            {
                PrepareTeleportOnRelease(hitPortal, hitPortal.linkedPortal);
                hitPortal.OnTravellerEnterPortal(this);
                return;
            }
        }
        else
        {
            if (fromPortal != null && fromPortal.travelMode == ObjectTravelMode.Raycast)
            {
                fromPortal.ReleaseTraveller(this);
            }
        }
    }

    /// <summary>
    /// Handles teleportation via traversal mode when the object is grabbed.
    /// </summary>
    private void HandleTraversionTravel()
    {
        if (fromPortal != null && fromPortal.travelMode == ObjectTravelMode.Traversation && IsGrabbed() && TraveledThroughScreen())
        {
            markedForTeleport = !markedForTeleport;
        }

        lastPos = transform.position;
    }

    /// <summary>
    /// Checks if the traveller has moved through a portal screen.
    /// </summary>
    private bool TraveledThroughScreen()
    {
        return !fromPortal.SameSideOfPortal(transform.position, lastPos) &&
               fromPortal.enterCollider.ClosestPoint(transform.position) == transform.position;
    }

    /// <summary>
    /// Performs a physics raycast against portal screens.
    /// </summary>
    private bool Raycast(Vector3 startPos, Vector3 endPos, out RaycastHit hit)
    {
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        int portalLayer = LayerMask.NameToLayer("PortalScreen");
        if (portalLayer == -1)
        {
            Debug.LogWarning("Layer 'PortalScreen' does not exist!");
            hit = default;
            return false;
        }

        int layerMask = 1 << portalLayer;
        bool didHit = Physics.Raycast(startPos, direction.normalized, out hit, distance, layerMask);

        Debug.DrawLine(startPos, didHit ? hit.point : endPos, didHit ? Color.green : Color.red, 0.1f);

        return didHit;
    }

    /// <summary>
    /// Called when the object is grabbed.
    /// </summary>
    private void OnGrab(SelectEnterEventArgs args)
    {
        PortalManager.Instance.player.AddGrabbedTraveller(this);

        if (fromPortal == null) return;
        fromPortal.ReleaseTraveller(this);
    }

    /// <summary>
    /// Called when the object is released.
    /// Handles teleportation based on mode and portal state.
    /// </summary>
    private void OnRelease(SelectExitEventArgs args)
    {
        PortalManager.Instance.player.RemoveGrabbedTraveller(this);

        if (fromPortal == null || fromPortal.isEndpoint) return;

        if (fromPortal.travelMode == ObjectTravelMode.Raycast)
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

    /// <summary>
    /// Applies transformed velocities after teleportation.
    /// </summary>
    private IEnumerator ApplyVelocity(Matrix4x4 m)
    {
        yield return null;
        rb.linearVelocity = m.MultiplyVector(rb.linearVelocity);
        rb.angularVelocity = m.MultiplyVector(rb.angularVelocity);
    }

    #endregion
}
