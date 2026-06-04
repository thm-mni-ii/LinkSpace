using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// XR Ray Interactor that supports teleportation through spatialViews using an arc.
/// </summary>
[AddComponentMenu("XR/Interactors/SpatialView Ray Interactor")]
public class LinkSpaceRayInteractor : XRRayInteractor
{
    #region Inspector Variables

    [Tooltip("Line renderer used to visualize spatialView ray arcs.")]
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

    private SpatialViewScreen currentSpatialViewScreen;
    private RaycastHit currentViewHit;
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

        CheckSpatialViewHit();

        if (currentSpatialViewScreen != null && currentSpatialViewScreen.spatialView.linkedView != null)
        {
            if (!currentSpatialViewScreen.spatialView.teleportable) return;
            RedirectRayThroughSpatialViewArc(currentSpatialViewScreen.spatialView, currentViewHit);
        }

        if (isActiveTeleportInteractor && ThumbstickReleased())
        {
            if (currentSpatialViewScreen.spatialView.teleportable)
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
    /// Checks if the ray hits a spatialView screen and updates current spatialView info.
    /// </summary>
    private void CheckSpatialViewHit()
    {
        if (TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            SpatialViewScreen spatialViewScreen = hit.collider.GetComponent<SpatialViewScreen>();
            if (spatialViewScreen != null)
            {
                currentSpatialViewScreen = spatialViewScreen;
                currentViewHit = hit;
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

        currentSpatialViewScreen = null;
        lineRenderer.enabled = false;
    }

    /// <summary>
    /// Redirects the teleportation arc through a spatialView and calculates collisions.
    /// </summary>
    private void RedirectRayThroughSpatialViewArc(SpatialView spatialView, RaycastHit hit)
    {
        if (spatialView == null || spatialView.linkedView == null || lineRenderer == null || arcSegments < 2)
            return;

        Vector3 newOrigin = spatialView.MapPointToLinkedView(hit.point);
        Vector3 exitDir = spatialView.MapDirectionToLinkedView(redirectDir);
        float distanceScale = spatialView.GetMappingScaleTo(spatialView.linkedView);
        float mappedArcSpeed = arcSpeed * distanceScale;
        float mappedMaxArcDistance = maxArcDistance * distanceScale;

        if (mappedArcSpeed <= Mathf.Epsilon)
            return;

        Vector3[] arcPoints = new Vector3[arcSegments];
        for (int i = 0; i < arcSegments; i++)
        {
            float t = i / (float)(arcSegments - 1) * (mappedMaxArcDistance / mappedArcSpeed);
            arcPoints[i] = newOrigin + exitDir * mappedArcSpeed * t + 0.5f * Physics.gravity * t * t;
        }

        int layerMask = ~LayerMask.GetMask("SpatialViewScreen");
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
    /// Performs the teleportation to the selected point through the spatialView.
    /// </summary>
    private void TryTeleport()
    {
        if (tpPoint == Vector3.zero)
            return;

        currentSpatialViewScreen.spatialView.ReleaseTraveller(LinkSpaceManager.Instance.player);
        LinkSpaceManager.Instance.player.transform.SetPositionAndRotation(tpPoint, LinkSpaceManager.Instance.player.transform.rotation);
    }

    #endregion
}
