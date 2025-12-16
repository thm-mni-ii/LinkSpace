using System.Collections.Generic;
using UnityEngine;

public class PortalTraveller : MonoBehaviour
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
    /// Called when the traveller first touches a portal.
    /// Sets up the graphics clone and material references.
    /// </summary>
    public virtual void EnterPortalThreshold(Portal portal)
    {
        if (portal.isEndpoint) return;

        if (graphicsClone == null)
        {
            if (graphicsObject == null) return;

            graphicsClone = Instantiate(graphicsObject);
            PortalTraveller graphicsTraveller = graphicsClone.GetComponent<PortalTraveller>();
            if (graphicsTraveller != null) Destroy(graphicsTraveller);

            graphicsClone.transform.parent = graphicsObject.transform.parent;
            graphicsClone.transform.localScale = graphicsObject.transform.localScale;

            originalMaterials = GetMaterials(graphicsObject);
            cloneMaterials = GetMaterials(graphicsClone);
        }
        else
        {
            graphicsClone.SetActive(true);
        }
    }

    /// <summary>
    /// Called when the traveller is no longer touching a portal.
    /// Disables the graphics clone and slicing effects.
    /// </summary>
    public virtual void ExitPortalThreshold()
    {
        if (graphicsClone == null) return;

        graphicsClone.SetActive(false);

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
        }
    }

    /// <summary>
    /// Sets the slice offset distance for the traveller's materials.
    /// </summary>
    /// <param name="dst">Offset distance value</param>
    /// <param name="clone">If true, applies to clone materials; otherwise to original</param>
    public void SetSliceOffsetDst(float dst, bool clone)
    {
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (clone)
            {
                cloneMaterials[i].SetFloat("sliceOffsetDst", dst);
            }
            else
            {
                originalMaterials[i].SetFloat("sliceOffsetDst", dst);
            }
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
