using UnityEngine;
using UnityEngine.Serialization;

public class MoveLightController : MonoBehaviour
{
    [Header("References")]
    public Camera referenceCamera;
    [FormerlySerializedAs("screenCheckPoint")]
    public Transform controlledPoint;
    public Transform swingTarget;

    [Header("Movement")]
    [FormerlySerializedAs("speed")]
    public float moveSpeed = 7.5f;
    public float moveSmoothTime = 0.08f;

    [Header("Bounds")]
    [Range(0f, 0.49f)] public float viewportPadding = 0.06f;

    [Header("Swing")]
    public bool enableSwing = true;
    public float maxSwingAngle = 6f;
    public float swingResponse = 1.25f;
    public float swingSpring = 16f;
    public float swingDamping = 7.5f;

    private Vector3 _targetWorldPosition;
    private Vector3 _moveVelocity;
    private Vector3 _lastPosition;
    private Vector3 _lastVelocity;
    private Quaternion _restSwingRotation;
    private float _swingAngle;
    private float _swingVelocity;
    private bool _initialized;

    private void Start()
    {
        Initialize(force: true);
    }

    private void LateUpdate()
    {
        if (!Initialize())
        {
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        UpdateTargetPosition(dt);
        MoveRoot(dt);
        ClampControlledPointInsideViewport();
        UpdateSwing(dt);
    }

    private bool Initialize(bool force = false)
    {
        if (referenceCamera == null)
        {
            referenceCamera = Camera.main;
            if (referenceCamera == null)
            {
                return false;
            }
        }

        if (_initialized && !force)
        {
            return true;
        }

        Transform point = GetControlledPoint();
        Vector3 viewportPoint = referenceCamera.WorldToViewportPoint(point.position);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        _targetWorldPosition = transform.position;
        _lastPosition = transform.position;
        _lastVelocity = Vector3.zero;
        _restSwingRotation = GetSwingTarget().localRotation;
        _swingAngle = 0f;
        _swingVelocity = 0f;
        _initialized = true;
        return true;
    }

    private void UpdateTargetPosition(float dt)
    {
        Vector2 input = ReadInput();
        if (input.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector3 right = referenceCamera.transform.right;
        Vector3 up = referenceCamera.transform.up;
        Vector3 moveDirection = (right * input.x) + (up * input.y);

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        _targetWorldPosition += moveDirection * moveSpeed * dt;
    }

    private void MoveRoot(float dt)
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            _targetWorldPosition,
            ref _moveVelocity,
            moveSmoothTime,
            Mathf.Infinity,
            dt);
    }

    private void ClampControlledPointInsideViewport()
    {
        Transform point = GetControlledPoint();
        Vector3 viewportPoint = referenceCamera.WorldToViewportPoint(point.position);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        float clampedX = Mathf.Clamp(viewportPoint.x, viewportPadding, 1f - viewportPadding);
        float clampedY = Mathf.Clamp(viewportPoint.y, viewportPadding, 1f - viewportPadding);
        if (Mathf.Approximately(clampedX, viewportPoint.x) && Mathf.Approximately(clampedY, viewportPoint.y))
        {
            return;
        }

        viewportPoint.x = clampedX;
        viewportPoint.y = clampedY;

        Vector3 clampedWorldPoint = referenceCamera.ViewportToWorldPoint(viewportPoint);
        Vector3 delta = clampedWorldPoint - point.position;
        transform.position += delta;
        _targetWorldPosition += delta;
        _moveVelocity = Vector3.zero;
    }

    private void UpdateSwing(float dt)
    {
        Transform target = GetSwingTarget();
        if (!enableSwing)
        {
            target.localRotation = Quaternion.Slerp(
                target.localRotation,
                _restSwingRotation,
                1f - Mathf.Exp(-10f * dt));
            _lastPosition = transform.position;
            _lastVelocity = Vector3.zero;
            _swingAngle = 0f;
            _swingVelocity = 0f;
            return;
        }

        Vector3 currentVelocity = (transform.position - _lastPosition) / dt;
        _lastPosition = transform.position;
        _lastVelocity = currentVelocity;

        float lateralVelocity = Vector3.Dot(currentVelocity, referenceCamera.transform.right);
        float targetAngle = Mathf.Clamp(-lateralVelocity * swingResponse, -maxSwingAngle, maxSwingAngle);
        float restore = (targetAngle - _swingAngle) * swingSpring;
        float damp = -_swingVelocity * swingDamping;

        _swingVelocity += (restore + damp) * dt;
        _swingAngle += _swingVelocity * dt;
        _swingAngle = Mathf.Clamp(_swingAngle, -maxSwingAngle, maxSwingAngle);

        target.localRotation = _restSwingRotation * Quaternion.Euler(0f, 0f, _swingAngle);
    }

    private Transform GetControlledPoint()
    {
        return controlledPoint != null ? controlledPoint : transform;
    }

    private Transform GetSwingTarget()
    {
        if (swingTarget != null)
        {
            return swingTarget;
        }

        if (controlledPoint != null)
        {
            return controlledPoint;
        }

        return transform;
    }

    private static Vector2 ReadInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.L)) horizontal += 1f;
        if (Input.GetKey(KeyCode.J)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.I)) vertical += 1f;
        if (Input.GetKey(KeyCode.K)) vertical -= 1f;

        Vector2 input = new Vector2(horizontal, vertical);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        return input;
    }
}
