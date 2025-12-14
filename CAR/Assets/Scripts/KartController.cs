using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Import parametrs")]
    [SerializeField] private bool _import = false;
    [SerializeField] private KartConfig _kartConfig;

    [Header("Wheel attachment points")]
    [SerializeField] private Transform _frontLeftWheel;
    [SerializeField] private Transform _frontRightWheel;
    [SerializeField] private Transform _rearLeftWheel;
    [SerializeField] private Transform _rearRightWheel;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionAsset _playerInput;

    [Header("Weight distribution")]
    [SerializeField, Range(0, 1)] private float _frontAxisShare = 0.5f;

    [Header("Engine & drivetrain")]
    [SerializeField] private KartEngine _engine;
    [SerializeField] private float _gearRatio = 8f;
    [SerializeField] private float _drivetrainEfficiency = 0.9f;

    private InputAction _moveAction;
    private float _throttleInput;
    private float _steepInput;
    private bool _handbrakePressed; // ← ручник

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
    [SerializeField] private float rollingResistance = 30f; 

    [Header("Handbrake")]
    [SerializeField] private float handbrakeRollingMultiplier = 8f; 

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

    private void Initialize()
    {
        if (_kartConfig != null)
        {
            _rigidbody.mass = _kartConfig.mass;
            frictionCoefficient = _kartConfig.frictionCoefficient;
            rollingResistance = _kartConfig.rollingResistance;
            maxSteeringAngle = _kartConfig.maxSteerAngle;
            _gearRatio = _kartConfig.gearRatio;
            wheelRadius = _kartConfig.wheelRadius;
            lateralStiffnes = _kartConfig.lateralStiffness;
        }
    }

    private void OnDisable()
    {
        _playerInput.Disable();
    }

    private void Update()
    {
        ReadInput();
        RotateFrontWheels();
    }

    private void ReadInput()
    {
        Vector2 move = _moveAction.ReadValue<Vector2>();
        _steepInput = Mathf.Clamp(move.x, -1, 1);
        _throttleInput = Mathf.Clamp(move.y, -1, 1);

       
        _handbrakePressed = Input.GetKey(KeyCode.Space);

        
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

        _frontRightNormalForce = _frontLeftNormalForce = frontWeight * 0.5f;
        _rearRightNormalForce = _rearLeftNormalForce = rearWeight * 0.5f;
    }

    private void ApplyEngineForces()
    {
        Vector3 forward = transform.forward;
        float speedAlongForward = Vector3.Dot(_rigidbody.linearVelocity, forward);
        if (_throttleInput > 0 && speedAlongForward > maxSpeed) return;

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

        Fx = 0f;
        Fy = 0f;

        bool isRear = (wheel == _rearLeftWheel || wheel == _rearRightWheel);

       
        float currentLateralStiffness = lateralStiffnes;
        float currentRollingResistance = rollingResistance;

        if (isRear && _handbrakePressed)
        {
            currentLateralStiffness = 0f; 

          
            currentRollingResistance *= handbrakeRollingMultiplier; 

            
            float additionalBrakeForce = 1200f; 
            if (Mathf.Abs(vlong) > 0.5f) 
            {
                float brakeDir = vlong > 0 ? -1f : 1f; 
                Fx += brakeDir * additionalBrakeForce;
            }
        }

        
        if (isDrive)
        {
            speedAlongForward = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            float engineTorqueOut = _engine.Simulate(_throttleInput, speedAlongForward, Time.fixedDeltaTime);
            float totalWheelTorque = engineTorqueOut * _gearRatio * _drivetrainEfficiency;
            float wheelTorque = totalWheelTorque * 0.5f;
            Fx += wheelTorque / wheelRadius;
        }

        
        if (isSteer)
        {
            float rooling = -currentRollingResistance * vlong;
            Fx += rooling;
        }

        
        float fyRaw = -currentLateralStiffness * vlat;
        Fy += fyRaw;

        
        float frictionlimit = frictionCoefficient * normalForce;
        float forceLenght = Mathf.Sqrt(Fx * Fx + Fy * Fy);
        if (forceLenght > frictionlimit)
        {
            float scale = frictionlimit / forceLenght;
            Fy += scale;  
            Fx += scale;
        }

        Vector3 force = wheelForward * Fx + wheelRight * Fy;
        _rigidbody.AddForceAtPosition(force, wheel.position, ForceMode.Force);
    }

    
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f));
        backgroundStyle.padding = new RectOffset(15, 15, 10, 10);

        GUILayout.BeginArea(new Rect(20, 20, 400, 350), backgroundStyle);
        GUILayout.BeginVertical();

        GUILayout.Label("=== KART TELEMETRY ===", style);

        style.fontSize = 20;
        style.normal.textColor = Color.cyan;
        GUILayout.Space(10);
        GUILayout.Label($"Speed: {speedAlongForward:0.0} m/s ({(speedAlongForward * 3.6f):0.0} km/h)", style);
        GUILayout.Label($"Engine RPM: {_engine.CurrentRpm:0} RPM", style);

        Color torqueColor = _engine.CurrentTorque > 300 ? Color.green : Color.yellow;
        GUI.color = torqueColor;
        GUILayout.Label($"Engine Torque: {_engine.CurrentTorque:0.0} N·m", style);
        GUI.color = Color.white;

        if (_handbrakePressed)
        {
            GUI.color = Color.red;
            GUILayout.Label("HANDBRAKE ON → DRIFT!", style);
            GUI.color = Color.white;
        }

        GUILayout.Space(10);
        style.normal.textColor = Color.magenta;
        GUILayout.Label("Wheel Forces:", style);

        style.fontSize = 18;
        style.normal.textColor = Color.white;
        GUILayout.Label($"Fx: {Fx:0.0} N");
        GUILayout.Label($"Fy: {Fy:0.0} N");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}