using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    public PlayerInputActions playerActions;

    [SerializeField] float _moveSpeed;
    [SerializeField] [Min(0)] float _startingMoveDuration;
    [SerializeField] AnimationCurve _startingMoveCurve;
    [SerializeField] [Min(0)] float _endingMoveDuration;
    [SerializeField] AnimationCurve _endingMoveCurve;
    [SerializeField] [Range(0,1)] float _sneakingAmount;
    [SerializeField] [Min(0)] float _speedTransitionDuration;
    [SerializeField] ParticleSystem _dashTrailParticle;
    [SerializeField] private ParticleSystem _dashParticle;
    [SerializeField] float _dashSpeed;
    [SerializeField] float _dashDuration;
    [SerializeField] [Min(0)] float _timeBeforeNextDash;
    [SerializeField] float _minimumSize;
    [SerializeField] bool _changeScale;
    [SerializeField] bool _isRotatingToDirection = true;
    [SerializeField] float _timeToRotate = 0;

    InputAction _moveInput;
    InputAction _sneakInput;
    InputAction _dashInput;

    Vector2 _inputDirection = Vector2.zero;
    Vector2 _currentDirection = Vector2.zero;
    Vector2 _lastDirection = Vector2.zero;
    Vector2 _defaultScale;

    float _currentMoveSpeed;
    float _currentDashSpeed;
    float _currentSpeedTransition;
    float _currentDashDuration;
    float _currentCurveTime;
    float _currentEndingMoveDuration;
    float _currentTimeToRotate;
    float _currentTimeBeforeNextDash;

    bool _isMoving = false;

    Transform _transform;

    Transform _cam;

    void Awake()
    {
        _cam = Camera.main.transform;
        _transform = GetComponent<Transform>();
        _defaultScale = _transform.localScale;

        playerActions = new PlayerInputActions();

        _currentMoveSpeed = _moveSpeed;
        _currentDashSpeed = 0;
        _currentTimeToRotate = 0;
        _currentDashDuration = _dashDuration;
        _currentSpeedTransition = _speedTransitionDuration;
        _currentEndingMoveDuration = _endingMoveDuration;
        _currentTimeBeforeNextDash = _timeBeforeNextDash;
    }

    void OnEnable()
    {
        _moveInput = playerActions.Player.Move;
        _moveInput.Enable();
        _sneakInput = playerActions.Player.Sneak;
        _sneakInput.Enable();
        _dashInput = playerActions.Player.Dash;
        _dashInput.Enable();
    }

    void OnDisable()
    {
        _moveInput.Disable();
        _sneakInput.Disable();
        _dashInput.Disable();
    }

    void Update()
    {
        _inputDirection = _moveInput.ReadValue<Vector2>();

        _currentDashSpeed = CalculateDashSpeed();
        _currentMoveSpeed = CalculateSmoothSpeed() + _currentDashSpeed;
        _currentDirection = CalculateSmoothDirection();

        Move();
        RotateToDirection();
        ClampPositionToCamera();
        SetScale();
    }

    //void Aspirator()
    //{
    //    _transform.position = Vector3.MoveTowards(_transform.position, targetPosition.position, 4);
    //}

    void SetScale()
    {
        _transform.localScale = _changeScale ? CalculateScaleFromSpeed() : _defaultScale;
    }

    Vector2 CalculateScaleFromSpeed()
    {
        Vector2 minScale =  _minimumSize * _defaultScale;
        float maxMoveSpeed = _moveSpeed + _dashSpeed;
        float speedScale = _currentMoveSpeed * _currentDirection.magnitude;

        return Vector2.Lerp(_defaultScale, minScale, speedScale / maxMoveSpeed);
    }

    void ClampPositionToCamera()
    {

        float xOffset = 8.5f;
        float yOffset = 4.5f;
        float clampedXPosition = Mathf.Clamp(_transform.position.x, -xOffset + _cam.position.x, xOffset + _cam.position.x);
        float clampedYPosition = Mathf.Clamp(_transform.position.y, -yOffset + _cam.position.y, yOffset + _cam.position.y);
        _transform.position = new Vector3(clampedXPosition, clampedYPosition, 0);
    }


    void RotateToDirection()
    {
        Vector2 dir;

        if (_currentDirection == Vector2.zero)
        {
            _currentTimeToRotate = 0;
            dir = _lastDirection;
        }
        else
        {
            _currentTimeToRotate += Time.deltaTime;
            dir = Vector2.Lerp(_lastDirection, _currentDirection, _currentTimeToRotate / _timeToRotate);
        }

        float angle = _isRotatingToDirection ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    float CalculateSmoothSpeed()
    {
        float targetSpeed = _sneakInput.IsPressed() ? _moveSpeed * _sneakingAmount : _moveSpeed;

        if (_currentMoveSpeed != targetSpeed)
            _currentSpeedTransition = 0;

        _currentSpeedTransition += Time.deltaTime;

        return Mathf.Lerp(_currentMoveSpeed, targetSpeed, _currentSpeedTransition / _speedTransitionDuration);
    }

    float CalculateDashSpeed()
    {
        _currentTimeBeforeNextDash += Time.deltaTime;
        if (_dashInput.triggered && _currentTimeBeforeNextDash >= _timeBeforeNextDash)
        {
            _currentTimeBeforeNextDash = 0;
            _currentDashDuration = 0;
            _dashParticle.transform.position = _transform.position;
            _dashParticle.Play();
            _dashTrailParticle.Play();
        }

        //if (_dashSpeed == 0)
        //{
        //    _currentEndingMoveDuration = _endingMoveDuration;
        //}
        //else
        //{
        //    _currentEndingMoveDuration = _dashDuration;
        //}

        // if is no more moving after dash, there will be no more dash effect.
        _currentDashDuration = _isMoving ? _currentDashDuration + Time.deltaTime : _dashDuration;

        return Mathf.Lerp(_dashSpeed, 0, _currentDashDuration / _dashDuration);
    }

    Vector2 CalculateSmoothDirection()
    {
        if (_moveInput.triggered)
        {
            _currentCurveTime = 0;
            _isMoving = true;
        }

        if (_inputDirection == Vector2.zero && _isMoving)
        {
            _currentCurveTime = 0;
            _lastDirection = _currentDirection;
            _isMoving = false;
        }

        float curveValue = _isMoving ? CalculateCurveValue(_startingMoveCurve, _startingMoveDuration) : CalculateCurveValue(_endingMoveCurve, _currentEndingMoveDuration);
        return Vector2.Lerp(_currentDirection, _inputDirection.normalized, curveValue);
    }

    void Move()
    {
        transform.Translate(Time.deltaTime * _currentMoveSpeed * _currentDirection, relativeTo: Space.World);
    }

    float CalculateCurveValue(AnimationCurve curve, float duration)
    {
        if (_currentCurveTime >= duration)
            return curve.Evaluate(1);

        _currentCurveTime += Time.deltaTime;

        float xCurve = _currentCurveTime / duration;

        return curve.Evaluate(xCurve);
    }
}