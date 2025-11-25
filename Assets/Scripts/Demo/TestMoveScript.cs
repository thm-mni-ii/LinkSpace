using UnityEngine;

public class TestMoveScript : MonoBehaviour
{
    [Header("Movement Settings")]
    public bool canMove = false;      // Toggle movement
    public float speed = 5f;          // Units per second
    public Vector3 direction = Vector3.forward; // Movement direction

    void Update()
    {
        if (canMove)
        {
            // Normalize direction so speed is consistent regardless of magnitude
            Vector3 moveDir = direction.normalized;
            transform.position += moveDir * speed * Time.deltaTime;
        }
    }
}
