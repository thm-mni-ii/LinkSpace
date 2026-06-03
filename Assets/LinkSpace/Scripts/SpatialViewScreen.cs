using UnityEngine;

public class SpatialViewScreen : MonoBehaviour
{
    public SpatialView spatialView;
    public MeshRenderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        gameObject.layer = LayerMask.NameToLayer("SpatialViewScreen");
    }
}
