using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// XR Ray Interactor that supports teleportation through portals using an arc.
/// </summary>
[AddComponentMenu("XR/Interactors/Portal Ray Interactor")]
public class PortalRayInteractor : XRRayInteractor
{
    #region Inspector Variables

    [Tooltip("Line renderer used to visualize portal ray arcs.")]
    public LineRenderer lineRenderer;

    [Header("Arc Settings")]
    [Tooltip("Number of segments used to draw the teleportation arc.")]
    public int arcSegments = 20;

    [Tooltip("Maximum distance of the teleportation arc.")]
    public float maxArcDistance = 10f;

    [Tooltip("Speed multiplier for the teleportation arc.")]
    public float arcSpeed = 10f;

    #endregion

    #region Private Variables

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

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Initializes references and line renderer settings.
    /// </summary>
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

    /// <summary>
    /// Processes ray interactions each frame.
    /// </summary>
    public override void ProcessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractor(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        CheckPortalHit();

        if (currentPortalScreen != null && currentPortalScreen.portal.linkedPortal != null)
        {
            if (!currentPortalScreen.portal.teleportable) return;
            RedirectRayThroughPortalArc(currentPortalScreen.portal, currentPortalHit);
        }

        if (isActiveTeleportInteractor && ThumbstickReleased())
        {
            if (currentPortalScreen.portal.teleportable)
                TryTeleport();

            isActiveTeleportInteractor = false;
            lineVisual.reticle.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Checks if the thumbstick was released this frame.
    /// </summary>
    private bool ThumbstickReleased()
    {
        Vector2 current = directionalManipulationInput.ReadValue();
        bool wasMoving = previousMove.magnitude > deadZone;
        bool isMoving = current.magnitude > deadZone;

        bool released = wasMoving && !isMoving;
        previousMove = current;
        return released;
    }

    /// <summary>
    /// Checks if the ray hits a portal screen and updates current portal info.
    /// </summary>
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
                    redirectDir = (segmentEnd - segmentStart).normalized;
                }
                return;
            }
        }

        currentPortalScreen = null;
        lineRenderer.enabled = false;
    }

    /// <summary>
    /// Redirects the teleportation arc through a portal and calculates collisions.
    /// </summary>
    private void RedirectRayThroughPortalArc(Portal portal, RaycastHit hit)
    {
        if (portal == null || portal.linkedPortal == null || lineRenderer == null)
            return;

        Transform portalTransform = portal.transform;
        Transform linkedTransform = portal.linkedPortal.transform;

        Vector3 originalDir = (hit.point - attachTransform.position).normalized;
        Vector3 localHitPoint = portalTransform.InverseTransformPoint(hit.point);
        Vector3 localDir = portalTransform.InverseTransformDirection(originalDir);

        Vector3 newOrigin = linkedTransform.TransformPoint(localHitPoint);
        Vector3 exitDir = linkedTransform.TransformDirection(portalTransform.InverseTransformDirection(redirectDir)).normalized;

        Vector3[] arcPoints = new Vector3[arcSegments];
        for (int i = 0; i < arcSegments; i++)
        {
            float t = i / (float)(arcSegments - 1) * (maxArcDistance / arcSpeed);
            arcPoints[i] = newOrigin + exitDir * arcSpeed * t + 0.5f * Physics.gravity * t * t;
        }

        int layerMask = ~LayerMask.GetMask("PortalScreen");
        int visiblePoints = arcSegments;
        for (int i = 0; i < arcSegments - 1; i++)
        {
            if (Physics.Linecast(arcPoints[i], arcPoints[i + 1], out RaycastHit hitInfo, layerMask))
            {
                visiblePoints = i + 1;
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
    }

    /// <summary>
    /// Performs the teleportation to the selected point through the portal.
    /// </summary>
    private void TryTeleport()
    {
        if (tpPoint == Vector3.zero)
            return;

        currentPortalScreen.portal.ReleaseTraveller(PortalManager.Instance.player);
        PortalManager.Instance.player.transform.SetPositionAndRotation(tpPoint, PortalManager.Instance.player.transform.rotation);
    }

    #endregion
}
