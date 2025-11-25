using System.Collections;
using UnityEngine;

public class MoveBackAndForth : MonoBehaviour
{
    public Vector3 pointA = new Vector3(0, 0, 0);
    public Vector3 pointB = new Vector3(0, 0, 5);
    public float speed = 2f;

    private void Start()
    {
        StartCoroutine(MoveLoop());
    }

    IEnumerator MoveLoop()
    {
        while (true)
        {
            // Move to B
            yield return StartCoroutine(MoveTo(pointA, pointB));

            // Move back to A
            yield return StartCoroutine(MoveTo(pointB, pointA));
        }
    }

    IEnumerator MoveTo(Vector3 start, Vector3 end)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null; // wait one frame
        }
    }
}
