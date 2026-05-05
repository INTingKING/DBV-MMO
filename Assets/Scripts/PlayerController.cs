using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    // This is the GENERATED class from your "InputSystem_Actions" asset
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;

    private Rigidbody2D rb;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Create instance of the generated input class
        inputActions = new InputSystem_Actions();
        inputActions.Enable();

        // Subscribe using the correct structure for your asset
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogError("Rigidbody2D component is missing on the Player prefab!");
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && inputActions != null)
        {
            inputActions.Player.Move.performed -= OnMovePerformed;
            inputActions.Player.Move.canceled -= OnMoveCanceled;

            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }

        base.OnNetworkDespawn();
    }

    // Input Callbacks
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
    }

    // Movement
    private void FixedUpdate()
    {
        if (!IsOwner || rb == null) return;

        rb.linearVelocity = moveInput * moveSpeed;

        // Rotate player to face movement direction (nice for top-down)
        if (moveInput.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
            rb.SetRotation(angle);
        }
    }
}