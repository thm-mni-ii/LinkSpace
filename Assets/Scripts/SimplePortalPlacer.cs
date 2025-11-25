using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePortalPlacer : MonoBehaviour
{
    public Portal portalPrefab;
    public Material hologramMaterial;          // Assign a simple transparent material
    public float maxRaycastDistance = 20f;
    public LayerMask raycastMask;             // Should NOT include PortalPreview layer
    public Transform handTransform;
    public GameObject grabObject;

    private ExampleInputActions inputActions;

    private GameObject previewPortal;
    private bool isTriggerPressed = false;
    private bool isPlacing = false;

    private bool placingHit = false;

    private bool destroying = false;
    private Portal hoveredPortal = null;        // portal we are currently aiming at
    private Material[][] originalMats;          // store original materials

    private Portal portalA = null;
    private Portal portalB = null;

    // Grab & move
    private bool isGrabbing = false;
    private Portal grabbedPortal = null;
    private Vector3 grabbedOriginalPos;
    private Quaternion grabbedOriginalRot;
    private GameObject grabPreview = null;

    private bool placeUpwards = false;

    private void Awake()
    {
        inputActions = new ExampleInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Example.PlacePortal.started += OnPlacePressed;
        inputActions.Example.PlacePortal.canceled += OnPlaceReleased;
        inputActions.Example.DestroyPortal.started += OnDestroyPressed;
        inputActions.Example.DestroyPortal.canceled += OnDestroyReleased;
        inputActions.Example.LinkPortal.started += OnTriggerPressed;
        inputActions.Example.LinkPortal.canceled += OnTriggerReleased;
        inputActions.Example.GrabPortal.started += OnGrabPressed;
        inputActions.Example.GrabPortal.canceled += OnGrabReleased;
        inputActions.Example.SpawnObject.started += OnSpawnPressed;
        inputActions.Example.SwapAlign.started += OnSwapPressed;


    }

    private void OnDisable()
    {
        inputActions.Disable();
    }
    private void Update()
    {
        if (isPlacing)
        {
            UpdatePreviewPosition();
            return;
        }

        if (destroying)
        {
            RaycastForDestroy();
            return;
        }

        if (isTriggerPressed)
        {
            RaycastForLinking();
            return;
        }

        if (isGrabbing && grabbedPortal != null)
        {
            UpdateGrabPreviewPosition();
            return;
        }

        if (hoveredPortal != null)
        {
            RestorePortalMaterials(hoveredPortal);
            hoveredPortal = null;
        }
    }

    private void OnPlacePressed(InputAction.CallbackContext ctx)
    {
        StartPreview();
        isPlacing = true;
    }

    private void OnPlaceReleased(InputAction.CallbackContext ctx)
    {
        if (previewPortal != null && placingHit)
        {
            PortalManager.Instance.CreatePortal(portalPrefab, previewPortal.transform);
        }

        Destroy(previewPortal);
        isPlacing = false;
    }

    private void OnDestroyPressed(InputAction.CallbackContext ctx)
    {
        destroying = true;
    }

    private void OnDestroyReleased(InputAction.CallbackContext ctx)
    {
        destroying = false;

        if (hoveredPortal != null)
        {
            RestorePortalMaterials(hoveredPortal);
            PortalManager.Instance.DestroyPortal(hoveredPortal);
            hoveredPortal = null;
        }
    }

    private void OnTriggerPressed(InputAction.CallbackContext ctx)
    {
        isTriggerPressed = true;
    }

    private void OnTriggerReleased(InputAction.CallbackContext ctx)
    {
        isTriggerPressed = false;

        if (hoveredPortal != null)
        {
            // Assign portalA or portalB
            if (portalA == null)
            {
                portalA = hoveredPortal;
            }
            else
            {
                portalB = hoveredPortal;

                // Link and reset
                PortalManager.Instance.LinkPortals(portalA, portalB);
                portalA = null;
                portalB = null;
            }

            RestorePortalMaterials(hoveredPortal);
            hoveredPortal = null;
        }
    }

    private void OnSpawnPressed(InputAction.CallbackContext ctx)
    {
        GameObject go = Instantiate(grabObject, handTransform.position + handTransform.forward * 2, Quaternion.identity);
    }

    private void OnGrabPressed(InputAction.CallbackContext ctx)
    {
        isGrabbing = true;

        // Raycast to select portal
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            Portal portal = hit.collider.GetComponentInParent<Portal>();
            PortalScreen screen = hit.collider.GetComponent<PortalScreen>();
            if (portal != null && screen == null)
            {
                grabbedPortal = portal;
                grabbedOriginalPos = portal.transform.position;
                grabbedOriginalRot = portal.transform.rotation;

                grabbedPortal.gameObject.SetActive(false);

                // Create preview hologram
                grabPreview = Instantiate(grabbedPortal.gameObject);
                SetLayerRecursively(grabPreview, LayerMask.NameToLayer("Ignore Raycast"));

                foreach (var rend in grabPreview.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[rend.materials.Length];
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = hologramMaterial;
                    rend.materials = mats;
                }

                foreach (var comp in grabPreview.GetComponentsInChildren<MonoBehaviour>())
                    comp.enabled = false;
                foreach (var col in grabPreview.GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
        }
    }

    private void OnGrabReleased(InputAction.CallbackContext ctx)
    {
        isGrabbing = false;

        if (grabbedPortal != null)
        {
            if (grabPreview != null && placingHit) // valid surface
            {
                PortalManager.Instance.SetPortalTransform(grabbedPortal, grabPreview.transform);
            }
            else // restore
            {
                PortalManager.Instance.SetPortalPosition(grabbedPortal, grabbedOriginalPos);
                PortalManager.Instance.SetPortalRotation(grabbedPortal, grabbedOriginalRot);
            }

            // Re-enable
           grabbedPortal.gameObject.SetActive(true);

            grabbedPortal = null;
        }

        if (grabPreview != null)
        {
            Destroy(grabPreview);
            grabPreview = null;
        }
    }

    private void OnSwapPressed(InputAction.CallbackContext ctx)
    {
        placeUpwards = !placeUpwards;
    }
    

    // ---------------------------------------------------------
    //  Create Preview Object
    // ---------------------------------------------------------
    private void StartPreview()
    {
        if (previewPortal != null) Destroy(previewPortal);

        previewPortal = Instantiate(portalPrefab.gameObject);
        previewPortal.name = "PortalPreview";

        // Put preview on its own non-raycastable layer
        int previewLayer = LayerMask.NameToLayer("Ignore Raycast");
        SetLayerRecursively(previewPortal, previewLayer);

        // Replace all materials with hologram
        foreach (var rend in previewPortal.GetComponentsInChildren<Renderer>())
        {
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = hologramMaterial;

            rend.materials = newMats;
        }

        // Disable scripts & collision
        foreach (var comp in previewPortal.GetComponentsInChildren<MonoBehaviour>())
            comp.enabled = false;

        foreach (var col in previewPortal.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    // ---------------------------------------------------------
    //  Update Preview Position
    // ---------------------------------------------------------
    private void UpdatePreviewPosition()
    {
        Ray r = new Ray(handTransform.position, handTransform.forward);
        
        if (Physics.Raycast(r, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            previewPortal.SetActive(true);
            previewPortal.transform.position = hit.point;
            if (placeUpwards)
            {
                Vector3 forward = Vector3.ProjectOnPlane(handTransform.forward, hit.normal).normalized;
                previewPortal.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
            }
            else
            {
                previewPortal.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
                placingHit = true;
        }
        else
        {
            previewPortal.SetActive(false);
            placingHit = false;
        }
    }

    private void RaycastForDestroy()
    {
        Ray ray = new(handTransform.position, handTransform.forward);
        Debug.DrawRay(handTransform.position, handTransform.forward * maxRaycastDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            Portal portal = hit.collider.GetComponentInParent<Portal>();

            if (portal != null)
            {
                if (hoveredPortal != portal)
                {
                    // switch target
                    if (hoveredPortal != null)
                        RestorePortalMaterials(hoveredPortal);

                    hoveredPortal = portal;
                    ApplyHologramMaterials(portal);
                }
                return;
            }
        }

        // No portal hit — remove highlight
        if (hoveredPortal != null)
        {
            RestorePortalMaterials(hoveredPortal);
            hoveredPortal = null;
        }
    }
    // -------------------------------
    // Raycast for linking portals
    // -------------------------------
    private void RaycastForLinking()
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        Debug.DrawRay(handTransform.position, handTransform.forward * maxRaycastDistance, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            Portal portal = hit.collider.GetComponentInParent<Portal>();
            if (portal != null && portal != hoveredPortal)
            {
                if (hoveredPortal != null)
                    RestorePortalMaterials(hoveredPortal);

                hoveredPortal = portal;
                ApplyHologramMaterials(portal);
            }
        }
        else if (hoveredPortal != null)
        {
            RestorePortalMaterials(hoveredPortal);
            hoveredPortal = null;
        }
    }

    // --------------------------------------------
    // Apply hologram materials to portal
    // --------------------------------------------
    private void ApplyHologramMaterials(Portal portal)
    {
        var renderers = portal.GetComponentsInChildren<Renderer>();

        originalMats = new Material[renderers.Length][];

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer rend = renderers[r];

            // skip portal screen (special shader)
            if (rend.GetComponent<PortalScreen>() != null)
            {
                originalMats[r] = null;
                continue;
            }

            originalMats[r] = rend.materials;

            Material[] holoMats = new Material[rend.materials.Length];
            for (int i = 0; i < holoMats.Length; i++)
                holoMats[i] = hologramMaterial;

            rend.materials = holoMats;
        }
    }

    // --------------------------------------------
    // Restore original materials
    // --------------------------------------------
    private void RestorePortalMaterials(Portal portal)
    {
        if (originalMats == null) return;

        var renderers = portal.GetComponentsInChildren<Renderer>();

        for (int r = 0; r < renderers.Length; r++)
        {
            if (originalMats[r] == null)
                continue; // skip screen

            renderers[r].materials = originalMats[r];
        }

        originalMats = null;
    }

    private void UpdateGrabPreviewPosition()
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        Debug.DrawRay(handTransform.position, handTransform.forward * maxRaycastDistance, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            grabPreview.SetActive(true);
            grabPreview.transform.position = hit.point;
            if (placeUpwards)
            {
                Vector3 forward = Vector3.ProjectOnPlane(handTransform.forward, hit.normal).normalized;
                grabPreview.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
            }
            else
            {
                grabPreview.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
            placingHit = true;
        }
        else
        {
            grabPreview.SetActive(false);
            placingHit = false;
        }
    }

    // Utility
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
