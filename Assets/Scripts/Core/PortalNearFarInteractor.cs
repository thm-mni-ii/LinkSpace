using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[AddComponentMenu("XR/Interactors/Portal Near-Far Interactor")]
public class PortalNearFarInteractor : NearFarInteractor
{
    [Header("Portal Settings")]
    public Portal currentPortal; // Updated dynamically
    private Vector3 currentPortalHitPoint; // Hit point on the portal plane

    /// <summary>
    /// Called each frame to update which portal the ray is currently pointing at
    /// </summary>
    public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.PreprocessInteractor(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        UpdateCurrentPortal();
        RedirectRayThroughCurrentPortal();
    }

    /// <summary>
    /// Updates currentPortal if the far caster ray hits a portal screen
    /// </summary>
    private void UpdateCurrentPortal()
    {
        if (farInteractionCaster == null)
        {
            currentPortal = null;
            return;
        }

        // Get the far ray's start and end points
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
                // Draw debug ray to portal
                //Debug.DrawRay(origin, direction * rayLength, Color.green);
                return;
            }
        }

        currentPortal = null;
    }

    /// <summary>
    /// Redirects the far ray through the portal if needed
    /// </summary>
    public void RedirectRayThroughCurrentPortal()
    {
        if (currentPortal == null || currentPortal.linkedPortal == null)
            return;

        Transform portalTransform = currentPortal.transform;
        Transform linkedTransform = currentPortal.linkedPortal.transform;

        // Direction from the interactor to the original far ray hit
        Vector3 rayDir = (farInteractionCaster.lastSamplePoint - farInteractionCaster.castOrigin.position).normalized;

        // Transform the hit point through the portal
        Matrix4x4 portalToLinked = linkedTransform.localToWorldMatrix * portalTransform.worldToLocalMatrix;
        Vector3 newOrigin = portalToLinked.MultiplyPoint(currentPortalHitPoint);
        Vector3 newDirection = portalToLinked.MultiplyVector(rayDir);

        float rayLength = 10f; // Max distance for redirected interaction

        // Debug visualization
        //Debug.DrawRay(newOrigin, newDirection * rayLength, Color.blue);
        int layerMask = ~LayerMask.GetMask("PortalScreen");
        if (Physics.Raycast(newOrigin, newDirection, out RaycastHit redirectedHit, rayLength,layerMask))
        {
            IXRSelectInteractable interactable = redirectedHit.collider.GetComponentInParent<IXRSelectInteractable>();
            if (interactable != null && !hasSelection && IsGrabPressed())
            {
                // Calculate object offset relative to linked portal
                Vector3 localOffsetToLinkedPortal = linkedTransform.InverseTransformPoint(interactable.transform.position);

                // Transform offset into current portal's space
                Vector3 worldOffsetAtCurrentPortal = portalTransform.TransformPoint(localOffsetToLinkedPortal);

                // Select the object
                interactionManager.SelectEnter(this, interactable);

                // Apply portal-relative offset
                interactionAttachController.MoveTo(worldOffsetAtCurrentPortal);
                interactionAttachController.ApplyLocalRotationOffset(Quaternion.identity);
                GrabPortalTraveller grabPortalTraveller = interactable.transform.gameObject.GetComponent<GrabPortalTraveller>();
                if (!PortalManager.Instance.useRaycastTravel && grabPortalTraveller)
                {
                    grabPortalTraveller.markedForTeleport = true;
                }

                // Optional: debug hit point
                //Debug.DrawRay(redirectedHit.point, Vector3.up * 0.2f, Color.red, 1f);
            }
        }
    }


    /// <summary>
    /// Helper to check if grab input is pressed
    /// </summary>
    private bool IsGrabPressed()
    {
        // Check the interactor's select input (like the grab button)
        return selectInput != null && selectInput.ReadValue() > 0.5f && !hasSelection;
    }

}
