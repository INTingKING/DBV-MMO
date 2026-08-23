using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Tooltip("Seconds to catch up to the target. 0 = instant (recommended for local player).")]
    [SerializeField] private float smoothTime = 0.05f;

    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    private Transform _target;
    private Vector3 _velocity;

    private void Awake()
    {
        Instance = this;
        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        _target = newTarget;
        _velocity = Vector3.zero;

        if (snap && _target != null)
            transform.position = _target.position + offset;
    }

    public void ClearTarget()
    {
        _target = null;
        _velocity = Vector3.zero;
    }

    public bool IsFollowing(Transform candidate)
    {
        return _target == candidate;
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        Vector3 desiredPosition = _target.position + offset;

        if (smoothTime <= 0.0001f)
        {
            transform.position = desiredPosition;
            _velocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _velocity,
            smoothTime);
    }
}
