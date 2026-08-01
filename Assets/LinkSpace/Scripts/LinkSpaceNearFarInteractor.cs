using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using static SpatialView;

/// <summary>
/// A Near-Far interactor that supports interacting with objects through spatialViews.
/// Redirects rays through spatialView screens for far interactions.
/// </summary>
[AddComponentMenu("XR/Interactors/SpatialView Near-Far Interactor")]
public class LinkSpaceNearFarInteractor : NearFarInteractor
{
    #region Private Variables
    [HideInInspector]
    [Tooltip("The spatialView currently hit by the far ray.")]
    public SpatialView currentView;
    private Vector3 currentViewHitPoint; // Hit point on the spatialView plane
    private CurveVisualController curveVisualController;
    private LineRenderer curveLineRenderer;
    private bool curveVisualHiddenForGrabbedTraveller;
    private bool curveVisualWasEnabled;

    #endregion

    #region Unity Callbacks

    protected override void Awake()
    {
        base.Awake();

        curveVisualController = GetComponent<CurveVisualController>();
        if (curveVisualController != null)
            curveLineRenderer = curveVisualController.lineRenderer;
    }

    /// <summary>
    /// Called each frame to update which spatialView the ray is pointing at and handle redirection.
    /// </summary>
    public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.PreprocessInteractor(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        UpdateGrabbedTravellerCurveVisualState();
        UpdateCurrentSpatialView();
        RedirectRayThroughCurrentSpatialView();
    }

    protected override void OnDisable()
    {
        RestoreGrabbedTravellerCurveVisual();
        base.OnDisable();
    }

    #endregion

    #region Public Methods

    public bool TryGetStableGrabDriverTransform(out Transform driverTransform)
    {
        driverTransform = null;

        if (farInteractionCaster != null && farInteractionCaster.castOrigin != null)
        {
            driverTransform = farInteractionCaster.castOrigin;
            return true;
        }

        driverTransform = attachTransform != null ? attachTransform : transform;
        return driverTransform != null;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the current spatialView if the far ray hits a spatialView screen.
    /// </summary>
    private void UpdateCurrentSpatialView()
    {
        if (farInteractionCaster == null)
        {
            currentView = null;
            return;
        }

        Transform rayOrigin = farInteractionCaster.castOrigin;
        Vector3 origin = rayOrigin.position;
        Vector3 endPoint = farInteractionCaster.lastSamplePoint;

        Vector3 direction = (endPoint - origin).normalized;
        float rayLength = Vector3.Distance(origin, endPoint);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, rayLength))
        {
            SpatialViewScreen screen = hit.collider.GetComponent<SpatialViewScreen>();
            if (screen != null)
            {
                currentView = screen.spatialView;
                currentViewHitPoint = hit.point;
                return;
            }
        }

        currentView = null;
    }

    private void UpdateGrabbedTravellerCurveVisualState()
    {
        if (TryGetSelectedGrabTraveller(out _))
        {
            HideGrabbedTravellerCurveVisual();
            return;
        }

        RestoreGrabbedTravellerCurveVisual();
    }

    private bool TryGetSelectedGrabTraveller(out GrabSpatialTraveller traveller)
    {
        traveller = null;

        if (!hasSelection)
            return false;

        for (int i = 0; i < interactablesSelected.Count; i++)
        {
            IXRSelectInteractable interactable = interactablesSelected[i];
            if (interactable == null || interactable.transform == null)
                continue;

            traveller = interactable.transform.GetComponent<GrabSpatialTraveller>();
            if (traveller == null)
                traveller = interactable.transform.GetComponentInParent<GrabSpatialTraveller>();

            if (traveller != null)
                return true;
        }

        traveller = null;
        return false;
    }

    private void HideGrabbedTravellerCurveVisual()
    {
        if (curveVisualHiddenForGrabbedTraveller)
            return;

        if (curveVisualController == null)
            curveVisualController = GetComponent<CurveVisualController>();

        if (curveVisualController == null)
            return;

        curveVisualHiddenForGrabbedTraveller = true;
        curveVisualWasEnabled = curveVisualController.enabled;

        if (curveLineRenderer == null)
            curveLineRenderer = curveVisualController.lineRenderer;

        if (curveLineRenderer != null)
            curveLineRenderer.enabled = false;

        curveVisualController.enabled = false;
    }

    private void RestoreGrabbedTravellerCurveVisual()
    {
        if (!curveVisualHiddenForGrabbedTraveller)
            return;

        if (curveLineRenderer != null)
            curveLineRenderer.enabled = false;

        if (curveVisualController != null)
            curveVisualController.enabled = curveVisualWasEnabled;

        curveVisualHiddenForGrabbedTraveller = false;
    }

    /// <summary>
    /// Redirects the far ray through the current spatialView and selects interactables if hit.
    /// </summary>
    public void RedirectRayThroughCurrentSpatialView()
    {
        if (currentView == null || currentView.linkedView == null)
            return;

        Vector3 rayDir = (farInteractionCaster.lastSamplePoint - farInteractionCaster.castOrigin.position).normalized;

        Vector3 newOrigin = currentView.MapPointToLinkedView(currentViewHitPoint);
        Vector3 newDirection = currentView.MapDirectionToLinkedView(rayDir);

        float rayLength = 10f * currentView.GetMappingScaleTo(currentView.linkedView);

        int layerMask = ~LayerMask.GetMask("SpatialViewScreen");
        if (Physics.Raycast(newOrigin, newDirection, out RaycastHit redirectedHit, rayLength, layerMask))
        {
            IXRSelectInteractable interactable = redirectedHit.collider.GetComponentInParent<IXRSelectInteractable>();
            if (interactable != null && !hasSelection && IsGrabPressed())
            {
                Vector3 worldOffsetAtCurrentSpatialView = currentView.MapLinkedPointToSource(interactable.transform.position);

                interactionManager.SelectEnter(this, interactable);
                interactionAttachController.MoveTo(worldOffsetAtCurrentSpatialView);
                interactionAttachController.ApplyLocalRotationOffset(Quaternion.identity);

                GrabSpatialTraveller grabSpatialTraveller = interactable.transform.gameObject.GetComponent<GrabSpatialTraveller>();
                if (currentView.travelMode == ObjectTravelMode.Traversation && grabSpatialTraveller)
                {
                    grabSpatialTraveller.markedForTeleport = true;
                }
            }
        }
    }

    /// <summary>
    /// Checks if the interactor's grab input is pressed.
    /// </summary>
    private bool IsGrabPressed()
    {
        return selectInput != null && selectInput.ReadValue() > 0.5f && !hasSelection;
    }

    #endregion
}
