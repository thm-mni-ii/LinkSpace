using System.Collections.Generic;
using UnityEngine;

public class SpatialTraveller : MonoBehaviour
{
    #region Inspector Variables

    [Tooltip("The main graphics object of the traveller.")]
    public GameObject graphicsObject;

    [HideInInspector]
    [Tooltip("Marks whether the traveller is scheduled for teleport.")]
    public bool markedForTp;

    #endregion

    #region Public Properties

    [HideInInspector]
    public GameObject graphicsClone { get; set; }

    [HideInInspector]
    public Vector3 previousPosition { get; set; }

    [HideInInspector]
    public Material[] originalMaterials { get; set; }

    [HideInInspector]
    public Material[] cloneMaterials { get; set; }

    #endregion

    #region Protected Variables

    protected Rigidbody rb;
    private readonly List<SpatialView> visualTrackingViews = new List<SpatialView>();
    private SpatialView visualOwner;

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Initializes references.
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Teleports the traveller to a new position and rotation.
    /// Optionally preserves momentum.
    /// </summary>
    public virtual void Teleport(Vector3 pos, Quaternion rot)
    {
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = rot;

            Physics.SyncTransforms();
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// Called when the traveller first touches a spatialView.
    /// Sets up the graphics clone and material references.
    /// </summary>
    public virtual void EnterSpatialViewThreshold(SpatialView spatialView)
    {
        if (spatialView.isEndpoint) return;

        if (!visualTrackingViews.Contains(spatialView))
            visualTrackingViews.Add(spatialView);

        if (!UsesPortalVisualClone(spatialView))
        {
            if (visualOwner == spatialView)
                ReleasePortalVisualOwner();

            return;
        }

        EnsureGraphicsClone();

        if (graphicsClone == null)
            return;

        if (visualOwner == null)
            visualOwner = spatialView;

        if (visualOwner == spatialView)
            graphicsClone.SetActive(true);
    }

    /// <summary>
    /// Called when the traveller is no longer touching a spatialView.
    /// Disables the clone only when the released spatialView owns the visual proxy.
    /// </summary>
    public virtual void ExitSpatialViewThreshold(SpatialView spatialView)
    {
        if (spatialView != null)
            visualTrackingViews.Remove(spatialView);

        if (visualOwner != spatialView)
            return;

        ReleasePortalVisualOwner();
        TryClaimNextPortalVisualOwner();
    }

    /// <summary>
    /// Called when the traveller is no longer touching any spatialView.
    /// Disables the graphics clone and slicing effects.
    /// </summary>
    public virtual void ExitSpatialViewThreshold()
    {
        visualTrackingViews.Clear();
        ReleasePortalVisualOwner();
    }

    /// <summary>
    /// Returns true when this spatialView owns the traveller's single visual clone.
    /// </summary>
    public bool IsPortalVisualOwner(SpatialView spatialView)
    {
        return visualOwner == spatialView && graphicsClone != null && graphicsClone.activeSelf;
    }

    /// <summary>
    /// Returns true when this traveller should use portal clone/slice visuals for the given spatialView.
    /// </summary>
    public virtual bool UsesPortalVisualClone(SpatialView spatialView)
    {
        return true;
    }

    /// <summary>
    /// Disables portal clone/slice visuals without changing trigger tracking.
    /// </summary>
    public void DisablePortalVisuals()
    {
        ReleasePortalVisualOwner();
    }

    /// <summary>
    /// Applies a uniform mapping scale to the traveller.
    /// </summary>
    public virtual void ApplyMappingScale(float scaleMultiplier)
    {
        if (Mathf.Approximately(scaleMultiplier, 1f))
            return;

        transform.localScale *= Mathf.Max(0.0001f, scaleMultiplier);
    }

    /// <summary>
    /// Sets the slice offset distance for the traveller's materials.
    /// </summary>
    /// <param name="dst">Offset distance value</param>
    /// <param name="clone">If true, applies to clone materials; otherwise to original</param>
    public void SetSliceOffsetDst(float dst, bool clone)
    {
        Material[] materials = clone ? cloneMaterials : originalMaterials;

        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].SetFloat("sliceOffsetDst", dst);
        }
    }

    /// <summary>
    /// Returns the traveller's current position, either via Rigidbody or transform.
    /// </summary>
    public virtual Vector3 GetPosition()
    {
        return rb != null ? rb.position : transform.position;
    }

    #endregion

    #region Private Methods

    private void EnsureGraphicsClone()
    {
        if (graphicsClone != null)
            return;

        if (graphicsObject == null) return;

        graphicsClone = Instantiate(graphicsObject);
        SpatialTraveller graphicsTraveller = graphicsClone.GetComponent<SpatialTraveller>();
        if (graphicsTraveller != null) Destroy(graphicsTraveller);

        graphicsClone.transform.parent = graphicsObject.transform.parent;
        graphicsClone.transform.localScale = graphicsObject.transform.localScale;
        graphicsClone.SetActive(false);

        originalMaterials = GetMaterials(graphicsObject);
        cloneMaterials = GetMaterials(graphicsClone);
    }

    private void ReleasePortalVisualOwner()
    {
        visualOwner = null;

        if (graphicsClone != null)
            graphicsClone.SetActive(false);

        ResetOriginalSlice();
    }

    private void TryClaimNextPortalVisualOwner()
    {
        for (int i = visualTrackingViews.Count - 1; i >= 0; i--)
        {
            SpatialView spatialView = visualTrackingViews[i];

            if (spatialView == null || spatialView.isEndpoint)
            {
                visualTrackingViews.RemoveAt(i);
                continue;
            }

            if (!UsesPortalVisualClone(spatialView))
                continue;

            EnsureGraphicsClone();

            if (graphicsClone == null)
                return;

            visualOwner = spatialView;
            graphicsClone.SetActive(true);
            return;
        }
    }

    private void ResetOriginalSlice()
    {
        if (originalMaterials == null)
            return;

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
        }
    }

    /// <summary>
    /// Collects all materials from the given GameObject and its children.
    /// </summary>
    private Material[] GetMaterials(GameObject g)
    {
        var renderers = g.GetComponentsInChildren<MeshRenderer>();
        var matList = new List<Material>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                matList.Add(mat);
            }
        }
        return matList.ToArray();
    }

    #endregion
}
