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
    // public GameObject inGameUI;

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
        
        // inGameUI.SetActive(true);
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
            ItemInteraction();
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

    private void ItemInteraction()
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
            if (closestCollider.CompareTag("Item") || closestCollider.transform.root.CompareTag("Item"))
            {
                NetworkObject networkItem = ResolveNetworkItem(closestCollider);

                if (networkItem != null)
                {
                    Item itemData = networkItem.GetComponent<Item>();
                    string code = itemData != null && !string.IsNullOrEmpty(itemData.itemCode) ? itemData.itemCode : networkItem.name;

                    if (inventory != null)
                    {
                        inventory.AddItem(networkItem);
                        Debug.Log($"Local pickup: {code}");
                    }

                    RPC_PickupItem(networkItem);
                }
            }
            else if (closestCollider.CompareTag("NPC") || closestCollider.transform.root.CompareTag("NPC"))
            {
                NPCDelivery npc = closestCollider.GetComponentInParent<NPCDelivery>();
                if (npc == null)
                    npc = closestCollider.GetComponent<NPCDelivery>();

                if (npc != null && inventory != null)
                {
                    bool hasItem = inventory.Contains(npc.needs);
                    Debug.Log($"Delivery check. Needed={npc.needs}, inventory has item={hasItem}");

                    if (hasItem)
                    {
                        inventory.RemoveItem(npc.needs);
                        Debug.Log($"Found in bag! Delivering {npc.needs}");

                        if (Object.HasStateAuthority)
                        {
                            GameManager.Instance.ItemDelivered(npc.GetComponent<NetworkObject>());
                            Debug.Log("Delivered");
                        }
                        else
                        {
                            // Debug.Log(
                            //     $"Player={Object.Id} " +
                            //     $"InputAuthority={Object.HasInputAuthority} " +
                            //     $"StateAuthority={Object.HasStateAuthority}"
                            // );
                            // Debug.Log("Calling RPC_DeliverItem");
                            RPC_RequestDelivery(npc.GetComponent<NetworkObject>());
                            // Debug.Log("RPC_DeliverItem called");
                        }
                    }
                }
                else if (npc != null)
                {
                    Debug.Log($"Delivery failed. Needed={npc.needs}, inventory has item={inventory?.Contains(npc.needs)}");
                }
            }
        }
        else
        {
            // Debug.Log("No valid item found");
        }
    }

    private NetworkObject ResolveNetworkItem(Collider collider)
    {
        if (collider == null)
            return null;

        Item itemComponent = collider.GetComponent<Item>()
            ?? collider.GetComponentInParent<Item>()
            ?? collider.GetComponentInChildren<Item>();

        if (itemComponent != null)
        {
            NetworkObject networkItem = itemComponent.GetComponent<NetworkObject>()
                ?? itemComponent.GetComponentInParent<NetworkObject>()
                ?? itemComponent.GetComponentInChildren<NetworkObject>();

            if (networkItem != null)
                return networkItem;
        }

        return collider.GetComponent<NetworkObject>()
            ?? collider.GetComponentInParent<NetworkObject>()
            ?? collider.GetComponentInChildren<NetworkObject>();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PickupItem(NetworkObject item)
    {
        Debug.Log($"RPC Pickup item={item}");

        if (item != null && GameManager.Instance != null)
        {
            GameManager.Instance.ItemPicked(item);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_RequestDelivery(NetworkObject npc)
    {
        Debug.Log($"RPC_RequestDelivery executed on {name} | InputAuthority={Object.HasInputAuthority} | StateAuthority={Object.HasStateAuthority}");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ItemDelivered(npc);
            Debug.Log("Delivered");
        }
        // else
        // {
        //     Debug.Log("Delivery request received by non-authority instance");
        // }
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