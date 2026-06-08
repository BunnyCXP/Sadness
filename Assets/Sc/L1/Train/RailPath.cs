using UnityEngine;

public class RailPath : MonoBehaviour
{
    [Header("路径点")]
    [Tooltip("按火车行驶顺序摆放。建议全部用 Empty。")]
    public Transform[] waypoints;

    [Header("出口连接器")]
    public RailConnector exitConnector;

    public float GetLength()
    {
        if (waypoints == null || waypoints.Length < 2)
            return 0f;

        float length = 0f;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null)
                continue;

            length += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
        }

        return length;
    }

    public Vector3 GetPointAtDistance(float distance)
    {
        if (waypoints == null || waypoints.Length == 0)
            return transform.position;

        if (waypoints.Length == 1 || waypoints[0] == null)
            return waypoints[0] != null ? waypoints[0].position : transform.position;

        float remaining = Mathf.Clamp(distance, 0f, GetLength());

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Transform a = waypoints[i];
            Transform b = waypoints[i + 1];

            if (a == null || b == null)
                continue;

            float segmentLength = Vector3.Distance(a.position, b.position);

            if (segmentLength <= 0.0001f)
                continue;

            if (remaining <= segmentLength)
            {
                float t = remaining / segmentLength;
                return Vector3.Lerp(a.position, b.position, t);
            }

            remaining -= segmentLength;
        }

        Transform last = waypoints[waypoints.Length - 1];
        return last != null ? last.position : transform.position;
    }

    public Vector3 GetTangentAtDistance(float distance)
    {
        if (waypoints == null || waypoints.Length < 2)
            return transform.forward;

        float pathLength = GetLength();
        float clampedDistance = Mathf.Clamp(distance, 0f, pathLength);

        float remaining = clampedDistance;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Transform a = waypoints[i];
            Transform b = waypoints[i + 1];

            if (a == null || b == null)
                continue;

            Vector3 dir = b.position - a.position;
            float segmentLength = dir.magnitude;

            if (segmentLength <= 0.0001f)
                continue;

            if (remaining <= segmentLength)
                return dir.normalized;

            remaining -= segmentLength;
        }

        for (int i = waypoints.Length - 2; i >= 0; i--)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
            {
                Vector3 dir = waypoints[i + 1].position - waypoints[i].position;
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }
        }

        return transform.forward;
    }
}