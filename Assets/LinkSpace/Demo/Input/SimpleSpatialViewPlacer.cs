using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleSpatialViewPlacer : MonoBehaviour
{
    public SpatialView spatialViewPrefab;
    public Material hologramMaterial;          // Assign a simple transparent material
    public float maxRaycastDistance = 20f;
    public LayerMask raycastMask;             // Should NOT include SpatialViewPreview layer
    public Transform handTransform;
    public GameObject grabObject;

    private ExampleInputActions inputActions;

    private GameObject previewView;
    private bool isTriggerPressed = false;
    private bool isPlacing = false;

    private bool placingHit = false;

    private bool destroying = false;
    private SpatialView hoveredView = null;        // spatialView we are currently aiming at
    private Material[][] originalMats;          // store original materials

    private SpatialView viewA = null;
    private SpatialView viewB = null;

    // Grab & move
    private bool isGrabbing = false;
    private SpatialView grabbedView = null;
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
        inputActions.Example.PlaceSpatialView.started += OnPlacePressed;
        inputActions.Example.PlaceSpatialView.canceled += OnPlaceReleased;
        inputActions.Example.DestroySpatialView.started += OnDestroyPressed;
        inputActions.Example.DestroySpatialView.canceled += OnDestroyReleased;
        inputActions.Example.LinkSpatialView.started += OnTriggerPressed;
        inputActions.Example.LinkSpatialView.canceled += OnTriggerReleased;
        inputActions.Example.GrabSpatialView.started += OnGrabPressed;
        inputActions.Example.GrabSpatialView.canceled += OnGrabReleased;
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

        if (isGrabbing && grabbedView != null)
        {
            UpdateGrabPreviewPosition();
            return;
        }

        if (hoveredView != null)
        {
            RestoreSpatialViewMaterials(hoveredView);
            hoveredView = null;
        }
    }

    private void OnPlacePressed(InputAction.CallbackContext ctx)
    {
        StartPreview();
        isPlacing = true;
    }

    private void OnPlaceReleased(InputAction.CallbackContext ctx)
    {
        if (previewView != null && placingHit)
        {
            LinkSpaceManager.Instance.CreateSpatialView(spatialViewPrefab, previewView.transform);
        }

        Destroy(previewView);
        isPlacing = false;
    }

    private void OnDestroyPressed(InputAction.CallbackContext ctx)
    {
        destroying = true;
    }

    private void OnDestroyReleased(InputAction.CallbackContext ctx)
    {
        destroying = false;

        if (hoveredView != null)
        {
            RestoreSpatialViewMaterials(hoveredView);
            LinkSpaceManager.Instance.DestroySpatialView(hoveredView);
            hoveredView = null;
        }
    }

    private void OnTriggerPressed(InputAction.CallbackContext ctx)
    {
        isTriggerPressed = true;
    }

    private void OnTriggerReleased(InputAction.CallbackContext ctx)
    {
        isTriggerPressed = false;

        if (hoveredView != null)
        {
            // Assign viewA or viewB
            if (viewA == null)
            {
                viewA = hoveredView;
            }
            else
            {
                viewB = hoveredView;

                // Link and reset
                LinkSpaceManager.Instance.LinkSpatialViews(viewA, viewB);
                viewA = null;
                viewB = null;
            }

            RestoreSpatialViewMaterials(hoveredView);
            hoveredView = null;
        }
    }

    private void OnSpawnPressed(InputAction.CallbackContext ctx)
    {
        GameObject go = Instantiate(grabObject, handTransform.position + handTransform.forward * 2, Quaternion.identity);
    }

    private void OnGrabPressed(InputAction.CallbackContext ctx)
    {
        isGrabbing = true;

        // Raycast to select spatialView
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            SpatialView spatialView = hit.collider.GetComponentInParent<SpatialView>();
            SpatialViewScreen screen = hit.collider.GetComponent<SpatialViewScreen>();
            if (spatialView != null && screen == null)
            {
                grabbedView = spatialView;
                grabbedOriginalPos = spatialView.transform.position;
                grabbedOriginalRot = spatialView.transform.rotation;

                grabbedView.gameObject.SetActive(false);

                // Create preview hologram
                grabPreview = Instantiate(grabbedView.gameObject);
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

        if (grabbedView != null)
        {
            if (grabPreview != null && placingHit) // valid surface
            {
                LinkSpaceManager.Instance.SetSpatialViewTransform(grabbedView, grabPreview.transform);
            }
            else // restore
            {
                LinkSpaceManager.Instance.SetSpatialViewPosition(grabbedView, grabbedOriginalPos);
                LinkSpaceManager.Instance.SetSpatialViewRotation(grabbedView, grabbedOriginalRot);
            }

            // Re-enable
           grabbedView.gameObject.SetActive(true);

            grabbedView = null;
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
        if (previewView != null) Destroy(previewView);

        previewView = Instantiate(spatialViewPrefab.gameObject);
        previewView.name = "SpatialViewPreview";

        // Put preview on its own non-raycastable layer
        int previewLayer = LayerMask.NameToLayer("Ignore Raycast");
        SetLayerRecursively(previewView, previewLayer);

        // Replace all materials with hologram
        foreach (var rend in previewView.GetComponentsInChildren<Renderer>())
        {
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = hologramMaterial;

            rend.materials = newMats;
        }

        // Disable scripts & collision
        foreach (var comp in previewView.GetComponentsInChildren<MonoBehaviour>())
            comp.enabled = false;

        foreach (var col in previewView.GetComponentsInChildren<Collider>())
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
            previewView.SetActive(true);
            previewView.transform.position = hit.point;
            if (placeUpwards)
            {
                Vector3 forward = Vector3.ProjectOnPlane(handTransform.forward, hit.normal).normalized;
                previewView.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
            }
            else
            {
                previewView.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
                placingHit = true;
        }
        else
        {
            previewView.SetActive(false);
            placingHit = false;
        }
    }

    private void RaycastForDestroy()
    {
        Ray ray = new(handTransform.position, handTransform.forward);
        Debug.DrawRay(handTransform.position, handTransform.forward * maxRaycastDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            SpatialView spatialView = hit.collider.GetComponentInParent<SpatialView>();

            if (spatialView != null)
            {
                if (hoveredView != spatialView)
                {
                    // switch target
                    if (hoveredView != null)
                        RestoreSpatialViewMaterials(hoveredView);

                    hoveredView = spatialView;
                    ApplyHologramMaterials(spatialView);
                }
                return;
            }
        }

        // No spatialView hit � remove highlight
        if (hoveredView != null)
        {
            RestoreSpatialViewMaterials(hoveredView);
            hoveredView = null;
        }
    }
    // -------------------------------
    // Raycast for linking spatialViews
    // -------------------------------
    private void RaycastForLinking()
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        Debug.DrawRay(handTransform.position, handTransform.forward * maxRaycastDistance, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            SpatialView spatialView = hit.collider.GetComponentInParent<SpatialView>();
            if (spatialView != null && spatialView != hoveredView)
            {
                if (hoveredView != null)
                    RestoreSpatialViewMaterials(hoveredView);

                hoveredView = spatialView;
                ApplyHologramMaterials(spatialView);
            }
        }
        else if (hoveredView != null)
        {
            RestoreSpatialViewMaterials(hoveredView);
            hoveredView = null;
        }
    }

    // --------------------------------------------
    // Apply hologram materials to spatialView
    // --------------------------------------------
    private void ApplyHologramMaterials(SpatialView spatialView)
    {
        var renderers = spatialView.GetComponentsInChildren<Renderer>();

        originalMats = new Material[renderers.Length][];

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer rend = renderers[r];

            // skip spatialView screen (special shader)
            if (rend.GetComponent<SpatialViewScreen>() != null)
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
    private void RestoreSpatialViewMaterials(SpatialView spatialView)
    {
        if (originalMats == null) return;

        var renderers = spatialView.GetComponentsInChildren<Renderer>();

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
