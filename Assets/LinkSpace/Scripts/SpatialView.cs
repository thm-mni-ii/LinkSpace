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
    [Header("Advanced Settings")]
    [Tooltip("Offset used for near clipping plane calculations.")]
    public float nearClipOffset = 0.05f;

    [Tooltip("Maximum allowed near clip distance.")]
    public float nearClipLimit = 0.2f;

    #endregion

    #region Private Variables

    private Camera viewCam;
    private Camera playerCam;
    private List<SpatialTraveller> trackedTravellers;
    private MeshFilter screenMeshFilter;
    private RenderTexture viewTextureLeft;
    private RenderTexture viewTextureRight;

    [HideInInspector]
    public Collider enterCollider;

    private Vector3 prevViewPos;
    private Quaternion prevViewRot;
    private bool warnedNonUniformMappingScale;

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
            screen.meshRenderer.material.SetInt("displayMask", 1);
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

        var traveller = other.GetComponent<SpatialTraveller>();
        var grabTraveller = other.GetComponent<GrabSpatialTraveller>();

        if (grabTraveller)
        {
            if (grabTraveller.IsGrabbed() && travelMode == ObjectTravelMode.Raycast) return;
            grabTraveller.PrepareTeleportOnRelease(this, linkedView);
        }

        if (traveller) OnTravellerEnterView(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        if (isEndpoint) return;

        var traveller = other.GetComponent<SpatialTraveller>();

        if (traveller && trackedTravellers.Contains(traveller))
        {
            var grabTraveller = traveller as GrabSpatialTraveller;

            if (grabTraveller && grabTraveller.IsGrabbed()) return;

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
        if (isEndpoint) return;

        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterSpatialViewThreshold(this);
            traveller.previousPosition = traveller.GetPosition();
            trackedTravellers.Add(traveller);
        }
    }
    /// <summary>
    /// Releases a traveller from spatialView tracking.
    /// Calls exit threshold behavior on the traveller.
    /// </summary>
    public void ReleaseTraveller(SpatialTraveller traveller)
    {
        if (isEndpoint) return;

        traveller.ExitSpatialViewThreshold();
        trackedTravellers.Remove(traveller);
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
        if (linkedView == null || isEndpoint || travelMode == ObjectTravelMode.None) return;

        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            SpatialTraveller traveller = trackedTravellers[i];
            PlayerSpatialTraveller playerTraveller = traveller as PlayerSpatialTraveller;

            Vector3 localPos = transform.InverseTransformPoint(traveller.GetPosition());

            Matrix4x4 prevSpatialViewMatrix = Matrix4x4.TRS(prevViewPos, prevViewRot, Vector3.one);
            Matrix4x4 prevSpatialViewInv = prevSpatialViewMatrix.inverse;

            Vector3 localPrevPos = prevSpatialViewInv.MultiplyPoint3x4(traveller.previousPosition);

            int viewSide = Math.Sign(localPos.z);
            int viewSideOld = Math.Sign(localPrevPos.z);

            GrabSpatialTraveller grabTraveller = traveller as GrabSpatialTraveller;

            if (grabTraveller == null || (grabTraveller != null && !grabTraveller.IsGrabbed()))
            {
                if (playerTraveller && !walkable) return;

                if (viewSide != viewSideOld)
                {
                    Transform travellerT = traveller.transform;
                    Vector3 positionOld = travellerT.position;
                    Quaternion rotationOld = travellerT.rotation;

                    Vector3 newPosition = MapPointToLinkedView(traveller.GetPosition());
                    Quaternion mappedRotation = playerTraveller == null
                        ? MapRotationToLinkedView(travellerT.rotation)
                        : MapRotationToLinkedView(playerTraveller.GetTeleportOriginRotation());
                    Quaternion newRotation = playerTraveller == null
                        ? mappedRotation
                        : Quaternion.Euler(rotationOld.eulerAngles.x, mappedRotation.eulerAngles.y, rotationOld.eulerAngles.z);

                    Vector3 tpOffset = playerTraveller ? linkedView.transform.forward * teleportationOffset * viewSide : Vector3.zero;

                    traveller.Teleport(newPosition + tpOffset, newRotation);

                    if (playerTraveller == null && ShouldScaleTravellerObjects())
                        traveller.ApplyMappingScale(GetMappingScaleTo(linkedView));

                    onObjectTravel.Invoke(traveller);

                    if (traveller.graphicsClone != null)
                        traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotationOld);

                    ReleaseTraveller(traveller);
                    linkedView.OnTravellerEnterView(traveller);

                    i--;
                    continue;
                }
            }

            traveller.previousPosition = traveller.GetPosition();
            prevViewPos = transform.position;
            prevViewRot = transform.rotation;
        }
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
        CreateViewTextures();

        if (!linkedView)
        {
            ClearRenderTexture(viewTextureLeft);
            ClearRenderTexture(viewTextureRight);
            return;
        }

        if (!mainCam.stereoEnabled)
        {
            RenderMono(viewTextureLeft, mainCam);
            RenderMono(viewTextureRight, mainCam);
            ResetViewCamera(mainCam);
            return;
        }

        RenderEye(Camera.StereoscopicEye.Left, viewTextureLeft, mainCam);
        RenderEye(Camera.StereoscopicEye.Right, viewTextureRight, mainCam);

        ResetViewCamera(mainCam);
    }
    /// <summary>
    /// Creates or updates render textures for spatialView camera eyes.
    /// </summary>
    private void CreateViewTextures()
    {
        if (isEndpoint || screen == null) return;

        int width = XRSettings.eyeTextureWidth;
        int height = XRSettings.eyeTextureHeight;

        // Fallback, falls XR noch nicht aktiv ist
        if (width <= 0 || height <= 0)
        {
            width = Mathf.Max(1, Screen.width);
            height = Mathf.Max(1, Screen.height);
        }

        if (viewTextureLeft == null || viewTextureLeft.width != width || viewTextureLeft.height != height)
        {
            if (viewTextureLeft != null)
            {
                viewTextureLeft.Release();
                Destroy(viewTextureLeft);
            }

            viewTextureLeft = new RenderTexture(width, height, 24)
            {
                name = "LinkSpace View Texture Left",
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
            };
            viewTextureLeft.Create();
        }

        if (viewTextureRight == null || viewTextureRight.width != width || viewTextureRight.height != height)
        {
            if (viewTextureRight != null)
            {
                viewTextureRight.Release();
                Destroy(viewTextureRight);
            }

            viewTextureRight = new RenderTexture(width, height, 24)
            {
                name = "LinkSpace View Texture Right",
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
            };
            viewTextureRight.Create();
        }

        screen.meshRenderer.material.SetTexture("_LeftEyeTex", viewTextureLeft);
        screen.meshRenderer.material.SetTexture("_RightEyeTex", viewTextureRight);
    }

    /// <summary>
    /// Release view textures.
    /// </summary>
    private void ReleaseViewTextures()
    {
        if (viewTextureLeft != null)
        {
            viewTextureLeft.Release();
            Destroy(viewTextureLeft);
            viewTextureLeft = null;
        }
        if (viewTextureRight != null)
        {
            viewTextureRight.Release();
            Destroy(viewTextureRight);
            viewTextureRight = null;
        }
    }
    /// <summary>
    /// Renders a single eye of the spatialView view into the given render texture.
    /// </summary>
    private void RenderEye(Camera.StereoscopicEye eye, RenderTexture target, Camera mainCam)
    {
        if (!linkedView || target == null) return;

        CopyCameraSettings(mainCam);
        viewCam.projectionMatrix = mainCam.GetStereoProjectionMatrix(eye);
        viewCam.targetTexture = target;

        Vector3 eyeWorldPos = mainCam.GetStereoViewMatrix(eye).inverse.MultiplyPoint(Vector3.zero);
        Quaternion eyeWorldRot = mainCam.transform.rotation;

        PositionViewCamera(linkedView, eyeWorldPos, eyeWorldRot);

        SetLinkedViewVisible(false);
        SetNearClipPlaneVR(linkedView.transform);
        viewCam.Render();
        SetLinkedViewVisible(true);
    }
    /// <summary>
    /// Renders the spatialView from a regular non-XR camera.
    /// </summary>
    private void RenderMono(RenderTexture target, Camera mainCam)
    {
        if (!linkedView || target == null) return;

        CopyCameraSettings(mainCam);
        viewCam.projectionMatrix = mainCam.projectionMatrix;
        viewCam.targetTexture = target;

        PositionViewCamera(linkedView, mainCam.transform.position, mainCam.transform.rotation);

        SetLinkedViewVisible(false);
        SetNearClipPlaneVR(linkedView.transform);
        viewCam.Render();
        SetLinkedViewVisible(true);
    }
    /// <summary>
    /// Copies render settings that should match the player camera for portal renders.
    /// </summary>
    private void CopyCameraSettings(Camera source)
    {
        viewCam.CopyFrom(source);
        viewCam.enabled = false;
    }
    /// <summary>
    /// Positions the spatialView camera relative to the linked spatialView.
    /// </summary>
    private void PositionViewCamera(SpatialView targetView, Vector3 sourcePosition, Quaternion sourceRotation)
    {
        viewCam.transform.position = MapPointTo(targetView, sourcePosition);
        viewCam.transform.rotation = MapRotationTo(targetView, sourceRotation);
    }
    /// <summary>
    /// Clears temporary render state from the spatialView camera.
    /// </summary>
    private void ResetViewCamera(Camera mainCam)
    {
        viewCam.targetTexture = null;
        viewCam.projectionMatrix = mainCam.projectionMatrix;
        viewCam.ResetProjectionMatrix();
    }
    /// <summary>
    /// Toggles visibility of the linked spatialView screen for rendering.
    /// </summary>
    private void SetLinkedViewVisible(bool visible)
    {
        if (linkedView.screen == null || linkedView.isEndpoint) return;

        linkedView.screen.meshRenderer.material.SetInt("displayMask", visible ? 1 : 0);
        linkedView.screen.meshRenderer.shadowCastingMode =
            visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
    }
    /// <summary>
    /// Clears a render texture with an optional background color.
    /// </summary>
    private void ClearRenderTexture(RenderTexture renderTexture, Color? backGroundColor = null)
    {
        if (renderTexture == null) return;
        Color colToUse = backGroundColor ?? Color.black;

        var oldRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, colToUse);
        RenderTexture.active = oldRT;
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
    int SideOfView(Vector3 pos) => Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
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
        return targetView.transform.position + targetView.transform.rotation * (localPoint * GetMappingScaleTo(targetView));
    }

    public Vector3 MapLinkedPointToSource(Vector3 linkedWorldPoint)
    {
        if (linkedView == null)
            return linkedWorldPoint;

        float scale = GetMappingScaleTo(linkedView);
        Vector3 linkedLocalPoint = Quaternion.Inverse(linkedView.transform.rotation) * (linkedWorldPoint - linkedView.transform.position);
        return transform.position + transform.rotation * (linkedLocalPoint / Mathf.Max(0.0001f, scale));
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
        return targetView.transform.rotation * localRotation;
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
        return (targetView.transform.rotation * localDirection).normalized;
    }

    public Vector3 MapLinearVelocityTo(SpatialView targetView, Vector3 velocity)
    {
        if (targetView == null)
            return velocity;

        Vector3 localVelocity = Quaternion.Inverse(transform.rotation) * velocity;
        return targetView.transform.rotation * (localVelocity * GetMappingScaleTo(targetView));
    }

    public Vector3 MapAngularVelocityTo(SpatialView targetView, Vector3 angularVelocity)
    {
        if (targetView == null)
            return angularVelocity;

        Vector3 localAngularVelocity = Quaternion.Inverse(transform.rotation) * angularVelocity;
        return targetView.transform.rotation * localAngularVelocity;
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
