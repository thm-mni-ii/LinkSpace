using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Portal : MonoBehaviour
{
    #region Inspector Variables

    [Header("Main Settings")]
    [Tooltip("The linked portal that objects will teleport to.")]
    public Portal linkedPortal;

    [Tooltip("Optional offset applied when teleporting through the portal.")]
    public float teleportationOffset = 0.01f;

    [Tooltip("Designates whether this portal is an endpoint (inactive visually and physically).")]
    public bool isEndpoint = false;

    [Header("Portal Creation")]
    [Tooltip("Use the mesh as a portal screen.")]
    public bool useMeshAsScreen = true;

    [Tooltip("The portal screen object.")]
    public PortalScreen screen;

    [Tooltip("Material applied to the portal screen.")]
    public Material screenMaterial;

    [Header("Navigation")]
    [Tooltip("If true, objects can walk through this portal.")]
    public bool walkable = true;

    [Tooltip("If true, objects can teleport through this portal.")]
    public bool teleportable = true;

    [Header("Object Travel")]
    [Tooltip("Mode used for object travel through the portal.")]
    public ObjectTravelMode travelMode = ObjectTravelMode.Raycast;

    [Header("Unity Events")]
    [Tooltip("Event that is triggered when an Object travels through the Portal")]
    public UnityEvent<PortalTraveller> onObjectTravel;
    [Header("Advanced Settings")]
    [Tooltip("Offset used for near clipping plane calculations.")]
    public float nearClipOffset = 0.05f;

    [Tooltip("Maximum allowed near clip distance.")]
    public float nearClipLimit = 0.2f;

    #endregion

    #region Private Variables

    private Camera portalCam;
    private Camera playerCam;
    private List<PortalTraveller> trackedTravellers;
    private MeshFilter screenMeshFilter;
    private RenderTexture viewTextureLeft;
    private RenderTexture viewTextureRight;

    [HideInInspector]
    public Collider enterCollider;

    private Vector3 prevPortalPos;
    private Quaternion prevPortalRot;

    #endregion

    #region Enums

    public enum ObjectTravelMode { None, Traversation, Raycast }

    #endregion

    #region Unity Callbacks

    void Awake()
    {
        CreatePortalHierarchy();

        if (portalCam != null)
            portalCam.enabled = false;

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
            trackedTravellers = new List<PortalTraveller>();
            screenMeshFilter = screen.GetComponent<MeshFilter>();
            screen.meshRenderer.material.SetInt("displayMask", 1);
            enterCollider = GetComponent<Collider>();
            screen.portal = this;
        }
    }

    void Start()
    {
        prevPortalPos = transform.position;
        prevPortalRot = transform.rotation;
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
        if (linkedPortal != null) linkedPortal.linkedPortal = this;
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isEndpoint) return;

        var traveller = other.GetComponent<PortalTraveller>();
        var grabTraveller = other.GetComponent<GrabPortalTraveller>();

        if (grabTraveller)
        {
            if (grabTraveller.IsGrabbed() && travelMode == ObjectTravelMode.Raycast) return;
            grabTraveller.PrepareTeleportOnRelease(this, linkedPortal);
        }

        if (traveller) OnTravellerEnterPortal(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        if (isEndpoint) return;

        var traveller = other.GetComponent<PortalTraveller>();

        if (traveller && trackedTravellers.Contains(traveller))
        {
            var grabTraveller = traveller as GrabPortalTraveller;

            if (grabTraveller && grabTraveller.IsGrabbed()) return;

            ReleaseTraveller(traveller);
        }
    }

    #endregion

    #region Portal Setup
    /// <summary>
    /// Creates portal camera and optional portal screen.
    /// Configures physics components and screen mesh.
    /// </summary>
    private void CreatePortalHierarchy()
    {
        GameObject camObj = new GameObject("PortalCamera");
        camObj.transform.parent = transform;
        portalCam = camObj.AddComponent<Camera>();

        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (!useMeshAsScreen) return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
            throw new NullReferenceException("Mesh not found.");

        GameObject portalScreen = new GameObject("PortalScreen");
        portalScreen.transform.parent = transform;
        portalScreen.transform.localRotation = Quaternion.identity;
        portalScreen.transform.localPosition = Vector3.zero;
        portalScreen.transform.localScale = transform.localScale;
        box.size = new Vector3(box.size.x * transform.localScale.x, box.size.y * transform.localScale.y, box.size.z * transform.localScale.z);
        transform.localScale = Vector3.one;

        MeshFilter mf = portalScreen.AddComponent<MeshFilter>();
        mf.mesh = meshFilter.mesh;
        MeshRenderer mr = portalScreen.AddComponent<MeshRenderer>();
        mr.material = screenMaterial;
        MeshCollider mc = portalScreen.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        portalScreen.layer = LayerMask.NameToLayer("PortalScreen");
        screen = portalScreen.AddComponent<PortalScreen>();
        screen.meshRenderer = mr;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = false;
    }

    #endregion

    #region Traveller Management
    /// <summary>
    /// Registers a traveller entering the portal.
    /// Initializes previous position for teleportation tracking.
    /// </summary>
    public void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if (isEndpoint) return;

        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold(this);
            traveller.previousPosition = traveller.GetPosition();
            trackedTravellers.Add(traveller);
        }
    }
    /// <summary>
    /// Releases a traveller from portal tracking.
    /// Calls exit threshold behavior on the traveller.
    /// </summary>
    public void ReleaseTraveller(PortalTraveller traveller)
    {
        if (isEndpoint) return;

        traveller.ExitPortalThreshold();
        trackedTravellers.Remove(traveller);
    }

    #endregion

    #region Traveller Updates
    /// <summary>
    /// Updates the positions and rotations of traveller clones for visual correctness.
    /// </summary>
    private void HandleTravellerVisuals()
    {
        if (linkedPortal == null || isEndpoint || travelMode == ObjectTravelMode.None) return;

        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;
            Matrix4x4 m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

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
    /// Handles teleportation logic for travellers crossing the portal plane.
    /// Updates traveller positions, rotations, and triggers linked portal entry.
    /// </summary>
    private void HandleTravellerTeleportation()
    {
        if (linkedPortal == null || isEndpoint || travelMode == ObjectTravelMode.None) return;

        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;
            Matrix4x4 m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

            Vector3 localPos = transform.InverseTransformPoint(traveller.GetPosition());

            Matrix4x4 prevPortalMatrix = Matrix4x4.TRS(prevPortalPos, prevPortalRot, Vector3.one);
            Matrix4x4 prevPortalInv = prevPortalMatrix.inverse;

            Vector3 localPrevPos = prevPortalInv.MultiplyPoint3x4(traveller.previousPosition);

            int portalSide = Math.Sign(localPos.z);
            int portalSideOld = Math.Sign(localPrevPos.z);

            GrabPortalTraveller grabTraveller = traveller as GrabPortalTraveller;
            PlayerPortalTraveller playerTraveller = traveller as PlayerPortalTraveller;

            if (grabTraveller == null || (grabTraveller != null && !grabTraveller.IsGrabbed()))
            {
                if (playerTraveller && !walkable) return;

                if (portalSide != portalSideOld)
                {
                    Vector3 positionOld = travellerT.position;
                    Quaternion rotationOld = travellerT.rotation;

                    Vector3 newPosition = m.GetColumn(3);
                    Quaternion newRotation = playerTraveller == null
                        ? m.rotation
                        : Quaternion.Euler(rotationOld.eulerAngles.x, m.rotation.eulerAngles.y, rotationOld.eulerAngles.z);

                    Vector3 tpOffset = playerTraveller ? linkedPortal.transform.forward * teleportationOffset * portalSide : Vector3.zero;

                    traveller.Teleport(newPosition + tpOffset, newRotation);
                    onObjectTravel.Invoke(traveller);

                    if (traveller.graphicsClone != null)
                        traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotationOld);

                    ReleaseTraveller(traveller);
                    linkedPortal.OnTravellerEnterPortal(traveller);

                    i--;
                    continue;
                }
            }

            traveller.previousPosition = traveller.GetPosition();
            prevPortalPos = transform.position;
            prevPortalRot = transform.rotation;
        }
    }

    #endregion

    #region Rendering
    /// <summary>
    /// Callback triggered before camera rendering.
    /// Renders portal view for the player's camera.
    /// </summary>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (isEndpoint) return;
        if (camera != playerCam) return;
        RenderPortalVR(camera);
    }
    /// <summary>
    /// Handles rendering of the portal in VR, including left and right eye textures.
    /// </summary>
    void RenderPortalVR(Camera mainCam)
    {
        CreateViewTextures();

        if (!linkedPortal)
        {
            ClearRenderTexture(viewTextureLeft);
            ClearRenderTexture(viewTextureRight);
            return;
        }

        if (!mainCam.stereoEnabled) return;

        RenderEye(Camera.StereoscopicEye.Left, viewTextureLeft, mainCam);
        RenderEye(Camera.StereoscopicEye.Right, viewTextureRight, mainCam);

        portalCam.targetTexture = null;
        portalCam.ResetProjectionMatrix();
    }
    /// <summary>
    /// Creates or updates render textures for portal camera eyes.
    /// </summary>
    private void CreateViewTextures()
    {
        if (isEndpoint) return;

        if (viewTextureLeft == null || viewTextureLeft.width != Screen.width || viewTextureLeft.height != Screen.height)
        {
            if (viewTextureLeft != null) viewTextureLeft.Release();
            viewTextureLeft = new RenderTexture(Screen.width, Screen.height, 24);
        }

        if (viewTextureRight == null || viewTextureRight.width != Screen.width || viewTextureRight.height != Screen.height)
        {
            if (viewTextureRight != null) viewTextureRight.Release();
            viewTextureRight = new RenderTexture(Screen.width, Screen.height, 24);
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
    /// Renders a single eye of the portal view into the given render texture.
    /// </summary>
    private void RenderEye(Camera.StereoscopicEye eye, RenderTexture target, Camera mainCam)
    {
        if (!linkedPortal) return;

        Transform fromPortal = transform;
        Transform toPortal = linkedPortal.transform;

        portalCam.targetTexture = target;
        portalCam.projectionMatrix = mainCam.GetStereoProjectionMatrix(eye);

        Vector3 eyeWorldPos = mainCam.GetStereoViewMatrix(eye).inverse.MultiplyPoint(Vector3.zero);
        Quaternion eyeWorldRot = mainCam.transform.rotation;

        Vector3 localPos = fromPortal.InverseTransformPoint(eyeWorldPos);
        Quaternion localRot = Quaternion.Inverse(fromPortal.rotation) * eyeWorldRot;

        portalCam.transform.position = toPortal.TransformPoint(localPos);
        portalCam.transform.rotation = toPortal.rotation * localRot;

        SetLinkedPortalVisible(false);
        SetNearClipPlaneVR(toPortal);
        portalCam.Render();
        SetLinkedPortalVisible(true);
    }
    /// <summary>
    /// Toggles visibility of the linked portal screen for rendering.
    /// </summary>
    private void SetLinkedPortalVisible(bool visible)
    {
        if (linkedPortal.screen == null || linkedPortal.isEndpoint) return;

        linkedPortal.screen.meshRenderer.material.SetInt("displayMask", visible ? 1 : 0);
        linkedPortal.screen.meshRenderer.shadowCastingMode =
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
    /// Adjusts the portal camera's near clip plane to avoid clipping geometry.
    /// </summary>
    private void SetNearClipPlaneVR(Transform clipPlanePortal)
    {
        float dot = Mathf.Sign(Vector3.Dot(clipPlanePortal.forward, clipPlanePortal.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlanePortal.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlanePortal.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCam.projectionMatrix = portalCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
    }

    #endregion

    #region Slice Updates
    /// <summary>
    /// Updates slicing parameters for a traveller and its clone to create portal clipping effects.
    /// </summary>
    void UpdateSliceParams(PortalTraveller traveller)
    {
        if (traveller == null || traveller.graphicsObject == null) return;
        int side = SideOfPortal(traveller.GetPosition());
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedPortal.transform.position;
        int cloneSide = -side;
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * -cloneSide;

        float sliceOffsetDst = 0.025f;
        float cloneSliceOffsetDst = 0.025f;

        GrabPortalTraveller grabPortalTraveller = traveller as GrabPortalTraveller;

        if (grabPortalTraveller && grabPortalTraveller.IsGrabbed()
            && !SameSideOfPortal(playerCam.transform.position, traveller.transform.position)
            && travelMode == ObjectTravelMode.Raycast)
        {
            sliceNormal = -sliceNormal;
            cloneSliceNormal = -cloneSliceNormal;
        }

        if (travelMode == ObjectTravelMode.Traversation
            && grabPortalTraveller && grabPortalTraveller.IsGrabbed()
            && grabPortalTraveller.markedForTeleport)
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
    /// Returns the side of the portal for a given position (front or back).
    /// </summary>
    int SideOfPortal(Vector3 pos) => Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    /// <summary>
    /// Checks if two positions are on the same side of the portal.
    /// </summary>
    public bool SameSideOfPortal(Vector3 posA, Vector3 posB) => SideOfPortal(posA) == SideOfPortal(posB);

    /// <summary>
    /// Links a Portal to this portal.
    /// </summary>
    public void Link(Portal portalToLink)
    {
        linkedPortal = portalToLink;
        portalToLink.linkedPortal = this;
    }
    /// <summary>
    /// Unlinks a Portal from this portal.
    /// </summary>
    public void Unlink()
    {
        if (!linkedPortal) return;
        if (linkedPortal.linkedPortal) linkedPortal.linkedPortal = null;
        linkedPortal = null;
    }

    #endregion
}
