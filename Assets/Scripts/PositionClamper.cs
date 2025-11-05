using UnityEngine;

public class PositionClamper : MonoBehaviour
{
    public Vector3 min = new Vector3(-1f, -1f, -1f);
    public Vector3 max = new Vector3(1f, 1f, 1f);
    public bool smooth = false;
    public float smoothSpeed = 5f; // units per second when smooth=true
    public bool useLocal = true; // clamp localPosition if true, world position otherwise

    void LateUpdate()
    {
        Vector3 pos = useLocal ? transform.localPosition : transform.position;
        Vector3 clamped = new Vector3(
            Mathf.Clamp(pos.x, min.x, max.x),
            Mathf.Clamp(pos.y, min.y, max.y),
            Mathf.Clamp(pos.z, min.z, max.z)
        );

        if (smooth)
        {
            float step = smoothSpeed * Time.deltaTime;
            if (useLocal)
                transform.localPosition = Vector3.MoveTowards(pos, clamped, step);
            else
                transform.position = Vector3.MoveTowards(pos, clamped, step);
        }
        else
        {
            if (useLocal)
                transform.localPosition = clamped;
            else
                transform.position = clamped;
        }
    }
}