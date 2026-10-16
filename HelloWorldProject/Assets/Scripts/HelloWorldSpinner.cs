using UnityEngine;

/// <summary>
/// Drives a visible rotation and emits a heartbeat log once per second.
/// The heartbeat is deliberately observable from outside the Editor so an
/// agent can prove the game loop is actually advancing (not frozen on frame 1)
/// by watching the angle change and the console log count grow.
/// </summary>
public class HelloWorldSpinner : MonoBehaviour
{
    [Tooltip("Rotation speed around Z, in degrees per second.")]
    public float degreesPerSecond = 90f;

    [Tooltip("Seconds between heartbeat log lines.")]
    public float heartbeatInterval = 1f;

    private float _heartbeatTimer;
    private int _beat;

    void Start()
    {
        Debug.Log($"[HelloWorldSpinner] Start on '{gameObject.name}' at t={Time.time:F2}s");
    }

    void Update()
    {
        // 2D projects live in the XY plane, so Z is the spin axis.
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);

        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= heartbeatInterval)
        {
            _heartbeatTimer -= heartbeatInterval;
            _beat++;
            Debug.Log($"[HelloWorldSpinner] beat={_beat} t={Time.time:F1}s angleZ={transform.eulerAngles.z:F1}");
        }
    }
}
