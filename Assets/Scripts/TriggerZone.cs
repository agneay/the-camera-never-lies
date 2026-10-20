using UnityEngine;

/// Axis-aligned volume polled against the player position (no physics triggers needed).
public class TriggerZone : MonoBehaviour
{
    public Vector3 size = Vector3.one;

    public bool Contains(Vector3 p) => new Bounds(transform.position, size).Contains(p);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.25f);
        Gizmos.DrawCube(transform.position, size);
    }
}
