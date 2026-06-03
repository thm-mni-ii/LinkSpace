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

    #endregion

    #region Enums

    public enum ObjectTravelMode { None, Traversation, Raycast }

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
            Transform travellerT = traveller.transform;
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;
            Matrix4x4 m = linkedView.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

            if (traveller.graphicsClone != null && traveller.graphicsObject != null)
            {
                Vector3 localOffset = traveller.graphicsObject.transform.localPosition;
                Quaternion localRotOffset = traveller.graphicsObject.transform.localRotation;

                traveller.graphicsClone.transform.position = m.MultiplyPoint(localOffset);
                traveller.graphicsClone.transform.rotation = m.rotation * localRotOffset;
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
            Transform travellerT = traveller.transform;
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;
            Matrix4x4 m = linkedView.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

            Vector3 localPos = transform.InverseTransformPoint(traveller.GetPosition());

            Matrix4x4 prevSpatialViewMatrix = Matrix4x4.TRS(prevViewPos, prevViewRot, Vector3.one);
            Matrix4x4 prevSpatialViewInv = prevSpatialViewMatrix.inverse;

            Vector3 localPrevPos = prevSpatialViewInv.MultiplyPoint3x4(traveller.previousPosition);

            int viewSide = Math.Sign(localPos.z);
            int viewSideOld = Math.Sign(localPrevPos.z);

            GrabSpatialTraveller grabTraveller = traveller as GrabSpatialTraveller;
            PlayerSpatialTraveller playerTraveller = traveller as PlayerSpatialTraveller;

            if (grabTraveller == null || (grabTraveller != null && !grabTraveller.IsGrabbed()))
            {
                if (playerTraveller && !walkable) return;

                if (viewSide != viewSideOld)
                {
                    Vector3 positionOld = travellerT.position;
                    Quaternion rotationOld = travellerT.rotation;

                    Vector3 newPosition = m.GetColumn(3);
                    Quaternion newRotation = playerTraveller == null
                        ? m.rotation
                        : Quaternion.Euler(rotationOld.eulerAngles.x, m.rotation.eulerAngles.y, rotationOld.eulerAngles.z);

                    Vector3 tpOffset = playerTraveller ? linkedView.transform.forward * teleportationOffset * viewSide : Vector3.zero;

                    traveller.Teleport(newPosition + tpOffset, newRotation);
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

    #endregion

    #region Rendering
    /// <summary>
    /// Callback triggered before camera rendering.
    /// Renders spatialView view for the player's camera.
    /// </summary>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (isEndpoint) return;
        if (camera != playerCam) return;
        RenderSpatialViewVR(camera);
    }
    /// <summary>
    /// Handles rendering of the spatialView in VR, including left and right eye textures.
    /// </summary>
    void RenderSpatialViewVR(Camera mainCam)
    {
        CreateViewTextures();

        if (!linkedView)
        {
            ClearRenderTexture(viewTextureLeft);
            ClearRenderTexture(viewTextureRight);
            return;
        }

        if (!mainCam.stereoEnabled) return;

        RenderEye(Camera.StereoscopicEye.Left, viewTextureLeft, mainCam);
        RenderEye(Camera.StereoscopicEye.Right, viewTextureRight, mainCam);

        viewCam.targetTexture = null;
        viewCam.ResetProjectionMatrix();
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
            width = Screen.width;
            height = Screen.height;
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
        if (!linkedView) return;

        Transform fromView = transform;
        Transform toView = linkedView.transform;

        viewCam.targetTexture = target;
        viewCam.projectionMatrix = mainCam.GetStereoProjectionMatrix(eye);

        Vector3 eyeWorldPos = mainCam.GetStereoViewMatrix(eye).inverse.MultiplyPoint(Vector3.zero);
        Quaternion eyeWorldRot = mainCam.transform.rotation;

        Vector3 localPos = fromView.InverseTransformPoint(eyeWorldPos);
        Quaternion localRot = Quaternion.Inverse(fromView.rotation) * eyeWorldRot;

        viewCam.transform.position = toView.TransformPoint(localPos);
        viewCam.transform.rotation = toView.rotation * localRot;

        SetLinkedViewVisible(false);
        SetNearClipPlaneVR(toView);
        viewCam.Render();
        SetLinkedViewVisible(true);
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
