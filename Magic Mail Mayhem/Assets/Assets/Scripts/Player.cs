using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkCharacterController))]
public class Player : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrain = 20f;
    [SerializeField] private float staminaRecharge = 15f;
    [SerializeField] private float staminaRecoveryDelay = 3f;

    [Header("Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private float lookSmoothing = 12f;

    private float yaw;
    private float pitch;
    private float targetYaw;
    private float targetPitch;
    private float staminaRecoveryTimer;
    private NetworkCharacterController controller;
    private bool sprintRequested;

    [Networked]
    public float Stamina { get; set; }

    private void Awake()
    {
        controller = GetComponent<NetworkCharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null)
            audioListener = GetComponentInChildren<AudioListener>(true);

        if (cameraPivot == null && playerCamera != null)
            cameraPivot = playerCamera.transform.parent;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority || Object.HasInputAuthority)
            Stamina = maxStamina;

        bool localPlayer = Object.HasInputAuthority;

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(localPlayer);

        if (audioListener != null)
            audioListener.enabled = localPlayer;

        if (localPlayer)
        {
            FindFirstObjectByType<LocalInput>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            sprintRequested = Keyboard.current.shiftKey.isPressed || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
            return;

        targetYaw += input.LookDelta.x;
        targetPitch -= input.LookDelta.y;
        targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);

        Quaternion movementRotation = Quaternion.Euler(0f, targetYaw, 0f);
        Vector3 moveDirection = movementRotation * new Vector3(input.Move.x, 0f, input.Move.y);

        if (moveDirection.sqrMagnitude > 0.001f)
            moveDirection.Normalize();

        float speed = walkSpeed;
        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool shouldSprint = (input.Sprint || sprintRequested) && isMoving;
        bool canSprint = shouldSprint && Stamina > 0f;

        if (canSprint)
        {
            speed = sprintSpeed;
        }

        if (controller != null)
        {
            controller.maxSpeed = speed;
            controller.acceleration = canSprint ? 20f : 12f;
            controller.braking = canSprint ? 18f : 12f;
        }

        if (Object.HasStateAuthority)
        {
            if (shouldSprint && Stamina > 0f)
            {
                Stamina -= staminaDrain * Runner.DeltaTime;
                Stamina = Mathf.Max(0f, Stamina);
                staminaRecoveryTimer = 0f;
            }
            else
            {
                if (staminaRecoveryTimer < staminaRecoveryDelay)
                {
                    staminaRecoveryTimer += Runner.DeltaTime;
                }
                else if (Stamina < maxStamina)
                {
                    Stamina += staminaRecharge * Runner.DeltaTime;
                    Stamina = Mathf.Min(maxStamina, Stamina);
                }
            }
        }
        
        controller.Move(moveDirection);
    }

    private void LateUpdate()
    {
        if (!Object.IsValid)
            return;

        float smoothing = Mathf.Clamp01(lookSmoothing * Time.deltaTime);
        yaw = Mathf.Lerp(yaw, targetYaw, smoothing);
        pitch = Mathf.Lerp(pitch, targetPitch, smoothing);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        else if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
