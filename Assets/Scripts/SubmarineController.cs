using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(Rigidbody))]
public class SubmarineController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float maxSpeed = 10f;
    public float turnRate = 45f;
    public float acceleration = 2f;
    public float deceleration = 4f;

    [Header("Buoyancy Settings")]
    public float baseBuoyancy = 9.81f;
    public float ascendForce = 25f;
    public float descendForce = 30f;
    public float buoyancyChangeSpeed = 5f;
    private float currentBuoyancy = 0f;

    [Header("Input References")]
    public InputActionAsset inputActions;
    private InputAction steering;
    private InputAction throttle;
    private InputAction brake;
    private InputAction ascend;
    private InputAction descend;
    private InputAction lookAction;

    private Rigidbody rb;
    private float currentSpeed;
    private float currentTurn;

    [Header("VR Settings")]
    public bool vrMode = false;
    public Camera subCamera; // Regular camera (non-VR)
    public XROrigin xrOrigin; // VR camera rig
    public Camera vrCamera; // The actual VR camera

    [Header("Controller Look Settings")]
    public bool simulateVR = true;
    public float lookSensitivity = 100f;
    public float maxLookAngle = 80f;
    private Vector2 lookInput;
    private float xRotation, yRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 5000f;

        // Initialize input actions
        var submarineMap = inputActions.FindActionMap("Submarine");
        steering = submarineMap.FindAction("Steering");
        throttle = submarineMap.FindAction("Throttle");
        brake = submarineMap.FindAction("Brake");
        ascend = submarineMap.FindAction("Ascend");
        descend = submarineMap.FindAction("Descend");
        lookAction = submarineMap.FindAction("Look");

        EnableInputs();

        // Initialize camera references
        if (subCamera == null) subCamera = GetComponentInChildren<Camera>();
        if (xrOrigin != null)
        {
            vrCamera = xrOrigin.Camera;
            xrOrigin.gameObject.SetActive(vrMode);
        }

        // Ensure proper camera state
        if (subCamera != null) subCamera.enabled = !vrMode;
    }

    void EnableInputs()
    {
        steering.Enable();
        throttle.Enable();
        brake.Enable();
        ascend.Enable();
        descend.Enable();
        if (simulateVR) lookAction.Enable();
    }

    void Update()
    {
        // Handle controller look in non-VR mode
        if (!vrMode && simulateVR)
        {
            lookInput = lookAction.ReadValue<Vector2>();
            HandleControllerLook();
        }

        // Prevent submarine tilt in VR mode
        if (vrMode)
        {
            Vector3 currentRot = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0, currentRot.y, 0);
        }
    }

    void HandleControllerLook()
    {
        if (subCamera == null) return;

        float mouseX = lookInput.x * lookSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * lookSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        yRotation += mouseX;

        subCamera.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        HandleDepth();
    }

    void HandleMovement()
    {
        float throttleInput = throttle.ReadValue<float>();
        float brakeInput = brake.ReadValue<float>();

        if (throttleInput > 0.1f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, maxSpeed * throttleInput, acceleration * Time.deltaTime);
        }
        else if (brakeInput > 0.1f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, -maxSpeed * 0.5f * brakeInput, deceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.deltaTime);
        }

        rb.velocity = transform.forward * currentSpeed;
    }

    void HandleRotation()
    {
        float rawTurn = steering.ReadValue<float>();
        currentTurn = rawTurn * turnRate * Time.fixedDeltaTime;
        rb.AddTorque(Vector3.up * currentTurn, ForceMode.VelocityChange);
    }

    void HandleDepth()
    {
        float targetBuoyancy = baseBuoyancy;

        if (ascend.ReadValue<float>() > 0.1f)
            targetBuoyancy += ascendForce;
        else if (descend.ReadValue<float>() > 0.1f)
            targetBuoyancy -= descendForce;

        currentBuoyancy = Mathf.Lerp(
            currentBuoyancy,
            targetBuoyancy,
            buoyancyChangeSpeed * Time.fixedDeltaTime
        );

        rb.AddForce(Vector3.up * currentBuoyancy * rb.mass, ForceMode.Force);
    }

    public void ToggleVRMode(bool enableVR)
    {
        vrMode = enableVR;
        if (xrOrigin != null) xrOrigin.gameObject.SetActive(vrMode);
        if (subCamera != null) subCamera.enabled = !vrMode;
    }
void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Steering: {steering.ReadValue<float>()}");
        GUILayout.Label($"Throttle: {throttle.ReadValue<float>()}");
        GUILayout.Label($"Brake: {brake.ReadValue<float>()}");
        GUILayout.Label($"Ascend: {ascend.ReadValue<float>()}");
        GUILayout.Label($"Descend: {descend.ReadValue<float>()}");
        GUILayout.EndArea();
    }
}
