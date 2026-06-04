using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Called each frame to update which spatialView the ray is pointing at and handle redirection.
    /// </summary>
    public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.PreprocessInteractor(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        UpdateCurrentSpatialView();
        RedirectRayThroughCurrentSpatialView();
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
