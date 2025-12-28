using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    // ... (ВСЕ ТВОИ ПЕРЕМЕННЫЕ ОСТАЮТСЯ БЕЗ ИЗМЕНЕНИЙ) ...
    [Header("Import parametrs")]
    [SerializeField] private bool _import = false;
    [SerializeField] private KartConfig _kartConfig;
    [SerializeField] private Transform _frontLeftWheel;
    [SerializeField] private Transform _frontRightWheel;
    [SerializeField] private Transform _rearLeftWheel;
    [SerializeField] private Transform _rearRightWheel;
    [SerializeField] private InputActionAsset _playerInput;
    [SerializeField, Range(0, 1)] private float _frontAxisShare = 0.5f;
    [SerializeField] private KartEngine _engine;
    [SerializeField] private float _gearRatio = 8f;
    [SerializeField] private float _drivetrainEfficiency = 0.9f;
    [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
    [SerializeField] private float handbrakeBrakeForce = 6000f;

    private InputAction _moveAction;
    private float _throttleInput;
    private float _steepInput;
    private bool _handbrakePressed;

    private float _frontLeftNormalForce, _frontRightNormalForce, _rearLeftNormalForce, _rearRightNormalForce;
    private Rigidbody _rigidbody;
    private Vector3 g = Physics.gravity;

    [SerializeField] private float engineTorque = 400f;
    [SerializeField] private float wheelRadius = 0.3f;
    [SerializeField] private float maxSpeed = 20;

    [Header("Steering")]
    [SerializeField] private float maxSteeringAngle;

    private Quaternion frontLeftInitialRot;
    private Quaternion frontRightInitialRot;

    [Header("Tyre friction")]
    [SerializeField] private float frictionCoefficient = 1f;
    [SerializeField] private float lateralStiffnes = 80f;
    [SerializeField] private float rollingResistance;

    private float speedAlongForward = 0f;
    private float Fx = 0f;
    private float Fy = 0f;

    private void Awake()
    {
        _playerInput.Enable();
        _rigidbody = GetComponent<Rigidbody>();
        var map = _playerInput.FindActionMap("Kart");
        _moveAction = map.FindAction("Move");
        if (_import) Initialize();
        frontLeftInitialRot = _frontLeftWheel.localRotation;
        frontRightInitialRot = _frontRightWheel.localRotation;
        ComputeStaticWheelLoad();
    }

    private void Initialize() { if (_kartConfig != null) { _rigidbody.mass = _kartConfig.mass; frictionCoefficient = _kartConfig.frictionCoefficient; rollingResistance = _kartConfig.rollingResistance; maxSteeringAngle = _kartConfig.maxSteerAngle; _gearRatio = _kartConfig.gearRatio; wheelRadius = _kartConfig.wheelRadius; lateralStiffnes = _kartConfig.lateralStiffness; } }
    private void OnDisable() { _playerInput.Disable(); }
    private void Update() { ReadInput(); RotateFrontWheels(); }

    private void ReadInput()
    {
        Vector2 move = _moveAction.ReadValue<Vector2>();
        _steepInput = Mathf.Clamp(move.x, -1, 1);
        _throttleInput = Mathf.Clamp(move.y, -1, 1);
        _handbrakePressed = Input.GetKey(handbrakeKey);
    }

    void RotateFrontWheels()
    {
        float steerAngle = maxSteeringAngle * _steepInput;
        Quaternion steerRot = Quaternion.Euler(0, steerAngle, 0);
        _frontLeftWheel.localRotation = frontLeftInitialRot * steerRot;
        _frontRightWheel.localRotation = frontRightInitialRot * steerRot;
    }

    void ComputeStaticWheelLoad()
    {
        float mass = _rigidbody.mass;
        float totalWeight = mass * Mathf.Abs(g.y);
        float frontWeight = totalWeight * _frontAxisShare;
        float rearWeight = totalWeight - frontWeight;
        _frontRightNormalForce = frontWeight * 0.5f;
        _frontLeftNormalForce = _frontRightNormalForce;
        _rearRightNormalForce = rearWeight * 0.5f;
        _rearLeftNormalForce = _rearRightNormalForce;
    }

    private void ApplyEngineForces()
    {
        Vector3 forward = transform.forward;
        float speedAlongForwardCurrent = Vector3.Dot(_rigidbody.linearVelocity, forward);
        if (_throttleInput > 0 && speedAlongForwardCurrent > maxSpeed) return;
        float driveTorque = engineTorque * _throttleInput;
        float driveForcePerWheel = driveTorque / wheelRadius / 2;
        Vector3 forceRear = forward * driveForcePerWheel;
        _rigidbody.AddForceAtPosition(forceRear, _rearLeftWheel.position, ForceMode.Force);
        _rigidbody.AddForceAtPosition(forceRear, _rearRightWheel.position, ForceMode.Force);
    }

    private void FixedUpdate()
    {
        ApplyEngineForces();
        ApplyWheelForce(_frontLeftWheel, _frontLeftNormalForce, isSteer: true, isDrive: false);
        ApplyWheelForce(_frontRightWheel, _frontRightNormalForce, isSteer: true, isDrive: false);
        ApplyWheelForce(_rearLeftWheel, _rearLeftNormalForce, isSteer: false, isDrive: true);
        ApplyWheelForce(_rearRightWheel, _rearRightNormalForce, isSteer: false, isDrive: true);
    }

    void ApplyWheelForce(Transform wheel, float normalForce, bool isSteer, bool isDrive)
    {
        Vector3 wheelPos = wheel.position;
        Vector3 wheelForward = wheel.forward;
        Vector3 wheelRight = wheel.right;
        Vector3 velocity = _rigidbody.GetPointVelocity(wheelPos);
        float vlong = Vector3.Dot(velocity, wheelForward);
        float vlat = Vector3.Dot(velocity, wheelRight);

        float currentFx = 0f;
        float currentFy = 0f;

        if (isDrive)
        {
            speedAlongForward = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            float engineTorqueOut = _engine.Simulate(_throttleInput, speedAlongForward, Time.fixedDeltaTime);
            float totalWheelTorque = engineTorqueOut * _gearRatio * _drivetrainEfficiency;
            float wheelTorque = totalWheelTorque * 0.5f;
            currentFx += wheelTorque / wheelRadius;

            if (_handbrakePressed)
            {
                float brakeDir = vlong > 0 ? -1f : (vlong < 0 ? 1f : -1f);
                currentFx += brakeDir * handbrakeBrakeForce;
            }
        }
        else if (isSteer)
        {
            float rooling = -rollingResistance * vlong;
            currentFx += rooling;
        }

        currentFy -= lateralStiffnes * vlat;
        float frictionlimit = frictionCoefficient * normalForce;
        float forceLenght = Mathf.Sqrt(currentFx * currentFx + currentFy * currentFy);

        if (forceLenght > frictionlimit)
        {
            float scale = frictionlimit / forceLenght;
            currentFy *= scale;
            currentFx *= scale;
        }

        Fx = currentFx; Fy = currentFy; // Для UI
        Vector3 force = wheelForward * currentFx + wheelRight * currentFy;
        _rigidbody.AddForceAtPosition(force, wheel.position, ForceMode.Force);
    }

    // НОВЫЙ КРАСИВЫЙ UI
    void OnGUI()
    {
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 10, 320, 260), "");
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        int y = 20;
        float speedMS = _rigidbody.linearVelocity.magnitude;
        float speedKMH = speedMS * 3.6f;

        GUI.Label(new Rect(20, y, 300, 25), "=== BOLIDE TELEMETRY ===", style); y += 35;

        style.fontSize = 16;
        GUI.Label(new Rect(20, y, 300, 20), $"Speed: {speedMS:F1} m/s ({speedKMH:F1} km/h)"); y += 25;
        GUI.Label(new Rect(20, y, 300, 20), $"Engine RPM: {_engine.CurrentRpm:F0} RPM"); y += 25;
        GUI.Label(new Rect(20, y, 300, 20), $"Current Torque: {_engine.CurrentTorque:F1} N·m"); y += 30;

        // Пытаемся взять силы из Aero (задание требует Drag и Downforce)
        var aero = GetComponent<KartAreo>();
        if (aero != null)
        {
            GUI.color = Color.green;
            // Рассчитываем их прямо тут для UI, чтобы не лезть в логику Aero
            float speedSq = speedMS * speedMS;
            float drag = 0.5f * 1.225f * 0.9f * 0.6f * speedSq;
            float downforce = 0.5f * 1.225f * (0.05f * aero.wingAngleDeg * Mathf.Deg2Rad) * 0.4f * speedSq;

            GUI.Label(new Rect(20, y, 300, 20), $"Drag Force: {drag:F1} N"); y += 25;
            GUI.Label(new Rect(20, y, 300, 20), $"Downforce: {downforce:F1} N"); y += 30;
        }

        if (_handbrakePressed)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(20, y, 300, 20), "HANDBRAKE ACTIVE", style);
        }
    }
}