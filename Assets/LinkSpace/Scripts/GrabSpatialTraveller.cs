using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static SpatialView;

/// <summary>
/// A spatialView traveller that can be grabbed by the player and interact with spatialViews.
/// Handles raycast and traversal-based teleportation while grabbed.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class GrabSpatialTraveller : SpatialTraveller
{
    #region Inspector Variables

    [HideInInspector]
    [Tooltip("XRGrabInteractable component used for grabbing interactions.")]
    public XRGrabInteractable grabInteractable;

    #endregion

    #region Public Variables

    [HideInInspector]
    [Tooltip("SpatialView from which the traveller originates.")]
    public SpatialView fromView;

    [HideInInspector]
    [Tooltip("SpatialView to which the traveller will teleport.")]
    public SpatialView toView;

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
    /// Teleports the traveller and optionally transforms velocities through the spatialView.
    /// </summary>
    public override void Teleport(Vector3 pos, Quaternion rot)
    {
        rb.position = pos;
        rb.rotation = rot;

        if (fromView != null && toView != null)
        {
            Vector3 localVel = fromView.transform.InverseTransformDirection(rb.linearVelocity);
            rb.linearVelocity = toView.transform.TransformDirection(localVel);

            Vector3 localAngVel = fromView.transform.InverseTransformDirection(rb.angularVelocity);
            rb.angularVelocity = toView.transform.TransformDirection(localAngVel);
        }
        
    }

    /// <summary>
    /// Handles behaviour when exiting a spatialView threshold.
    /// </summary>
    public override void ExitSpatialViewThreshold()
    {
        base.ExitSpatialViewThreshold();
        //if (fromView.travelMode == ObjectTravelMode.Traversation && IsGrabbed()) return;
        fromView = null;
        toView = null;
    }

    /// <summary>
    /// Sets the spatialViews for teleportation when the object is released.
    /// </summary>
    public void PrepareTeleportOnRelease(SpatialView from, SpatialView to)
    {
        fromView = from;
        toView = to;
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
            SpatialView hitView = hit.collider.GetComponentInParent<SpatialView>();
            if (hitView != null && hitView.travelMode == ObjectTravelMode.Raycast && fromView == null)
            {
                PrepareTeleportOnRelease(hitView, hitView.linkedView);
                hitView.OnTravellerEnterView(this);
                return;
            }
        }
        else
        {
            if (fromView != null && fromView.travelMode == ObjectTravelMode.Raycast)
            {
                fromView.ReleaseTraveller(this);
            }
        }
    }

    /// <summary>
    /// Handles teleportation via traversal mode when the object is grabbed.
    /// </summary>
    private void HandleTraversionTravel()
    {
        if (fromView != null && fromView.travelMode == ObjectTravelMode.Traversation && IsGrabbed() && TraveledThroughScreen())
        {
            markedForTeleport = !markedForTeleport;
        }

        lastPos = transform.position;
    }

    /// <summary>
    /// Checks if the traveller has moved through a spatialView screen.
    /// </summary>
    private bool TraveledThroughScreen()
    {
        return !fromView.SameSideOfView(transform.position, lastPos) &&
               fromView.enterCollider.ClosestPoint(transform.position) == transform.position;
    }

    /// <summary>
    /// Performs a physics raycast against spatialView screens.
    /// </summary>
    private bool Raycast(Vector3 startPos, Vector3 endPos, out RaycastHit hit)
    {
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        int spatialViewLayer = LayerMask.NameToLayer("SpatialViewScreen");
        if (spatialViewLayer == -1)
        {
            Debug.LogWarning("Layer 'SpatialViewScreen' does not exist!");
            hit = default;
            return false;
        }

        int layerMask = 1 << spatialViewLayer;
        bool didHit = Physics.Raycast(startPos, direction.normalized, out hit, distance, layerMask);

        Debug.DrawLine(startPos, didHit ? hit.point : endPos, didHit ? Color.green : Color.red, 0.1f);

        return didHit;
    }

    /// <summary>
    /// Called when the object is grabbed.
    /// </summary>
    private void OnGrab(SelectEnterEventArgs args)
    {
        LinkSpaceManager.Instance.player.AddGrabbedTraveller(this);

        if (fromView == null) return;
        fromView.ReleaseTraveller(this);
    }

    /// <summary>
    /// Called when the object is released.
    /// Handles teleportation based on mode and spatialView state.
    /// </summary>
    private void OnRelease(SelectExitEventArgs args)
    {
        LinkSpaceManager.Instance.player.RemoveGrabbedTraveller(this);

        if (fromView == null || fromView.isEndpoint) return;

        if (fromView.travelMode == ObjectTravelMode.Raycast)
        {
            if (grabRayHasHit)
            {
                Matrix4x4 m = toView.transform.localToWorldMatrix *
                              fromView.transform.worldToLocalMatrix *
                              transform.localToWorldMatrix;

                fromView.ReleaseTraveller(this);

                rb.position = m.GetColumn(3);
                rb.rotation = m.rotation;
                StartCoroutine(ApplyVelocity(m));
            }
            else
            {
                fromView.ReleaseTraveller(this);
            }
        }
        else
        {
            if (markedForTeleport)
            {
                Matrix4x4 m = toView.transform.localToWorldMatrix *
                              fromView.transform.worldToLocalMatrix *
                              transform.localToWorldMatrix;

                fromView.ReleaseTraveller(this);

                rb.position = m.GetColumn(3);
                rb.rotation = m.rotation;
                StartCoroutine(ApplyVelocity(m));
            }
            else
            {
                fromView.ReleaseTraveller(this);
            }

            fromView = null;
            toView = null;
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
