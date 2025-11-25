using UnityEngine;

public class PortalScreen : MonoBehaviour
{
    public Portal portal;
    public MeshRenderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }
}
