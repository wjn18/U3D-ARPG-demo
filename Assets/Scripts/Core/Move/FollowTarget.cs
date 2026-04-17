using UnityEngine;

public class TankFollowTarget : MonoBehaviour
{
    public Transform body;
    public Vector3 offset;

    void LateUpdate()
    {
        if (body == null) return;

        transform.position = body.position + offset;

        transform.rotation = Quaternion.identity;
    }
}