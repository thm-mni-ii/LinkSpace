using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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

    [SerializeField]
    [Tooltip("Ignore physical collision with portal screen raycast colliders so grabbed objects can actually cross the portal plane.")]
    private bool ignorePortalScreenCollisions = true;

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

    private const float MinRaycastDistance = 0.001f;

    private bool grabRayHasHit;
    private Vector3 lastPos;
    private IXRSelectInteractor primaryGrabInteractor;
    private Vector3 grabLocalPosition;
    private Quaternion grabLocalRotation = Quaternion.identity;
    private bool hasGrabPose;
    private bool isPortalTransferActive;
    private SpatialView liveSourceView;
    private SpatialView liveTargetView;
    private Vector3 liveLastSourcePosition;
    private int liveTransferStartFrame = -1;
    private float liveScaleMultiplier = 1f;
    private bool liveAppliedScale;
    private bool savedTrackPosition;
    private bool savedTrackRotation;
    private bool hasSavedGrabTrackingSettings;
    private Collider[] travellerColliders;
    private Vector3 lastPhysicsPosition;

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
        travellerColliders = GetComponentsInChildren<Collider>();
        lastPos = transform.position;
        lastPhysicsPosition = rb.position;

        if (ignorePortalScreenCollisions)
            SetPortalScreenCollisionsIgnored(true);
    }

    /// <summary>
    /// Updates raycast and traversal checks while grabbed.
    /// </summary>
    private void Update()
    {
        if (!IsGrabbed() || isPortalTransferActive)
            return;

        HandleRaycastTravel();
    }

    private void FixedUpdate()
    {
        if (IsGrabbed() || isPortalTransferActive)
        {
            lastPhysicsPosition = rb.position;
            return;
        }

        HandleUngrabbedScreenCrossingTravel();
    }

    private void LateUpdate()
    {
        if (!IsGrabbed())
        {
            lastPos = transform.position;
            return;
        }

        if (isPortalTransferActive)
            UpdateLivePortalTransfer();
        else
            HandleScreenCrossingTravel();
    }

    [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderOrder + 1)]
    private void OnBeforeRender()
    {
        if (!isPortalTransferActive || !IsGrabbed())
            return;

        ApplyLivePortalTransferPose();
    }

    /// <summary>
    /// Cleans up event listeners when destroyed.
    /// </summary>
    private void OnDestroy()
    {
        RestoreGrabTracking();

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
        ApplyPose(pos, rot);
        lastPos = pos;
        lastPhysicsPosition = pos;

        if (fromView != null && toView != null)
        {
            rb.linearVelocity = fromView.MapLinearVelocityTo(toView, rb.linearVelocity);
            rb.angularVelocity = fromView.MapAngularVelocityTo(toView, rb.angularVelocity);
        }
    }

    /// <summary>
    /// Handles behaviour when exiting a spatialView threshold.
    /// </summary>
    public override void ExitSpatialViewThreshold()
    {
        base.ExitSpatialViewThreshold();
        fromView = null;
        toView = null;
    }

    public override void ExitSpatialViewThreshold(SpatialView spatialView)
    {
        base.ExitSpatialViewThreshold(spatialView);

        if (fromView == spatialView)
        {
            fromView = null;
            toView = null;
            markedForTeleport = false;
            grabRayHasHit = false;
        }
    }

    public override bool UsesPortalVisualClone(SpatialView spatialView)
    {
        return !IsGrabbed() && !isPortalTransferActive;
    }

    /// <summary>
    /// Sets the spatialViews for teleportation when the object is released.
    /// </summary>
    public void PrepareTeleportOnRelease(SpatialView from, SpatialView to)
    {
        if (isPortalTransferActive)
            return;

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

    public bool IsPortalTransferActive => isPortalTransferActive;

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles teleportation via raycast mode when the object is grabbed.
    /// </summary>
    private void HandleRaycastTravel()
    {
        if (!TryGetPrimaryInteractorAttach(out Transform attachTransform))
        {
            grabRayHasHit = false;
            return;
        }

        RaycastHit hit;
        grabRayHasHit = Raycast(attachTransform.position, transform.position, out hit);

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
            if (fromView != null
                && fromView.travelMode == ObjectTravelMode.Raycast
                && !fromView.ContainsEnterColliderPoint(transform.position))
            {
                fromView.ReleaseTraveller(this);
            }
        }
    }

    /// <summary>
    /// Starts a live portal transfer when the grabbed object crosses the visible screen.
    /// </summary>
    private void HandleScreenCrossingTravel()
    {
        Pose sourcePose = new Pose(transform.position, transform.rotation);

        if (TryGetCrossedPortal(lastPos, sourcePose.position, out SpatialView sourceView, out SpatialView targetView))
        {
            BeginLivePortalTransfer(sourceView, targetView, sourcePose);
            return;
        }

        lastPos = sourcePose.position;
    }

    private void HandleUngrabbedScreenCrossingTravel()
    {
        Vector3 currentPosition = rb.position;

        if (TryGetCrossedPortal(lastPhysicsPosition, currentPosition, out SpatialView sourceView, out SpatialView targetView))
        {
            TransferUngrabbedThroughPortal(sourceView, targetView, new Pose(currentPosition, rb.rotation));
            return;
        }

        lastPhysicsPosition = currentPosition;
    }

    /// <summary>
    /// Performs a physics raycast against spatialView screens.
    /// </summary>
    private bool Raycast(Vector3 startPos, Vector3 endPos, out RaycastHit hit)
    {
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        if (distance <= MinRaycastDistance)
        {
            hit = default;
            return false;
        }

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

    private bool TryGetCrossedPortal(Vector3 previousSourcePosition, Vector3 currentSourcePosition, out SpatialView sourceView, out SpatialView targetView)
    {
        sourceView = null;
        targetView = null;

        if (fromView != null
            && toView != null
            && !fromView.isEndpoint
            && fromView.travelMode != ObjectTravelMode.None
            && fromView.TryGetVisibleScreenCrossing(previousSourcePosition, currentSourcePosition, out _, out _))
        {
            sourceView = fromView;
            targetView = toView;
            return true;
        }

        if (!TryRaycastCrossedScreen(previousSourcePosition, currentSourcePosition, out SpatialView crossedView))
            return false;

        if (crossedView == null
            || crossedView.linkedView == null
            || crossedView.isEndpoint
            || crossedView.travelMode == ObjectTravelMode.None)
        {
            return false;
        }

        sourceView = crossedView;
        targetView = crossedView.linkedView;
        return true;
    }

    private void TransferUngrabbedThroughPortal(SpatialView sourceView, SpatialView targetView, Pose sourcePose)
    {
        if (sourceView == null || targetView == null || targetView.isEndpoint)
        {
            lastPhysicsPosition = sourcePose.position;
            return;
        }

        Vector3 sourceVisualPosition = transform.position;
        Quaternion sourceVisualRotation = transform.rotation;

        fromView = sourceView;
        toView = targetView;

        Teleport(
            sourceView.MapPointTo(targetView, sourcePose.position),
            sourceView.MapRotationTo(targetView, sourcePose.rotation));

        if (sourceView.ShouldScaleTravellerObjects())
            ApplyMappingScale(sourceView.GetMappingScaleTo(targetView));

        sourceView.onObjectTravel.Invoke(this);

        if (graphicsClone != null)
            graphicsClone.transform.SetPositionAndRotation(sourceVisualPosition, sourceVisualRotation);

        sourceView.ReleaseTraveller(this);
        targetView.OnTravellerTeleportedIntoView(this);

        lastPos = rb.position;
        lastPhysicsPosition = rb.position;
    }

    private bool TryRaycastCrossedScreen(Vector3 previousSourcePosition, Vector3 currentSourcePosition, out SpatialView crossedView)
    {
        crossedView = null;

        Vector3 direction = currentSourcePosition - previousSourcePosition;
        float distance = direction.magnitude;

        if (distance <= MinRaycastDistance)
            return false;

        int spatialViewLayer = LayerMask.NameToLayer("SpatialViewScreen");
        if (spatialViewLayer == -1)
            return false;

        int layerMask = 1 << spatialViewLayer;

        if (!Physics.Raycast(previousSourcePosition, direction.normalized, out RaycastHit hit, distance, layerMask, QueryTriggerInteraction.Ignore))
            return false;

        SpatialView hitView = hit.collider.GetComponentInParent<SpatialView>();

        if (hitView == null || !hitView.TryGetVisibleScreenCrossing(previousSourcePosition, currentSourcePosition, out _, out _))
            return false;

        crossedView = hitView;
        return true;
    }

    /// <summary>
    /// Called when the object is grabbed.
    /// </summary>
    private void OnGrab(SelectEnterEventArgs args)
    {
        LinkSpaceManager.Instance?.player?.AddGrabbedTraveller(this);
        CaptureGrabPose(args.interactorObject);
        DisablePortalVisuals();

        if (ignorePortalScreenCollisions)
            SetPortalScreenCollisionsIgnored(true);

        lastPos = transform.position;

        if (fromView != null && !fromView.ContainsEnterColliderPoint(transform.position))
            fromView.ReleaseTraveller(this);
    }

    /// <summary>
    /// Called when the object is released.
    /// Handles teleportation based on mode and spatialView state.
    /// </summary>
    private void OnRelease(SelectExitEventArgs args)
    {
        LinkSpaceManager.Instance?.player?.RemoveGrabbedTraveller(this);

        if (isPortalTransferActive)
        {
            SpatialView sourceView = liveSourceView;
            SpatialView targetView = liveTargetView;

            ClearLivePortalTransfer();

            if (sourceView != null && targetView != null)
            {
                StartCoroutine(ApplyVelocity(sourceView, targetView));
                StartCoroutine(TrackReleasedObjectFromViewNextFrame(targetView));
            }

            ClearGrabPose();
            return;
        }

        RefreshReleasedObjectTracking();
        ClearGrabPose();
    }

    private void BeginLivePortalTransfer(SpatialView sourceView, SpatialView targetView, Pose sourcePose)
    {
        if (sourceView == null || targetView == null || targetView.isEndpoint)
            return;

        liveSourceView = sourceView;
        liveTargetView = targetView;
        liveLastSourcePosition = sourcePose.position;
        liveTransferStartFrame = Time.frameCount;
        liveScaleMultiplier = sourceView.GetMappingScaleTo(targetView);
        liveAppliedScale = sourceView.ShouldScaleTravellerObjects();
        isPortalTransferActive = true;
        markedForTeleport = false;
        grabRayHasHit = false;

        if (primaryGrabInteractor != null)
            CaptureGrabPose(primaryGrabInteractor, sourcePose);

        sourceView.ReleaseTraveller(this);
        DisablePortalVisuals();
        fromView = null;
        toView = null;
        SaveAndDisableGrabTracking();

        ApplyMappedPose(sourceView, targetView, sourcePose);

        if (liveAppliedScale)
            ApplyMappingScale(liveScaleMultiplier);

        rb.linearVelocity = sourceView.MapLinearVelocityTo(targetView, rb.linearVelocity);
        rb.angularVelocity = sourceView.MapAngularVelocityTo(targetView, rb.angularVelocity);
        sourceView.onObjectTravel.Invoke(this);
    }

    private void UpdateLivePortalTransfer()
    {
        if (liveSourceView == null || liveTargetView == null)
        {
            ClearLivePortalTransfer();
            return;
        }

        if (!TryGetCurrentGrabSourcePose(out Pose sourcePose))
        {
            ClearLivePortalTransfer();
            return;
        }

        if (Time.frameCount != liveTransferStartFrame
            && liveSourceView.TryGetVisibleScreenCrossing(liveLastSourcePosition, sourcePose.position, out _, out _))
        {
            EndLivePortalTransferOnSourceSide(sourcePose);
            return;
        }

        ApplyMappedPose(liveSourceView, liveTargetView, sourcePose);
        liveLastSourcePosition = sourcePose.position;
    }

    private void ApplyLivePortalTransferPose()
    {
        if (liveSourceView == null || liveTargetView == null)
            return;

        if (!TryGetCurrentGrabSourcePose(out Pose sourcePose))
            return;

        ApplyMappedPose(liveSourceView, liveTargetView, sourcePose);
    }

    private void EndLivePortalTransferOnSourceSide(Pose sourcePose)
    {
        SpatialView sourceView = liveSourceView;
        SpatialView targetView = liveTargetView;

        if (liveAppliedScale)
            ApplyMappingScale(1f / Mathf.Max(0.0001f, liveScaleMultiplier));

        ApplyPose(sourcePose.position, sourcePose.rotation);
        ClearLivePortalTransfer();

        fromView = sourceView;
        toView = targetView;

        if (sourceView != null)
            sourceView.OnTravellerEnterView(this);

        lastPos = sourcePose.position;
    }

    private void ApplyMappedPose(SpatialView sourceView, SpatialView targetView, Pose sourcePose)
    {
        Vector3 mappedPosition = sourceView.MapPointTo(targetView, sourcePose.position);
        Quaternion mappedRotation = sourceView.MapRotationTo(targetView, sourcePose.rotation);
        ApplyPose(mappedPosition, mappedRotation);
    }

    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        Physics.SyncTransforms();
    }

    private bool TryGetCurrentGrabSourcePose(out Pose sourcePose)
    {
        sourcePose = new Pose(transform.position, transform.rotation);

        if (!TryGetPrimaryInteractorAttach(out Transform attachTransform))
            return false;

        sourcePose = new Pose(
            attachTransform.TransformPoint(grabLocalPosition),
            attachTransform.rotation * grabLocalRotation);

        return true;
    }

    private bool TryGetPrimaryInteractorAttach(out Transform attachTransform)
    {
        attachTransform = null;

        if (grabInteractable == null || !grabInteractable.isSelected)
            return false;

        if (!hasGrabPose
            || primaryGrabInteractor == null
            || !primaryGrabInteractor.IsSelecting(grabInteractable))
        {
            if (grabInteractable.interactorsSelecting.Count == 0)
                return false;

            CaptureGrabPose(grabInteractable.interactorsSelecting[0]);
        }

        if (primaryGrabInteractor == null)
            return false;

        attachTransform = GetGrabDriverTransform(primaryGrabInteractor);
        return attachTransform != null;
    }

    private void CaptureGrabPose(IXRSelectInteractor interactor)
    {
        primaryGrabInteractor = interactor;
        hasGrabPose = false;

        if (grabInteractable == null || primaryGrabInteractor == null)
            return;

        CaptureGrabPose(primaryGrabInteractor, new Pose(transform.position, transform.rotation));
    }

    private void CaptureGrabPose(IXRSelectInteractor interactor, Pose objectPose)
    {
        primaryGrabInteractor = interactor;
        hasGrabPose = false;

        if (grabInteractable == null || primaryGrabInteractor == null)
            return;

        Transform attachTransform = GetGrabDriverTransform(primaryGrabInteractor);

        if (attachTransform == null)
            return;

        grabLocalPosition = attachTransform.InverseTransformPoint(objectPose.position);
        grabLocalRotation = Quaternion.Inverse(attachTransform.rotation) * objectPose.rotation;
        hasGrabPose = true;
    }

    private Transform GetGrabDriverTransform(IXRSelectInteractor interactor)
    {
        if (interactor is LinkSpaceNearFarInteractor linkSpaceNearFarInteractor
            && linkSpaceNearFarInteractor.TryGetStableGrabDriverTransform(out Transform driverTransform))
        {
            return driverTransform;
        }

        return interactor.GetAttachTransform(grabInteractable);
    }

    private void ClearGrabPose()
    {
        primaryGrabInteractor = null;
        hasGrabPose = false;
        grabLocalPosition = Vector3.zero;
        grabLocalRotation = Quaternion.identity;
    }

    private void ClearLivePortalTransfer()
    {
        RestoreGrabTracking();
        isPortalTransferActive = false;
        liveSourceView = null;
        liveTargetView = null;
        liveLastSourcePosition = Vector3.zero;
        liveTransferStartFrame = -1;
        liveScaleMultiplier = 1f;
        liveAppliedScale = false;
        markedForTeleport = false;
        grabRayHasHit = false;
    }

    private void SaveAndDisableGrabTracking()
    {
        if (grabInteractable == null || hasSavedGrabTrackingSettings)
            return;

        savedTrackPosition = grabInteractable.trackPosition;
        savedTrackRotation = grabInteractable.trackRotation;
        hasSavedGrabTrackingSettings = true;

        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
    }

    private void RestoreGrabTracking()
    {
        if (grabInteractable == null || !hasSavedGrabTrackingSettings)
            return;

        grabInteractable.trackPosition = savedTrackPosition;
        grabInteractable.trackRotation = savedTrackRotation;
        hasSavedGrabTrackingSettings = false;
    }

    private void RefreshReleasedObjectTracking()
    {
        SpatialView sourceView = fromView;
        fromView = null;
        toView = null;
        markedForTeleport = false;
        grabRayHasHit = false;

        if (sourceView == null || sourceView.isEndpoint)
            return;

        sourceView.ReleaseTraveller(this);
        StartCoroutine(TrackReleasedObjectFromViewNextFrame(sourceView));
    }

    private IEnumerator TrackReleasedObjectFromViewNextFrame(SpatialView spatialView)
    {
        yield return null;

        if (spatialView == null || spatialView.isEndpoint || !spatialView.ContainsEnterColliderPoint(transform.position))
            yield break;

        fromView = spatialView;
        toView = spatialView.linkedView;
        spatialView.OnTravellerEnterView(this);
    }

    /// <summary>
    /// Applies transformed velocities after teleportation.
    /// </summary>
    private IEnumerator ApplyVelocity(SpatialView sourceView, SpatialView targetView)
    {
        yield return null;
        rb.linearVelocity = sourceView.MapLinearVelocityTo(targetView, rb.linearVelocity);
        rb.angularVelocity = sourceView.MapAngularVelocityTo(targetView, rb.angularVelocity);
    }

    private void SetPortalScreenCollisionsIgnored(bool ignored)
    {
        if (travellerColliders == null || travellerColliders.Length == 0)
            travellerColliders = GetComponentsInChildren<Collider>();

        if (travellerColliders == null || travellerColliders.Length == 0)
            return;

        SpatialViewScreen[] screens = FindObjectsByType<SpatialViewScreen>(FindObjectsInactive.Exclude);
        for (int i = 0; i < screens.Length; i++)
        {
            Collider screenCollider = screens[i] != null ? screens[i].GetComponent<Collider>() : null;
            if (screenCollider == null)
                continue;

            for (int j = 0; j < travellerColliders.Length; j++)
            {
                Collider travellerCollider = travellerColliders[j];
                if (travellerCollider == null || travellerCollider == screenCollider)
                    continue;

                Physics.IgnoreCollision(travellerCollider, screenCollider, ignored);
            }
        }
    }

    #endregion
}
