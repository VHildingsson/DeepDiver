using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;
using System.Collections;

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

    [Header("VR Camera Settings")]
    [Tooltip("Local position offset for VR camera relative to submarine")]
    public Vector3 vrCameraLocalOffset = new Vector3(0, 1.6f, 0);
    [Tooltip("If true, maintains the offset set in the editor")]
    public bool useEditorOffset = false;
    private Vector3 editorCameraOffset;
    private bool isXrOriginInitialized = false;
    [Tooltip("Should the camera maintain its editor position?")]
    public bool maintainEditorPosition = true;
    [Tooltip("Manual camera offset if not using editor position")]
    public Vector3 manualCameraOffset = new Vector3(0, 1.6f, 0.2f);

    private Vector3 initialCameraLocalPosition;
    private Quaternion initialCameraLocalRotation;

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

            // Store initial camera transform BEFORE any XR initialization
            initialCameraLocalPosition = vrCamera.transform.localPosition;
            initialCameraLocalRotation = vrCamera.transform.localRotation;

            // Parent the XR Origin to the submarine but maintain world position
            xrOrigin.transform.SetParent(transform, true);

            // Reset local position to zero (relative to submarine)
            xrOrigin.transform.localPosition = Vector3.zero;
            xrOrigin.transform.localRotation = Quaternion.identity;

            xrOrigin.gameObject.SetActive(vrMode);
            isXrOriginInitialized = true;
        }

        if (subCamera != null) subCamera.enabled = !vrMode;
    }

    void LateUpdate()
    {
        if (!vrMode || !isXrOriginInitialized || xrOrigin == null) return;

        // Constantly verify the camera hasn't drifted
        if (maintainEditorPosition)
        {
            if (Vector3.Distance(vrCamera.transform.localPosition, initialCameraLocalPosition) > 0.01f)
            {
                vrCamera.transform.localPosition = initialCameraLocalPosition;
            }
        }
        else
        {
            if (Vector3.Distance(vrCamera.transform.localPosition, manualCameraOffset) > 0.01f)
            {
                vrCamera.transform.localPosition = manualCameraOffset;
            }
        }
    }

    private bool IsValidTransform(Transform t)
    {
        return float.IsFinite(t.position.x) && float.IsFinite(t.position.y) && float.IsFinite(t.position.z) &&
               float.IsFinite(t.rotation.x) && float.IsFinite(t.rotation.y) && float.IsFinite(t.rotation.z) && float.IsFinite(t.rotation.w);
    }

    private void ResetVRCamera()
    {
        if (xrOrigin == null) return;

        xrOrigin.transform.localPosition = Vector3.zero;
        xrOrigin.transform.localRotation = Quaternion.identity;
        vrCamera.transform.localPosition = useEditorOffset ? editorCameraOffset : vrCameraLocalOffset;
        vrCamera.transform.localRotation = Quaternion.identity;

        Debug.Log("VR Camera was reset due to invalid state");
    }

    void Start()
    {
        if (vrMode && xrOrigin != null)
        {
            // Wait one frame to ensure XR is initialized
            StartCoroutine(DelayedCameraPositioning());
        }
    }
    IEnumerator DelayedCameraPositioning()
    {
        // Wait for end of frame to ensure XR is fully initialized
        yield return new WaitForEndOfFrame();

        if (maintainEditorPosition)
        {
            vrCamera.transform.localPosition = initialCameraLocalPosition;
            vrCamera.transform.localRotation = initialCameraLocalRotation;
        }
        else
        {
            vrCamera.transform.localPosition = manualCameraOffset;
            vrCamera.transform.localRotation = Quaternion.identity;
        }

        // Force update the tracking origin
        xrOrigin.MoveCameraToWorldLocation(transform.TransformPoint(vrCamera.transform.localPosition));
        xrOrigin.MatchOriginUpCameraForward(transform.up, transform.forward);
    }

    void PositionVRCamera()
    {
        if (!vrMode || xrOrigin == null) return;

        // Reset any tracking offsets
        xrOrigin.MoveCameraToWorldLocation(transform.position);
        xrOrigin.MatchOriginUpCameraForward(transform.up, transform.forward);

        // If you want the camera at a specific offset from the submarine
        // xrOrigin.Camera.transform.localPosition = new Vector3(0, 1.6f, 0); // Example: 1.6m up
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
