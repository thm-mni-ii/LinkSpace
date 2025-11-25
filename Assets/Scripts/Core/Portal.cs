using System.Collections.Generic;
using System.Net;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class Portal : MonoBehaviour
{
    [Header("Main Settings")]
    public Portal linkedPortal;
    public PortalScreen screen;
    public int recursionLimit = 5;
    public float teleportationOffset = 0.01f;

    [Header("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;

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

    void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = false;

        trackedTravellers = new List<PortalTraveller>();
        screenMeshFilter = screen.GetComponent<MeshFilter>();

        // Set initial display mask to 1
        screen.meshRenderer.material.SetInt("displayMask", 1);
        enterCollider = GetComponent<Collider>();
        screen.portal = this;
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

    // Called when the script is disabled
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void LateUpdate()
    {
        HandleTravellerVisuals();
    }
    private void FixedUpdate()
    {
        HandleTravellerTeleportation();
    }
    void HandleTravellerVisuals()
    {
        if(linkedPortal == null) return;
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;

            // Compute the traveller's transformation matrix
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;

            // Compute the matrix that transforms from this portal space to the linked portal space
            Matrix4x4 m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

            

            // ---- UPDATE GRAPHICS CLONE ----
            if (traveller.graphicsClone != null && traveller.graphicsObject != null)
            {
                // Maintain same local offset and rotation for clone
                Vector3 localOffset = traveller.graphicsObject.transform.localPosition;
                Quaternion localRotOffset = traveller.graphicsObject.transform.localRotation;

                traveller.graphicsClone.transform.position = m.MultiplyPoint(localOffset);
                traveller.graphicsClone.transform.rotation = m.rotation * localRotOffset;
            }

            // ---- UPDATE SLICE EFFECT ----
            UpdateSliceParams(traveller);

            // Update previous frame data      
        }
    }

    private void HandleTravellerTeleportation()
    {
        if (linkedPortal == null) return;
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;

            // Compute the traveller's transformation matrix
            Matrix4x4 travellerMatrix = travellerT.localToWorldMatrix;

            // Compute the matrix that transforms from this portal space to the linked portal space
            Matrix4x4 m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerMatrix;

            // ---- FIX: Use local-space position to determine which side of the portal the traveller is on ----
            Vector3 localPos = transform.InverseTransformPoint(traveller.GetPosition());

            Matrix4x4 prevPortalMatrix =
                Matrix4x4.TRS(prevPortalPos, prevPortalRot, Vector3.one);

            Matrix4x4 prevPortalInv = prevPortalMatrix.inverse;

            // traveller’s previous position relative to the portal’s previous frame
            Vector3 localPrevPos = prevPortalInv.MultiplyPoint3x4(traveller.previousPosition);
            // (Make sure traveller.previousPosition is updated each frame like previousOffsetFromPortal was)

            int portalSide = System.Math.Sign(localPos.z);
            int portalSideOld = System.Math.Sign(localPrevPos.z);

            GrabPortalTraveller grabTraveller = traveller as GrabPortalTraveller;
            PlayerPortalTraveller playerTraveller = traveller as PlayerPortalTraveller;

            // ---- TELEPORT LOGIC ----
            if (grabTraveller == null || (grabTraveller != null && !grabTraveller.IsGrabbed()))
            {
                if (portalSide != portalSideOld)
                {
                    Debug.Log("tp");
                    // Traveller crossed the portal plane
                    Vector3 positionOld = travellerT.position;
                    Quaternion rotationOld = travellerT.rotation;

                    // Apply transformation matrix to get new position and rotation through the portal
                    Vector3 newPosition = m.GetColumn(3);

                    Quaternion newRotation = playerTraveller == null ? m.rotation : Quaternion.Euler(rotationOld.eulerAngles.x, m.rotation.eulerAngles.y, rotationOld.eulerAngles.z);

                    // Optional offset (apply in linked portal’s forward direction, not this portal’s)
                    Vector3 tpOffset = playerTraveller ? linkedPortal.transform.forward * teleportationOffset * portalSide : Vector3.zero;

                    traveller.Teleport(newPosition + tpOffset, newRotation);


                    // Keep graphics clone at pre-teleport position for seamless transition
                    if (traveller.graphicsClone != null)
                        traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotationOld);

                    // Release and register traveller with the linked portal
                    ReleaseTraveller(traveller);
                    linkedPortal.OnTravellerEnterPortal(traveller);

                    i--;
                    continue; // move to next traveller (avoid updating clone after teleport)
                }
            }
            traveller.previousPosition = traveller.GetPosition();
            prevPortalPos = transform.position;
            prevPortalRot = transform.rotation;
        }
    }


    void CreateViewTextures()
    {
        // LEFT Eye
        if (viewTextureLeft == null || viewTextureLeft.width != Screen.width || viewTextureLeft.height != Screen.height)
        {
            if (viewTextureLeft != null) viewTextureLeft.Release();
            viewTextureLeft = new RenderTexture(Screen.width, Screen.height, 24);           
        }

        // RIGHT Eye
        if (viewTextureRight == null || viewTextureRight.width != Screen.width || viewTextureRight.height != Screen.height)
        {
            if (viewTextureRight != null) viewTextureRight.Release();
            viewTextureRight = new RenderTexture(Screen.width, Screen.height, 24);
        }

        screen.meshRenderer.material.SetTexture("_LeftEyeTex", viewTextureLeft);
        screen.meshRenderer.material.SetTexture("_RightEyeTex", viewTextureRight);
    }


    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != playerCam) return;
        RenderPortalVR(camera);

    }
    void RenderPortalVR(Camera mainCam)
    {
        CreateViewTextures();
        if (!linkedPortal)
        {
            ClearRenderTexture(viewTextureLeft);
            ClearRenderTexture(viewTextureRight);
            return;
        }
        ;
        if (!mainCam.stereoEnabled) return;

        

        // Left eye
        RenderEye(Camera.StereoscopicEye.Left, viewTextureLeft, mainCam);
        // Right eye
        RenderEye(Camera.StereoscopicEye.Right, viewTextureRight, mainCam);

        // Cleanup
        portalCam.targetTexture = null;
        portalCam.ResetProjectionMatrix();
    }

    private void ClearRenderTexture(RenderTexture renderTexture, Color? backGroundColor = null)
    {
        if (renderTexture == null) return;
        Color colToUse = backGroundColor?? Color.black;
        
        var oldRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, colToUse);
        RenderTexture.active = oldRT;
    }
    // Renders one eye into the given target without recursion
    private void RenderEye(Camera.StereoscopicEye eye, RenderTexture target, Camera mainCam)
    {
        if (!linkedPortal) return;

        var fromPortal = transform;
        var toPortal = linkedPortal.transform;

        // Use projection matrix from the currently rendering XR camera
        portalCam.targetTexture = target;
        portalCam.projectionMatrix = mainCam.GetStereoProjectionMatrix(eye);

        // Eye world pose
        Vector3 eyeWorldPos = mainCam.GetStereoViewMatrix(eye).inverse.MultiplyPoint(Vector3.zero);
        Quaternion eyeWorldRot = mainCam.transform.rotation;

        // Transform eye pose through the portal
        Vector3 localPos = fromPortal.InverseTransformPoint(eyeWorldPos);
        Quaternion localRot = Quaternion.Inverse(fromPortal.rotation) * eyeWorldRot;

        portalCam.transform.position = toPortal.TransformPoint(localPos);
        portalCam.transform.rotation = toPortal.rotation * localRot;

        SetLinkedPortalVisible(false);
        SetNearClipPlaneVR(toPortal);
        portalCam.Render();
        SetLinkedPortalVisible(true);
    }

    private void SetLinkedPortalVisible(bool visible)
    {
        linkedPortal.screen.meshRenderer.material.SetInt("displayMask", visible ? 1 : 0);
        linkedPortal.screen.meshRenderer.shadowCastingMode =
            visible ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }

    int SideOfPortal(Vector3 pos) => System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    public bool SameSideOfPortal(Vector3 posA, Vector3 posB) => SideOfPortal(posA) == SideOfPortal(posB);

    public void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold(this);
            traveller.previousPosition = traveller.GetPosition();
            trackedTravellers.Add(traveller);
        }
    }
    void UpdateSliceParams(PortalTraveller traveller)
    {
        int side = SideOfPortal(traveller.GetPosition());
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 slicePos = transform.position;

        Vector3 cloneSlicePos = linkedPortal.transform.position;

        // Compute clone side based on original object side
        int cloneSide = -side; // opposite of original side
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * -cloneSide;

        float sliceOffsetDst = 0.025f;
        float cloneSliceOffsetDst = 0.025f;


        GrabPortalTraveller grabPortalTraveller = traveller as GrabPortalTraveller;
        if (grabPortalTraveller && grabPortalTraveller.IsGrabbed()
            && !SameSideOfPortal(playerCam.transform.position, traveller.transform.position)
            && PortalManager.Instance.useRaycastTravel)
        {

            sliceNormal = -sliceNormal;
            cloneSliceNormal = -cloneSliceNormal;
        }
        if(!PortalManager.Instance.useRaycastTravel 
            && grabPortalTraveller && grabPortalTraveller.IsGrabbed()
            && grabPortalTraveller.markedForTeleport)
        {
            sliceNormal = -sliceNormal;
            cloneSliceNormal = -cloneSliceNormal;
        }

        // Apply to materials
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
        else
        {
            //portalCam.ResetProjectionMatrix();
        }
    }

    public Ray TransformRayThroughPortal(Ray ray)
    {
        // Build transform matrix
        Matrix4x4 m = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix;

        Vector3 newOrigin = m.MultiplyPoint(ray.origin);
        Vector3 newDirection = m.MultiplyVector(ray.direction);

        return new Ray(newOrigin, newDirection);
    }



    void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();       
        var grabTraveller = other.GetComponent<GrabPortalTraveller>();
        if (grabTraveller)
        {
            if (grabTraveller.IsGrabbed() && PortalManager.Instance.useRaycastTravel) return;
            grabTraveller.PrepareTeleportOnRelease(this, linkedPortal);
        }
        if (traveller) OnTravellerEnterPortal(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        
        //var grabTraveller = other.GetComponent<GrabPortalTraveller>();
        if (traveller && trackedTravellers.Contains(traveller))
        {
            var grabTraveller = traveller as GrabPortalTraveller;

            if (PortalManager.Instance.useRaycastTravel && grabTraveller && grabTraveller.IsGrabbed()) return;

            ReleaseTraveller(traveller);
            
           
        }
    }
    public void ReleaseTraveller(PortalTraveller traveller)
    {
        traveller.ExitPortalThreshold();
        trackedTravellers.Remove(traveller);
    }
    public void Link(Portal portalToLink)
    {
        linkedPortal = portalToLink;
        portalToLink.linkedPortal = this;
    }

    public void Unlink()
    {
        if (!linkedPortal) return;
        if(linkedPortal.linkedPortal) linkedPortal.linkedPortal = null;
        linkedPortal = null;
    }
    void OnValidate()
    {
        if (linkedPortal != null) linkedPortal.linkedPortal = this;
    }
}
