using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkCharacterController))]
public class Player : NetworkBehaviour
{
    public int Score = 0;
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

    [Header("Pickup")]
    [SerializeField] private float pickupRange = 2f;

    private float yaw;
    private float pitch;
    [Networked]
    public float NetworkYaw { get; set; }
    [Networked]
    public float NetworkPitch { get; set; }
    private float targetYaw;
    private float targetPitch;
    private float staminaRecoveryTimer;
    private NetworkCharacterController controller;
    private bool sprintRequested;
    private PlayerInventory inventory;

    [Networked]
    public float Stamina { get; set; }

    private void Awake()
    {
        controller = GetComponent<NetworkCharacterController>();
        inventory = GetComponent<PlayerInventory>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null)
            audioListener = GetComponentInChildren<AudioListener>(true);

        if (cameraPivot == null && playerCamera != null)
            cameraPivot = playerCamera.transform.parent;
    }

    public override void Spawned()
    {
        // if (Object.HasStateAuthority || Object.HasInputAuthority)
        if (Object.HasStateAuthority)
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
        if (!Object.IsValid)
            return;

        if (Keyboard.current != null)
        {
            sprintRequested = Keyboard.current.shiftKey.isPressed || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
            return;
        
        if (Object.HasStateAuthority)
        {
            NetworkYaw += input.LookDelta.x;
            NetworkPitch -= input.LookDelta.y;
            NetworkPitch = Mathf.Clamp(NetworkPitch, -85, 85);
        }

        Quaternion movementRotation = Quaternion.Euler(0, NetworkYaw, 0);
        Vector3 moveDirection = movementRotation * new Vector3(input.Move.x, 0f, input.Move.y);

        if (moveDirection.sqrMagnitude > 0.001f)
            moveDirection.Normalize();
        
        if (input.pickup)
        {
            TryPickupItem();
        }

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

    private void TryPickupItem()
    {
        if (inventory == null || GameManager.Instance == null)
        {
            // Debug.Log($"Pickup failed: inventory={inventory}, gameManager={GameManager.Instance}");
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        
        Collider closestCollider = null;
        float closestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            float distance = Vector3.Distance(transform.position, collider.transform.position);
            if (distance < closestDistance && (collider.gameObject.tag == "Item" || collider.gameObject.tag == "NPC"))
            {
                closestDistance = distance;
                closestCollider = collider;
            }
        }

        if (closestCollider != null)
        {
            if (closestCollider.gameObject.tag == "Item") {
                // Debug.Log($"Found closest item at distance {closestDistance}, calling RPC");
                NetworkObject networkItem = closestCollider.GetComponent<NetworkObject>();
                RPC_PickupItem(networkItem);
            } else if (closestCollider.gameObject.tag == "NPC") {

            }
        }
        else
        {
            // Debug.Log("No valid item found");
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PickupItem(NetworkObject item)
    {
        if (inventory != null && item != null)
        {
            inventory.AddItem(item);
            GameManager.Instance.ItemPicked(item);
        }
    }

    private void LateUpdate()
    {
        if (!Object.IsValid)
            return;

        float smoothing = Mathf.Clamp01(lookSmoothing * Time.deltaTime);

        yaw = Mathf.Lerp(yaw, NetworkYaw, smoothing);
        pitch = Mathf.Lerp(pitch, NetworkPitch, smoothing);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        else if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}