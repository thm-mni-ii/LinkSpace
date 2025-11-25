using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;

public class PortalTraveller : MonoBehaviour {

    public GameObject graphicsObject;
    public GameObject graphicsClone { get; set; }
    public Vector3 previousPosition { get; set; }

    public Material[] originalMaterials { get; set; }
    public Material[] cloneMaterials { get; set; }

    [HideInInspector]
    public bool markedForTp;


    private Rigidbody rb;

    private void Awake()
    {
        GetComponent<Rigidbody>();
    }

    public virtual void Teleport (Vector3 pos, Quaternion rot) {
        
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

    // Called when first touches portal
    public virtual void EnterPortalThreshold(Portal portal) {     
        if (graphicsClone == null ) {
            if (graphicsObject == null) return;
            graphicsClone = Instantiate (graphicsObject);
            PortalTraveller graphicsTraveller = graphicsClone.GetComponent<PortalTraveller>();
            if(graphicsTraveller != null ) Destroy (graphicsTraveller); 
            graphicsClone.transform.parent = graphicsObject.transform.parent;
            graphicsClone.transform.localScale = graphicsObject.transform.localScale;
            originalMaterials = GetMaterials (graphicsObject);
            cloneMaterials = GetMaterials (graphicsClone);
        } else {
            graphicsClone.SetActive (true);
        }
    }

    // Called once no longer touching portal (excluding when teleporting)
    public virtual void ExitPortalThreshold ()
    {
        if (graphicsClone == null) return;
        graphicsClone.SetActive (false);
        // Disable slicing
        for (int i = 0; i < originalMaterials.Length; i++) {
            originalMaterials[i].SetVector ("sliceNormal", Vector3.zero);
        }
    }

    public void SetSliceOffsetDst (float dst, bool clone) {
        for (int i = 0; i < originalMaterials.Length; i++) {
            if (clone) {
                cloneMaterials[i].SetFloat ("sliceOffsetDst", dst);
            } else {
                originalMaterials[i].SetFloat ("sliceOffsetDst", dst);
            }

        }
    }

    Material[] GetMaterials (GameObject g) {
        var renderers = g.GetComponentsInChildren<MeshRenderer> ();
        var matList = new List<Material> ();
        foreach (var renderer in renderers) {
            foreach (var mat in renderer.materials) {
                matList.Add (mat);
            }
        }
        return matList.ToArray ();
    }

    public virtual Vector3 GetPosition()
    {
        if (rb != null)
        {
            return rb.position;
        }
        else
        {
           return transform.position;
        }
    }

    
}