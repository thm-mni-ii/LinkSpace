using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static Portal;

/// <summary>
/// A Near-Far interactor that supports interacting with objects through portals.
/// Redirects rays through portal screens for far interactions.
/// </summary>
[AddComponentMenu("XR/Interactors/Portal Near-Far Interactor")]
public class PortalNearFarInteractor : NearFarInteractor
{
    #region Private Variables
    [HideInInspector]
    [Tooltip("The portal currently hit by the far ray.")]
    public Portal currentPortal;
    private Vector3 currentPortalHitPoint; // Hit point on the portal plane

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Called each frame to update which portal the ray is pointing at and handle redirection.
    /// </summary>
    public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.PreprocessInteractor(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        UpdateCurrentPortal();
        RedirectRayThroughCurrentPortal();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the current portal if the far ray hits a portal screen.
    /// </summary>
    private void UpdateCurrentPortal()
    {
        if (farInteractionCaster == null)
        {
            currentPortal = null;
            return;
        }

        Transform rayOrigin = farInteractionCaster.castOrigin;
        Vector3 origin = rayOrigin.position;
        Vector3 endPoint = farInteractionCaster.lastSamplePoint;

        Vector3 direction = (endPoint - origin).normalized;
        float rayLength = Vector3.Distance(origin, endPoint);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, rayLength))
        {
            PortalScreen screen = hit.collider.GetComponent<PortalScreen>();
            if (screen != null)
            {
                currentPortal = screen.portal;
                currentPortalHitPoint = hit.point;
                return;
            }
        }

        currentPortal = null;
    }

    /// <summary>
    /// Redirects the far ray through the current portal and selects interactables if hit.
    /// </summary>
    public void RedirectRayThroughCurrentPortal()
    {
        if (currentPortal == null || currentPortal.linkedPortal == null)
            return;

        Transform portalTransform = currentPortal.transform;
        Transform linkedTransform = currentPortal.linkedPortal.transform;

        Vector3 rayDir = (farInteractionCaster.lastSamplePoint - farInteractionCaster.castOrigin.position).normalized;

        Matrix4x4 portalToLinked = linkedTransform.localToWorldMatrix * portalTransform.worldToLocalMatrix;
        Vector3 newOrigin = portalToLinked.MultiplyPoint(currentPortalHitPoint);
        Vector3 newDirection = portalToLinked.MultiplyVector(rayDir);

        float rayLength = 10f;

        int layerMask = ~LayerMask.GetMask("PortalScreen");
        if (Physics.Raycast(newOrigin, newDirection, out RaycastHit redirectedHit, rayLength, layerMask))
        {
            IXRSelectInteractable interactable = redirectedHit.collider.GetComponentInParent<IXRSelectInteractable>();
            if (interactable != null && !hasSelection && IsGrabPressed())
            {
                Vector3 localOffsetToLinkedPortal = linkedTransform.InverseTransformPoint(interactable.transform.position);
                Vector3 worldOffsetAtCurrentPortal = portalTransform.TransformPoint(localOffsetToLinkedPortal);

                interactionManager.SelectEnter(this, interactable);
                interactionAttachController.MoveTo(worldOffsetAtCurrentPortal);
                interactionAttachController.ApplyLocalRotationOffset(Quaternion.identity);

                GrabPortalTraveller grabPortalTraveller = interactable.transform.gameObject.GetComponent<GrabPortalTraveller>();
                if (currentPortal.travelMode == ObjectTravelMode.Traversation && grabPortalTraveller)
                {
                    grabPortalTraveller.markedForTeleport = true;
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
