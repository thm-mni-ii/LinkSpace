using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

[AddComponentMenu("XR/Interactors/Portal Ray Interactor")]
public class PortalRayInteractor : XRRayInteractor
{
    public LineRenderer lineRenderer;
    [Header("Arc Settings")]
    public int arcSegments = 20;
    public float maxArcDistance = 10f;
    public float arcSpeed = 10f;


    private PortalScreen currentPortalScreen;
    private RaycastHit currentPortalHit;
    private XRInteractorLineVisual lineVisual;
    private LineRenderer tpLine;
    private Vector3 redirectDir;

    private TeleportationProvider tpProvider;

    private bool isActiveTeleportInteractor = false;

    private Vector2 previousMove;
    private const float deadZone = 0.5f;
    private Vector3 tpPoint = Vector3.zero;


    protected override void Awake()
    {
        base.Awake();

        lineVisual = GetComponent<XRInteractorLineVisual>();
        tpLine = GetComponent<LineRenderer>();

        lineRenderer.material = tpLine.material;
        lineRenderer.startWidth = tpLine.startWidth;
        lineRenderer.endWidth = tpLine.endWidth;

        tpProvider = FindAnyObjectByType<TeleportationProvider>();
    }


    public override void ProcessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractor(updatePhase);

        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            CheckPortalHit();

            if (currentPortalScreen != null && currentPortalScreen.portal.linkedPortal != null)
            {
                RedirectRayThroughPortalArc(currentPortalScreen.portal, currentPortalHit);
            }

            if (isActiveTeleportInteractor && ThumbstickReleased())
            {
                TryTeleport();
                isActiveTeleportInteractor = false;
                lineVisual.reticle.gameObject.SetActive(false);
            }
        }
    }

    private bool ThumbstickReleased()
    {
        Vector2 current = directionalManipulationInput.ReadValue();
        Debug.Log(current.magnitude);
        bool wasMoving = previousMove.magnitude > deadZone;
        bool isMoving = current.magnitude > deadZone;

        bool released = wasMoving && !isMoving;
        previousMove = current;
        return released;
    }

    private void CheckPortalHit()
    {
        if (TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            PortalScreen portalScreen = hit.collider.GetComponent<PortalScreen>();
            if (portalScreen != null)
            {
                currentPortalScreen = portalScreen;
                currentPortalHit = hit;
                lineRenderer.enabled = true;
                int count = tpLine.positionCount;
                if (count >= 2)
                {
                    Vector3 segmentStart = tpLine.GetPosition(count - 2);
                    Vector3 segmentEnd = tpLine.GetPosition(count - 1);

                    //Debug.Log($"Last segment start: {segmentStart}, end: {segmentEnd}");

                    // Compute the angle of the segment in world space (XZ plane)
                    redirectDir = (segmentEnd - segmentStart).normalized;

                    // Draw an extended line (e.g., 2 units longer than actual segment)
                    //float extensionLength = 2f;
                    // Debug.DrawLine(segmentStart + -direction * extensionLength, segmentEnd + direction * extensionLength, Color.red, 1f);
                }
            }
            else
            {
                currentPortalScreen = null;
                lineRenderer.enabled = false;
            }
        }
        else
        {
            currentPortalScreen = null;
            lineRenderer.enabled = false;
        }
    }

    private void RedirectRayThroughPortalArc(Portal portal, RaycastHit hit)
    {
        if (portal == null || portal.linkedPortal == null || lineRenderer == null)
            return;
        Transform portalTransform = portal.transform;
        Transform linkedTransform = portal.linkedPortal.transform;

        // --- Step 1: Original direction from attachTransform to hit ---
        Vector3 originalDir = (hit.point - attachTransform.position).normalized;

        // --- Step 2: Move hit point & direction into portal local space ---
        Vector3 localHitPoint = portalTransform.InverseTransformPoint(hit.point);
        Vector3 localDir = portalTransform.InverseTransformDirection(originalDir);

        // --- Step 4: Transform into linked portal world space ---
        Vector3 newOrigin = linkedTransform.TransformPoint(localHitPoint);
        Vector3 exitDir = linkedTransform.TransformDirection(portalTransform.InverseTransformDirection(redirectDir));
        exitDir.Normalize();

        // --- Step 5: Generate arc points ---
        Vector3[] arcPoints = new Vector3[arcSegments];
        for (int i = 0; i < arcSegments; i++)
        {
            float t = i / (float)(arcSegments - 1) * (maxArcDistance / arcSpeed);
            arcPoints[i] = newOrigin + exitDir * arcSpeed * t + 0.5f * Physics.gravity * t * t;
        }

        // --- Step 6: Check collisions and optionally stop arc ---
        int layerMask = ~LayerMask.GetMask("PortalScreen");
        int visiblePoints = arcSegments;
        for (int i = 0; i < arcSegments - 1; i++)
        {
            if (Physics.Linecast(arcPoints[i], arcPoints[i + 1], out RaycastHit hitInfo, layerMask))
            {
                visiblePoints = i + 1; // stop the arc at collision
                BaseTeleportationInteractable tpInteractable = hitInfo.collider.GetComponent<BaseTeleportationInteractable>();
                if (tpInteractable != null)
                {
                    lineRenderer.colorGradient = lineVisual.validColorGradient;
                    tpLine.colorGradient = lineVisual.validColorGradient;
                    tpPoint = hitInfo.point;
                    isActiveTeleportInteractor = true;
                    lineVisual.reticle.transform.position = tpPoint;
                    lineVisual.reticle.gameObject.SetActive(true);

                }
                else
                {
                    lineRenderer.colorGradient = lineVisual.invalidColorGradient;
                    tpLine.colorGradient = lineVisual.invalidColorGradient;
                    isActiveTeleportInteractor = false;
                    lineVisual.reticle.gameObject.SetActive(false);
                }
                break;
            }
        }


        lineRenderer.positionCount = Mathf.Min(visiblePoints + 1, arcPoints.Length);
        lineRenderer.SetPositions(arcPoints);

        // --- Step 7: Update LineRenderer ---
        for (int i = 0; i < visiblePoints; i++)
            lineRenderer.SetPosition(i, arcPoints[i]);


    }

    private void TryTeleport()
    {
        if (tpPoint == Vector3.zero)
            return;
        currentPortalScreen.portal.ReleaseTraveller(PortalManager.Instance.player);
        PortalManager.Instance.player.transform.SetPositionAndRotation(tpPoint, PortalManager.Instance.player.transform.rotation);
      
    }
}