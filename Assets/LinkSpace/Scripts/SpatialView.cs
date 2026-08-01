using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SpatialView : MonoBehaviour
{
    #region Inspector Variables

    [Header("Main Settings")]
    [Tooltip("The linked spatialView that objects will teleport to.")]
    public SpatialView linkedView;

    [Tooltip("Optional offset applied when teleporting through the spatialView.")]
    public float teleportationOffset = 0.01f;

    [Tooltip("Designates whether this spatialView is an endpoint (inactive visually and physically).")]
    public bool isEndpoint = false;

    [Header("Mapping Scale")]
    [Tooltip("Controls whether linked positions preserve metric scale or are uniformly scaled when mapped to the linked Spatial View.")]
    public MappingScaleMode mappingScaleMode = MappingScaleMode.PreserveScale;

    [Tooltip("Uniform scale used when Mapping Scale Mode is Custom Scale Factor.")]
    [Min(0.0001f)]
    public float customMappingScale = 1f;

    [Tooltip("When enabled, non-player travellers and their visual clones are uniformly scaled in scale-aware mapping modes.")]
    public bool scaleTravellerObjects = true;

    [Header("Spatial View Creation")]
    [Tooltip("Use the mesh as a spatialView screen.")]
    public bool useMeshAsScreen = true;

    [Tooltip("The spatialView screen object.")]
    public SpatialViewScreen screen;

    [Tooltip("Material applied to the spatialView screen.")]
    public Material screenMaterial;

    [Header("Navigation")]
    [Tooltip("If true, objects can walk through this spatialView.")]
    public bool walkable = true;

    [Tooltip("If true, objects can teleport through this spatialView.")]
    public bool teleportable = true;

    [Header("Object Travel")]
    [Tooltip("Mode used for object travel through the spatialView.")]
    public ObjectTravelMode travelMode = ObjectTravelMode.Raycast;

    [Header("Unity Events")]
    [Tooltip("Event that is triggered when an Object travels through the SpatialView")]
    public UnityEvent<SpatialTraveller> onObjectTravel;

    [Header("Recursion")]
    [Tooltip("How many times the spatialView view is rendered recursively. Higher values look better but cost more GPU.")]
    [Range(1, 5)]
    public int maxRecursionDepth = 3;

    [Tooltip("Scales recursive render texture resolution. Lower values improve performance at the cost of portal image sharpness.")]
    [Range(0.25f, 1f)]
    public float recursionTextureScale = 1f;

    [Header("Advanced Settings")]
    [Tooltip("Offset used for near clipping plane calculations.")]
    public float nearClipOffset = 0.05f;

    [Tooltip("Maximum allowed near clip distance.")]
    public float nearClipLimit = 0.2f;

    [Tooltip("World-space margin around the visible screen that still counts as a valid walk-through crossing.")]
    [Min(0f)]
    public float screenTeleportMargin = 0.05f;

    #endregion

    #region Private Variables

    private const int RecursionTextureBufferCount = 2;
    private const float PlaneCrossingEpsilon = 0.0001f;
    private static readonly Quaternion PortalHalfTurn = Quaternion.Euler(0f, 180f, 0f);
    private static readonly int LeftEyeTexId = Shader.PropertyToID("_LeftEyeTex");
    private static readonly int RightEyeTexId = Shader.PropertyToID("_RightEyeTex");
    private static readonly int DisplayMaskId = Shader.PropertyToID("displayMask");

    private Camera viewCam;
    private Camera playerCam;
    private List<SpatialTraveller> trackedTravellers;
    private readonly Dictionary<SpatialTraveller, Vector3> travellerPreviousPositions = new Dictionary<SpatialTraveller, Vector3>();
    private MeshFilter screenMeshFilter;
    private RenderTexture[] viewTexturesLeft;
    private RenderTexture[] viewTexturesRight;
    private int outerTextureSlot = -1;

    [HideInInspector]
    public Collider enterCollider;

    private Vector3 prevViewPos;
    private Quaternion prevViewRot;
    private bool warnedNonUniformMappingScale;

    private struct ScreenRenderState
    {
        public MeshRenderer renderer;
        public Texture leftEyeTexture;
        public Texture rightEyeTexture;
        public int displayMask;
        public ShadowCastingMode shadowCastingMode;
        public bool rendererEnabled;
    }

    #endregion

    #region Enums

    public enum ObjectTravelMode { None, Traversation, Raycast }
    public enum MappingScaleMode { PreserveScale, ScaleByPortalSize, CustomScaleFactor }

    #endregion

    #region Unity Callbacks

    void Awake()
    {
        CreateSpatialViewHierarchy();

        if (viewCam != null)
            viewCam.enabled = false;

        if (isEndpoint)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
                renderer.enabled = false;

            if (screen)
                screen.gameObject.SetActive(false);
        }
        else
        {
            if (screen == null) return;

            playerCam = Camera.main;
            trackedTravellers = new List<SpatialTraveller>();
            screenMeshFilter = screen.GetComponent<MeshFilter>();
            screen.meshRenderer.material.SetInt(DisplayMaskId, 1);
            enterCollider = GetComponent<Collider>();
            screen.spatialView = this;
        }
    }

    void Start()
    {
        prevViewPos = transform.position;
        prevViewRot = transform.rotation;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseViewTextures();
    }

    void LateUpdate()
    {
        HandleTravellerVisuals();
    }

    void FixedUpdate()
    {
        TrackPlayerByTeleportOrigin();
        HandleTravellerTeleportation();
    }

    void OnValidate()
    {
        maxRecursionDepth = Mathf.Max(1, maxRecursionDepth);
        recursionTextureScale = Mathf.Clamp(recursionTextureScale, 0.25f, 1f);
        screenTeleportMargin = Mathf.Max(0f, screenTeleportMargin);
        if (linkedView != null) linkedView.linkedView = this;
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isEndpoint) return;

        SpatialTraveller traveller = GetTraveller(other);
        GrabSpatialTraveller grabTraveller = traveller as GrabSpatialTraveller;

        if (grabTraveller)
        {
            if (grabTraveller.IsPortalTransferActive) return;
            grabTraveller.PrepareTeleportOnRelease(this, linkedView);
        }

        if (traveller) OnTravellerEnterView(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        if (isEndpoint) return;

        SpatialTraveller traveller = GetTraveller(other);

        if (traveller && trackedTravellers.Contains(traveller))
        {
            var grabTraveller = traveller as GrabSpatialTraveller;

            if (grabTraveller && grabTraveller.IsGrabbed() && !grabTraveller.IsPortalTransferActive) return;

            ReleaseTraveller(traveller);
        }
    }

    #endregion

    #region SpatialView Setup
    /// <summary>
    /// Creates spatialView camera and optional spatialView screen.
    /// Configures physics components and screen mesh.
    /// </summary>
    private void CreateSpatialViewHierarchy()
    {
        GameObject camObj = new GameObject("LinkSpaceCamera");
        camObj.transform.parent = transform;
        viewCam = camObj.AddComponent<Camera>();

        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (!useMeshAsScreen) return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
            throw new NullReferenceException("Mesh not found.");

        GameObject spatialViewScreen = new GameObject("SpatialViewScreen");
        spatialViewScreen.transform.parent = transform;
        spatialViewScreen.transform.localRotation = Quaternion.identity;
        spatialViewScreen.transform.localPosition = Vector3.zero;
        spatialViewScreen.transform.localScale = transform.localScale;
        box.size = new Vector3(box.size.x * transform.localScale.x, box.size.y * transform.localScale.y, box.size.z * transform.localScale.z);
        transform.localScale = Vector3.one;

        MeshFilter mf = spatialViewScreen.AddComponent<MeshFilter>();
        mf.mesh = meshFilter.mesh;
        MeshRenderer mr = spatialViewScreen.AddComponent<MeshRenderer>();
        mr.material = screenMaterial;
        MeshCollider mc = spatialViewScreen.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        spatialViewScreen.layer = LayerMask.NameToLayer("SpatialViewScreen");
        screen = spatialViewScreen.AddComponent<SpatialViewScreen>();
        screen.meshRenderer = mr;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = false;
    }

    #endregion

    #region Traveller Management
    /// <summary>
    /// Registers a traveller entering the spatialView.
    /// Initializes previous position for teleportation tracking.
    /// </summary>
    public void OnTravellerEnterView(SpatialTraveller traveller)
    {
        OnTravellerEnterView(traveller, false);
    }

    public void OnTravellerTeleportedIntoView(SpatialTraveller traveller)
    {
        OnTravellerEnterView(traveller, true);
    }

    private void OnTravellerEnterView(SpatialTraveller traveller, bool resetPreviousPosition)
    {
        if (isEndpoint || traveller == null || trackedTravellers == null) return;

        Vector3 currentPosition = traveller.GetPosition();

        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterSpatialViewThreshold(this);
            trackedTravellers.Add(traveller);
            resetPreviousPosition = true;
        }
        else if (resetPreviousPosition)
        {
            traveller.EnterSpatialViewThreshold(this);
        }

        if (resetPreviousPosition)
            SetTravellerPreviousPosition(traveller, currentPosition);
    }
    /// <summary>
    /// Releases a traveller from spatialView tracking.
    /// Calls exit threshold behavior on the traveller.
    /// </summary>
    public void ReleaseTraveller(SpatialTraveller traveller)
    {
        if (isEndpoint || traveller == null) return;

        traveller.ExitSpatialViewThreshold(this);
        trackedTravellers.Remove(traveller);
        travellerPreviousPositions.Remove(traveller);
    }

    #endregion

    #region Traveller Updates
    /// <summary>
    /// Updates the positions and rotations of traveller clones for visual correctness.
    /// </summary>
    private void HandleTravellerVisuals()
    {
        if (linkedView == null || isEndpoint || travelMode == ObjectTravelMode.None) return;

        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            SpatialTraveller traveller = trackedTravellers[i];

            if (!traveller.IsPortalVisualOwner(this))
                continue;

            if (traveller.graphicsClone != null && traveller.graphicsObject != null)
            {
                traveller.graphicsClone.transform.position = MapPointToLinkedView(traveller.graphicsObject.transform.position);
                traveller.graphicsClone.transform.rotation = MapRotationToLinkedView(traveller.graphicsObject.transform.rotation);

                float scale = ShouldScaleTravellerObjects() ? GetMappingScaleTo(linkedView) : 1f;
                traveller.graphicsClone.transform.localScale = traveller.graphicsObject.transform.localScale * scale;
            }

            UpdateSliceParams(traveller);
        }
    }
    /// <summary>
    /// Handles teleportation logic for travellers crossing the spatialView plane.
    /// Updates traveller positions, rotations, and triggers linked spatialView entry.
    /// </summary>
    private void HandleTravellerTeleportation()
    {
        if (linkedView == null || isEndpoint || travelMode == ObjectTravelMode.None)
        {
            prevViewPos = transform.position;
            prevViewRot = transform.rotation;
            return;
        }

        Matrix4x4 prevSpatialViewMatrix = Matrix4x4.TRS(prevViewPos, prevViewRot, Vector3.one);
        Matrix4x4 prevSpatialViewInv = prevSpatialViewMatrix.inverse;

        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            SpatialTraveller traveller = trackedTravellers[i];

            if (traveller == null)
            {
                trackedTravellers.RemoveAt(i);
                i--;
                continue;
            }

            PlayerSpatialTraveller playerTraveller = traveller as PlayerSpatialTraveller;
            Vector3 currentPosition = traveller.GetPosition();

            GrabSpatialTraveller grabTraveller = traveller as GrabSpatialTraveller;

            if (grabTraveller == null || (grabTraveller != null && !grabTraveller.IsGrabbed()))
            {
                if (playerTraveller && !walkable)
                {
                    SetTravellerPreviousPosition(traveller, currentPosition);
                    continue;
                }

                if (travellerPreviousPositions.TryGetValue(traveller, out Vector3 previousPosition) &&
                    TryGetScreenCrossing(previousPosition, currentPosition, prevSpatialViewInv, out int sourceExitSide, out _))
                {
                    Transform travellerT = traveller.transform;
                    Vector3 positionOld = travellerT.position;
                    Quaternion rotationOld = travellerT.rotation;

                    Vector3 newPosition = MapPointToLinkedView(currentPosition);
                    Quaternion mappedRotation = playerTraveller == null
                        ? MapRotationToLinkedView(travellerT.rotation)
                        : MapRotationToLinkedView(playerTraveller.GetTeleportOriginRotation());
                    Quaternion newRotation = playerTraveller == null
                        ? mappedRotation
                        : Quaternion.Euler(rotationOld.eulerAngles.x, mappedRotation.eulerAngles.y, rotationOld.eulerAngles.z);

                    int targetExitSide = -sourceExitSide;
                    Vector3 tpOffset = playerTraveller ? linkedView.transform.forward * teleportationOffset * targetExitSide : Vector3.zero;

                    traveller.Teleport(newPosition + tpOffset, newRotation);

                    if (playerTraveller == null && ShouldScaleTravellerObjects())
                        traveller.ApplyMappingScale(GetMappingScaleTo(linkedView));

                    onObjectTravel.Invoke(traveller);

                    if (traveller.graphicsClone != null)
                        traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotationOld);

                    ReleaseTraveller(traveller);
                    linkedView.OnTravellerEnterView(traveller, true);

                    i--;
                    continue;
                }
            }

            SetTravellerPreviousPosition(traveller, currentPosition);
        }

        prevViewPos = transform.position;
        prevViewRot = transform.rotation;
    }

    /// <summary>
    /// Tracks the player by camera position so simulator/head movement can trigger travel without collider callbacks.
    /// </summary>
    private void TrackPlayerByTeleportOrigin()
    {
        if (linkedView == null || isEndpoint || !walkable || travelMode == ObjectTravelMode.None) return;
        if (LinkSpaceManager.Instance == null || LinkSpaceManager.Instance.player == null) return;

        PlayerSpatialTraveller playerTraveller = LinkSpaceManager.Instance.player;

        if (trackedTravellers.Contains(playerTraveller)) return;
        if (!IsPointInsideEnterCollider(playerTraveller.GetPosition())) return;

        OnTravellerEnterView(playerTraveller);
    }

    /// <summary>
    /// Checks whether a world-space point is inside the spatialView trigger volume.
    /// </summary>
    private bool IsPointInsideEnterCollider(Vector3 worldPoint)
    {
        BoxCollider box = enterCollider as BoxCollider;

        if (box == null)
            box = GetComponent<BoxCollider>();

        if (box == null)
            return false;

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 halfSize = box.size * 0.5f;
        const float tolerance = 0.05f;

        return Mathf.Abs(localPoint.x) <= halfSize.x + tolerance
            && Mathf.Abs(localPoint.y) <= halfSize.y + tolerance
            && Mathf.Abs(localPoint.z) <= halfSize.z + tolerance;
    }

    private SpatialTraveller GetTraveller(Collider other)
    {
        if (other == null)
            return null;

        SpatialTraveller traveller = other.GetComponent<SpatialTraveller>();
        return traveller != null ? traveller : other.GetComponentInParent<SpatialTraveller>();
    }

    /// <summary>
    /// Checks whether the movement segment crosses the visible screen surface.
    /// </summary>
    public bool DidCrossVisibleScreen(Vector3 previousWorldPosition, Vector3 currentWorldPosition)
    {
        return TryGetVisibleScreenCrossing(previousWorldPosition, currentWorldPosition, out _, out _);
    }

    /// <summary>
    /// Checks whether a world-space movement segment crosses the visible screen surface.
    /// </summary>
    public bool TryGetVisibleScreenCrossing(
        Vector3 previousWorldPosition,
        Vector3 currentWorldPosition,
        out int sourceExitSide,
        out Vector3 crossingWorldPosition)
    {
        return TryGetScreenCrossing(
            previousWorldPosition,
            currentWorldPosition,
            transform.worldToLocalMatrix,
            out sourceExitSide,
            out crossingWorldPosition);
    }

    /// <summary>
    /// Checks whether a world-space point is inside the broad tracking trigger volume.
    /// </summary>
    public bool ContainsEnterColliderPoint(Vector3 worldPoint)
    {
        return IsPointInsideEnterCollider(worldPoint);
    }

    /// <summary>
    /// Stores previous traveller positions per spatialView so overlapping triggers cannot corrupt crossing state.
    /// </summary>
    private void SetTravellerPreviousPosition(SpatialTraveller traveller, Vector3 position)
    {
        traveller.previousPosition = position;
        travellerPreviousPositions[traveller] = position;
    }

    /// <summary>
    /// Finds a valid crossing through this spatialView's visible screen, not just its broad trigger volume.
    /// </summary>
    private bool TryGetScreenCrossing(
        Vector3 previousWorldPosition,
        Vector3 currentWorldPosition,
        Matrix4x4 previousViewInv,
        out int sourceExitSide,
        out Vector3 crossingWorldPosition)
    {
        sourceExitSide = 0;
        crossingWorldPosition = currentWorldPosition;

        Vector3 previousLocalPosition = previousViewInv.MultiplyPoint3x4(previousWorldPosition);
        Vector3 currentLocalPosition = transform.InverseTransformPoint(currentWorldPosition);

        if (!TryGetPlaneExitSide(previousLocalPosition.z, currentLocalPosition.z, out sourceExitSide))
            return false;

        crossingWorldPosition = GetPlaneCrossingPoint(
            previousWorldPosition,
            currentWorldPosition,
            previousLocalPosition.z,
            currentLocalPosition.z);

        return IsPointInsideScreenBounds(crossingWorldPosition);
    }

    /// <summary>
    /// Returns the side reached after crossing the source portal plane.
    /// </summary>
    private bool TryGetPlaneExitSide(float previousLocalZ, float currentLocalZ, out int sourceExitSide)
    {
        sourceExitSide = 0;

        if (previousLocalZ > PlaneCrossingEpsilon && currentLocalZ <= PlaneCrossingEpsilon)
        {
            sourceExitSide = -1;
            return true;
        }

        if (previousLocalZ < -PlaneCrossingEpsilon && currentLocalZ >= -PlaneCrossingEpsilon)
        {
            sourceExitSide = 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Interpolates the movement segment to the point where it crosses the spatialView plane.
    /// </summary>
    private Vector3 GetPlaneCrossingPoint(
        Vector3 previousWorldPosition,
        Vector3 currentWorldPosition,
        float previousLocalZ,
        float currentLocalZ)
    {
        float denominator = previousLocalZ - currentLocalZ;

        if (Mathf.Abs(denominator) <= PlaneCrossingEpsilon)
            return currentWorldPosition;

        float t = Mathf.Clamp01(previousLocalZ / denominator);
        return Vector3.Lerp(previousWorldPosition, currentWorldPosition, t);
    }

    /// <summary>
    /// Checks the two largest local axes of the screen bounds so thin BoxColliders can be used as screens.
    /// </summary>
    private bool IsPointInsideScreenBounds(Vector3 worldPoint)
    {
        if (screen == null)
            return false;

        BoxCollider screenBox = screen.GetComponent<BoxCollider>();
        if (screenBox != null)
            return IsPointInsideLocalSurfaceBounds(worldPoint, screenBox.transform, screenBox.center, screenBox.size);

        Renderer renderer = screen.meshRenderer;
        if (renderer != null)
            return IsPointInsideLocalSurfaceBounds(worldPoint, renderer.transform, renderer.localBounds.center, renderer.localBounds.size);

        MeshFilter meshFilter = screen.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            return IsPointInsideLocalSurfaceBounds(worldPoint, meshFilter.transform, meshFilter.sharedMesh.bounds.center, meshFilter.sharedMesh.bounds.size);

        return false;
    }

    private bool IsPointInsideLocalSurfaceBounds(Vector3 worldPoint, Transform boundsTransform, Vector3 localCenter, Vector3 localSize)
    {
        Vector3 localPoint = boundsTransform.InverseTransformPoint(worldPoint) - localCenter;
        Vector3 halfSize = localSize * 0.5f;
        Vector3 worldSize = new Vector3(
            Mathf.Abs(localSize.x * boundsTransform.lossyScale.x),
            Mathf.Abs(localSize.y * boundsTransform.lossyScale.y),
            Mathf.Abs(localSize.z * boundsTransform.lossyScale.z));
        int thicknessAxis = SmallestAxis(worldSize);

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == thicknessAxis)
                continue;

            float margin = WorldMarginToLocalAxis(boundsTransform, axis);
            if (Mathf.Abs(GetAxis(localPoint, axis)) > GetAxis(halfSize, axis) + margin)
                return false;
        }

        return true;
    }

    private float WorldMarginToLocalAxis(Transform targetTransform, int axis)
    {
        Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float worldAxisLength = targetTransform.TransformVector(localAxis).magnitude;
        return worldAxisLength > Mathf.Epsilon ? screenTeleportMargin / worldAxisLength : screenTeleportMargin;
    }

    private static int SmallestAxis(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z)
            return 0;

        return size.y <= size.z ? 1 : 2;
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;
            case 1:
                return value.y;
            default:
                return value.z;
        }
    }

    #endregion

    #region Rendering
    /// <summary>
    /// Callback triggered before camera rendering.
    /// Renders spatialView view for the player's camera.
    /// </summary>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (isEndpoint) return;

        if (playerCam == null)
            playerCam = Camera.main;

        if (camera != playerCam) return;
        RenderSpatialView(camera);
    }
    /// <summary>
    /// Handles rendering of the spatialView for stereo XR and mono Game View cameras.
    /// </summary>
    void RenderSpatialView(Camera mainCam)
    {
        if (screen == null || screen.meshRenderer == null) return;

        if (!linkedView || linkedView == this)
        {
            outerTextureSlot = -1;
            SetScreenFallback();
            return;
        }

        CreateViewTextures();
        if (!HasViewTextures()) return;

        ScreenRenderState linkedScreenState = linkedView.CaptureScreenState();
        linkedView.HideScreenForViewRender();

        try
        {
            if (mainCam.stereoEnabled)
                RenderRecursiveStereo(mainCam);
            else
                RenderRecursiveMono(mainCam);
        }
        finally
        {
            linkedView.RestoreScreenState(linkedScreenState);
            ResetViewCamera(mainCam);
        }
    }
    /// <summary>
    /// Renders stereo recursion with two ping-pong texture pairs.
    /// </summary>
    private void RenderRecursiveStereo(Camera mainCam)
    {
        int previousSlot = -1;

        for (int level = GetRecursionDepth() - 1; level >= 0; level--)
        {
            int renderSlot = GetTextureSlot(level);
            int steps = level + 1;

            if (previousSlot < 0)
                SetScreenFallback();
            else
                SetScreenTexture(viewTexturesLeft[previousSlot], viewTexturesLeft[previousSlot]);

            RenderEye(Camera.StereoscopicEye.Left, viewTexturesLeft[renderSlot], mainCam, steps);

            if (previousSlot < 0)
                SetScreenFallback();
            else
                SetScreenTexture(viewTexturesRight[previousSlot], viewTexturesRight[previousSlot]);

            RenderEye(Camera.StereoscopicEye.Right, viewTexturesRight[renderSlot], mainCam, steps);

            previousSlot = renderSlot;
        }

        outerTextureSlot = previousSlot;
        RestoreScreenTextures();
    }
    /// <summary>
    /// Renders mono recursion with the left ping-pong buffer and mirrors it to both material eyes.
    /// </summary>
    private void RenderRecursiveMono(Camera mainCam)
    {
        int previousSlot = -1;

        for (int level = GetRecursionDepth() - 1; level >= 0; level--)
        {
            int renderSlot = GetTextureSlot(level);
            int steps = level + 1;

            if (previousSlot < 0)
                SetScreenFallback();
            else
                SetScreenTexture(viewTexturesLeft[previousSlot], viewTexturesLeft[previousSlot]);

            RenderMono(viewTexturesLeft[renderSlot], mainCam, steps);
            previousSlot = renderSlot;
        }

        outerTextureSlot = previousSlot;
        RestoreScreenTextures();
    }
    /// <summary>
    /// Creates or resizes the two ping-pong texture pairs used by all recursion depths.
    /// </summary>
    private void CreateViewTextures()
    {
        if (isEndpoint || screen == null) return;

        int width = XRSettings.eyeTextureWidth;
        int height = XRSettings.eyeTextureHeight;

        if (width <= 0 || height <= 0)
        {
            width = Mathf.Max(1, Screen.width);
            height = Mathf.Max(1, Screen.height);
        }

        width = Mathf.Max(1, Mathf.RoundToInt(width * recursionTextureScale));
        height = Mathf.Max(1, Mathf.RoundToInt(height * recursionTextureScale));

        if (viewTexturesLeft == null || viewTexturesLeft.Length != RecursionTextureBufferCount)
        {
            ReleaseViewTextures();
            viewTexturesLeft = new RenderTexture[RecursionTextureBufferCount];
            viewTexturesRight = new RenderTexture[RecursionTextureBufferCount];
        }

        for (int i = 0; i < RecursionTextureBufferCount; i++)
        {
            viewTexturesLeft[i] = CreateOrResizeViewTexture(viewTexturesLeft[i], width, height, $"LinkSpace View Texture Left {i + 1}");
            viewTexturesRight[i] = CreateOrResizeViewTexture(viewTexturesRight[i], width, height, $"LinkSpace View Texture Right {i + 1}");
        }
    }

    /// <summary>
    /// Creates or updates a single render texture.
    /// </summary>
    private RenderTexture CreateOrResizeViewTexture(RenderTexture renderTexture, int width, int height, string textureName)
    {
        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
            return renderTexture;

        if (renderTexture != null)
            ReleaseViewTexture(renderTexture);

        renderTexture = new RenderTexture(width, height, 24)
        {
            name = textureName,
            antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
            useMipMap = false,
            autoGenerateMips = false
        };
        renderTexture.Create();
        return renderTexture;
    }

    /// <summary>
    /// Release all recursion render textures.
    /// </summary>
    private void ReleaseViewTextures()
    {
        if (viewTexturesLeft != null)
        {
            foreach (RenderTexture renderTexture in viewTexturesLeft)
                ReleaseViewTexture(renderTexture);
        }

        if (viewTexturesRight != null)
        {
            foreach (RenderTexture renderTexture in viewTexturesRight)
                ReleaseViewTexture(renderTexture);
        }

        viewTexturesLeft = null;
        viewTexturesRight = null;
        outerTextureSlot = -1;
    }
    /// <summary>
    /// Releases a single render texture if it exists.
    /// </summary>
    private void ReleaseViewTexture(RenderTexture renderTexture)
    {
        if (renderTexture == null) return;

        renderTexture.Release();
        Destroy(renderTexture);
    }
    /// <summary>
    /// Sets this spatialView's screen to display the given textures.
    /// </summary>
    public void SetScreenTexture(Texture left, Texture right)
    {
        if (screen == null || screen.meshRenderer == null || isEndpoint) return;

        Material material = screen.meshRenderer.material;
        material.SetTexture(LeftEyeTexId, left != null ? left : Texture2D.blackTexture);
        material.SetTexture(RightEyeTexId, right != null ? right : Texture2D.blackTexture);
        material.SetInt(DisplayMaskId, 1);
        screen.meshRenderer.enabled = true;
        screen.meshRenderer.shadowCastingMode = ShadowCastingMode.On;
    }
    /// <summary>
    /// Shows the inactive screen color without sampling any recursion render texture.
    /// </summary>
    public void SetScreenFallback()
    {
        if (screen == null || screen.meshRenderer == null || isEndpoint) return;

        Material material = screen.meshRenderer.material;
        material.SetTexture(LeftEyeTexId, Texture2D.blackTexture);
        material.SetTexture(RightEyeTexId, Texture2D.blackTexture);
        material.SetInt(DisplayMaskId, 0);
        screen.meshRenderer.enabled = true;
        screen.meshRenderer.shadowCastingMode = ShadowCastingMode.On;
    }
    /// <summary>
    /// Restores this spatialView's screen to its own outermost rendered texture.
    /// </summary>
    public void RestoreScreenTextures()
    {
        if (!HasViewTextures() || screen == null || isEndpoint || outerTextureSlot < 0) return;

        SetScreenTexture(viewTexturesLeft[outerTextureSlot], viewTexturesRight[outerTextureSlot]);
    }
    /// <summary>
    /// Captures the current screen material and renderer state.
    /// </summary>
    private ScreenRenderState CaptureScreenState()
    {
        ScreenRenderState state = new ScreenRenderState();

        if (screen == null || screen.meshRenderer == null)
            return state;

        Material material = screen.meshRenderer.material;
        state.renderer = screen.meshRenderer;
        state.leftEyeTexture = material.GetTexture(LeftEyeTexId);
        state.rightEyeTexture = material.GetTexture(RightEyeTexId);
        state.displayMask = material.GetInt(DisplayMaskId);
        state.shadowCastingMode = screen.meshRenderer.shadowCastingMode;
        state.rendererEnabled = screen.meshRenderer.enabled;
        return state;
    }
    /// <summary>
    /// Restores a previously captured screen material and renderer state.
    /// </summary>
    private void RestoreScreenState(ScreenRenderState state)
    {
        if (state.renderer == null) return;

        Material material = state.renderer.material;
        material.SetTexture(LeftEyeTexId, state.leftEyeTexture);
        material.SetTexture(RightEyeTexId, state.rightEyeTexture);
        material.SetInt(DisplayMaskId, state.displayMask);
        state.renderer.shadowCastingMode = state.shadowCastingMode;
        state.renderer.enabled = state.rendererEnabled;
    }
    /// <summary>
    /// Hides this screen as geometry while another spatialView renders through it.
    /// </summary>
    private void HideScreenForViewRender()
    {
        if (screen == null || screen.meshRenderer == null || isEndpoint) return;

        screen.meshRenderer.enabled = false;
    }
    /// <summary>
    /// Renders a single eye of the spatialView view into the given render texture.
    /// </summary>
    private void RenderEye(Camera.StereoscopicEye eye, RenderTexture target, Camera mainCam, int steps)
    {
        if (!linkedView || target == null) return;

        CopyCameraSettings(mainCam);
        viewCam.projectionMatrix = mainCam.GetStereoProjectionMatrix(eye);
        viewCam.targetTexture = target;

        Vector3 eyeWorldPos = mainCam.GetStereoViewMatrix(eye).inverse.MultiplyPoint(Vector3.zero);
        Quaternion eyeWorldRot = mainCam.transform.rotation;

        RenderFromVirtualView(target, mainCam, eyeWorldPos, eyeWorldRot, steps);
    }
    /// <summary>
    /// Renders the spatialView from a regular non-XR camera.
    /// </summary>
    private void RenderMono(RenderTexture target, Camera mainCam, int steps)
    {
        if (!linkedView || target == null) return;

        CopyCameraSettings(mainCam);
        viewCam.projectionMatrix = mainCam.projectionMatrix;
        viewCam.targetTexture = target;

        RenderFromVirtualView(target, mainCam, mainCam.transform.position, mainCam.transform.rotation, steps);
    }
    /// <summary>
    /// Positions the virtual camera, clears the target and renders one recursion level.
    /// </summary>
    private void RenderFromVirtualView(RenderTexture target, Camera mainCam, Vector3 sourcePosition, Quaternion sourceRotation, int steps)
    {
        PositionViewCamera(linkedView, sourcePosition, sourceRotation, steps);
        ClearRenderTexture(target, mainCam.backgroundColor);
        SetNearClipPlaneVR(linkedView.transform);
        viewCam.Render();
    }
    /// <summary>
    /// Copies render settings that should match the player camera for portal renders.
    /// </summary>
    private void CopyCameraSettings(Camera source)
    {
        viewCam.CopyFrom(source);
        viewCam.enabled = false;
        viewCam.forceIntoRenderTexture = true;
        viewCam.stereoTargetEye = StereoTargetEyeMask.None;

        if (viewCam.clearFlags == CameraClearFlags.Nothing || viewCam.clearFlags == CameraClearFlags.Depth)
            viewCam.clearFlags = CameraClearFlags.SolidColor;
    }
    /// <summary>
    /// Positions the spatialView camera relative to the linked spatialView.
    /// </summary>
    private void PositionViewCamera(SpatialView targetView, Vector3 sourcePosition, Quaternion sourceRotation, int steps)
    {
        Vector3 viewPosition = sourcePosition;
        Quaternion viewRotation = sourceRotation;
        int depth = Mathf.Max(1, steps);

        for (int i = 0; i < depth; i++)
        {
            viewPosition = MapPointTo(targetView, viewPosition);
            viewRotation = MapRotationTo(targetView, viewRotation);
        }

        viewCam.transform.SetPositionAndRotation(viewPosition, viewRotation);
    }
    /// <summary>
    /// Clears temporary render state from the spatialView camera.
    /// </summary>
    private void ResetViewCamera(Camera mainCam)
    {
        if (viewCam == null) return;

        viewCam.targetTexture = null;
        viewCam.projectionMatrix = mainCam.projectionMatrix;
        viewCam.ResetProjectionMatrix();
    }
    /// <summary>
    /// Clears a render texture with an optional background color.
    /// </summary>
    private void ClearRenderTexture(RenderTexture renderTexture, Color? backGroundColor = null)
    {
        if (renderTexture == null) return;
        Color colToUse = backGroundColor ?? Color.black;

        RenderTexture oldRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, colToUse);
        RenderTexture.active = oldRT;
    }
    /// <summary>
    /// Returns true when both eye texture arrays contain the ping-pong buffers.
    /// </summary>
    private bool HasViewTextures()
    {
        return viewTexturesLeft != null
            && viewTexturesRight != null
            && viewTexturesLeft.Length == RecursionTextureBufferCount
            && viewTexturesRight.Length == RecursionTextureBufferCount
            && viewTexturesLeft[0] != null
            && viewTexturesRight[0] != null
            && viewTexturesLeft[1] != null
            && viewTexturesRight[1] != null;
    }
    /// <summary>
    /// Returns the active recursion depth, clamped to a valid runtime value.
    /// </summary>
    private int GetRecursionDepth()
    {
        return Mathf.Max(1, maxRecursionDepth);
    }
    /// <summary>
    /// Returns the ping-pong render slot used for a recursion level.
    /// </summary>
    private int GetTextureSlot(int recursionLevel)
    {
        return Mathf.Abs(recursionLevel) % RecursionTextureBufferCount;
    }
    /// <summary>
    /// Adjusts the spatialView camera's near clip plane to avoid clipping geometry.
    /// </summary>
    private void SetNearClipPlaneVR(Transform clipPlaneView)
    {
        float dot = Mathf.Sign(Vector3.Dot(clipPlaneView.forward, clipPlaneView.position - viewCam.transform.position));

        Vector3 camSpacePos = viewCam.worldToCameraMatrix.MultiplyPoint(clipPlaneView.position);
        Vector3 camSpaceNormal = viewCam.worldToCameraMatrix.MultiplyVector(clipPlaneView.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            viewCam.projectionMatrix = viewCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
    }

    #endregion

    #region Slice Updates
    /// <summary>
    /// Updates slicing parameters for a traveller and its clone to create spatialView clipping effects.
    /// </summary>
    void UpdateSliceParams(SpatialTraveller traveller)
    {
        if (traveller == null || traveller.graphicsObject == null) return;
        if (!traveller.IsPortalVisualOwner(this)) return;
        if (traveller.originalMaterials == null || traveller.cloneMaterials == null) return;

        int side = SideOfView(traveller.GetPosition());
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedView.transform.position;
        int cloneSide = -side;
        Vector3 cloneSliceNormal = linkedView.transform.forward * -cloneSide;

        float sliceOffsetDst = 0.025f;
        float cloneSliceOffsetDst = 0.025f;

        GrabSpatialTraveller grabSpatialTraveller = traveller as GrabSpatialTraveller;

        if (grabSpatialTraveller && grabSpatialTraveller.IsGrabbed()
            && !SameSideOfView(playerCam.transform.position, traveller.transform.position)
            && travelMode == ObjectTravelMode.Raycast)
        {
            sliceNormal = -sliceNormal;
            cloneSliceNormal = -cloneSliceNormal;
        }

        if (travelMode == ObjectTravelMode.Traversation
            && grabSpatialTraveller && grabSpatialTraveller.IsGrabbed()
            && grabSpatialTraveller.markedForTeleport)
        {
            sliceNormal = -sliceNormal;
            cloneSliceNormal = -cloneSliceNormal;
        }

        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            traveller.originalMaterials[i].SetVector("sliceCentre", slicePos);
            traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);
            traveller.originalMaterials[i].SetFloat("sliceOffsetDst", sliceOffsetDst);

            traveller.cloneMaterials[i].SetVector("sliceCentre", cloneSlicePos);
            traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
            traveller.cloneMaterials[i].SetFloat("sliceOffsetDst", cloneSliceOffsetDst);
        }
    }

    #endregion

    #region Utility Methods
    /// <summary>
    /// Returns the side of the spatialView for a given position (front or back).
    /// </summary>
    int SideOfView(Vector3 pos)
    {
        float side = Vector3.Dot(pos - transform.position, transform.forward);

        if (side > PlaneCrossingEpsilon)
            return 1;

        if (side < -PlaneCrossingEpsilon)
            return -1;

        return 0;
    }
    /// <summary>
    /// Checks if two positions are on the same side of the spatialView.
    /// </summary>
    public bool SameSideOfView(Vector3 posA, Vector3 posB) => SideOfView(posA) == SideOfView(posB);

    public float GetMappingScaleTo(SpatialView targetView)
    {
        if (targetView == null)
            return 1f;

        switch (mappingScaleMode)
        {
            case MappingScaleMode.ScaleByPortalSize:
                float sourceSize = GetUniformPortalSize();
                float targetSize = targetView.GetUniformPortalSize();
                WarnIfNonUniformScale(targetView.GetPortalLossyScale(), targetView.GetPortalScaleTransform());
                return sourceSize > Mathf.Epsilon ? targetSize / sourceSize : 1f;
            case MappingScaleMode.CustomScaleFactor:
                return Mathf.Max(0.0001f, customMappingScale);
            default:
                return 1f;
        }
    }

    public bool ShouldScaleTravellerObjects()
    {
        return scaleTravellerObjects && !Mathf.Approximately(GetMappingScaleTo(linkedView), 1f);
    }

    public Vector3 MapPointToLinkedView(Vector3 worldPoint)
    {
        return MapPointTo(linkedView, worldPoint);
    }

    public Vector3 MapPointTo(SpatialView targetView, Vector3 worldPoint)
    {
        if (targetView == null)
            return worldPoint;

        Vector3 localPoint = Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
        Vector3 mappedLocalPoint = PortalHalfTurn * (localPoint * GetMappingScaleTo(targetView));
        return targetView.transform.position + targetView.transform.rotation * mappedLocalPoint;
    }

    public Vector3 MapLinkedPointToSource(Vector3 linkedWorldPoint)
    {
        if (linkedView == null)
            return linkedWorldPoint;

        float scale = GetMappingScaleTo(linkedView);
        Vector3 linkedLocalPoint = Quaternion.Inverse(linkedView.transform.rotation) * (linkedWorldPoint - linkedView.transform.position);
        Vector3 sourceLocalPoint = PortalHalfTurn * linkedLocalPoint;
        return transform.position + transform.rotation * (sourceLocalPoint / Mathf.Max(0.0001f, scale));
    }

    public Quaternion MapRotationToLinkedView(Quaternion worldRotation)
    {
        return MapRotationTo(linkedView, worldRotation);
    }

    public Quaternion MapRotationTo(SpatialView targetView, Quaternion worldRotation)
    {
        if (targetView == null)
            return worldRotation;

        Quaternion localRotation = Quaternion.Inverse(transform.rotation) * worldRotation;
        return targetView.transform.rotation * PortalHalfTurn * localRotation;
    }

    public Vector3 MapDirectionToLinkedView(Vector3 worldDirection)
    {
        return MapDirectionTo(linkedView, worldDirection);
    }

    public Vector3 MapDirectionTo(SpatialView targetView, Vector3 worldDirection)
    {
        if (targetView == null)
            return worldDirection.normalized;

        Vector3 localDirection = Quaternion.Inverse(transform.rotation) * worldDirection;
        return (targetView.transform.rotation * (PortalHalfTurn * localDirection)).normalized;
    }

    public Vector3 MapLinearVelocityTo(SpatialView targetView, Vector3 velocity)
    {
        if (targetView == null)
            return velocity;

        Vector3 localVelocity = Quaternion.Inverse(transform.rotation) * velocity;
        return targetView.transform.rotation * (PortalHalfTurn * (localVelocity * GetMappingScaleTo(targetView)));
    }

    public Vector3 MapAngularVelocityTo(SpatialView targetView, Vector3 angularVelocity)
    {
        if (targetView == null)
            return angularVelocity;

        Vector3 localAngularVelocity = Quaternion.Inverse(transform.rotation) * angularVelocity;
        return targetView.transform.rotation * (PortalHalfTurn * localAngularVelocity);
    }

    private float GetUniformPortalSize()
    {
        Renderer renderer = screen != null ? screen.meshRenderer : null;

        if (renderer != null)
        {
            Bounds localBounds = renderer.localBounds;
            Vector3 lossyScale = GetPortalLossyScale();
            WarnIfNonUniformScale(lossyScale, GetPortalScaleTransform());

            float width = Mathf.Abs(localBounds.size.x * lossyScale.x);
            float height = Mathf.Abs(localBounds.size.y * lossyScale.y);
            float area = Mathf.Max(0.0001f, width * height);
            return Mathf.Sqrt(area);
        }

        Vector3 fallbackScale = GetPortalLossyScale();
        WarnIfNonUniformScale(fallbackScale, GetPortalScaleTransform());
        return Mathf.Max(0.0001f, (Mathf.Abs(fallbackScale.x) + Mathf.Abs(fallbackScale.y) + Mathf.Abs(fallbackScale.z)) / 3f);
    }

    private Transform GetPortalScaleTransform()
    {
        return screen != null ? screen.transform : transform;
    }

    private Vector3 GetPortalLossyScale()
    {
        return GetPortalScaleTransform().lossyScale;
    }

    private void WarnIfNonUniformScale(Vector3 scale, Transform scaleTransform)
    {
        if (mappingScaleMode != MappingScaleMode.ScaleByPortalSize || warnedNonUniformMappingScale)
            return;

        float x = Mathf.Abs(scale.x);
        float y = Mathf.Abs(scale.y);
        float z = Mathf.Abs(scale.z);
        bool nonUniform = !Mathf.Approximately(x, y) || !Mathf.Approximately(x, z);

        if (!nonUniform)
            return;

        Debug.LogWarning(
            $"SpatialView '{name}' uses Scale By Portal Size with non-uniform scale on '{scaleTransform.name}'. LinkSpace uses a single uniform portal-size factor for mapping.",
            this);
        warnedNonUniformMappingScale = true;
    }

    /// <summary>
    /// Links a SpatialView to this spatialView.
    /// </summary>
    public void Link(SpatialView spatialViewToLink)
    {
        linkedView = spatialViewToLink;
        spatialViewToLink.linkedView = this;
    }
    /// <summary>
    /// Unlinks a SpatialView from this spatialView.
    /// </summary>
    public void Unlink()
    {
        if (!linkedView) return;
        if (linkedView.linkedView) linkedView.linkedView = null;
        linkedView = null;
    }

    #endregion
}
