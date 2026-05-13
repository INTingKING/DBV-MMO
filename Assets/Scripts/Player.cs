using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class Player : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Vector2 _moveInput;

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        _moveInput = context.ReadValue<Vector2>();
        Debug.Log($"OnMove called! Input = {_moveInput} | IsOwner = {IsOwner}");
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 direction = new Vector3(_moveInput.x, _moveInput.y, 0f).normalized;
            Vector3 proposedPosition = transform.position + direction * (moveSpeed * Time.deltaTime);

            // TODO: Add collision check here before moving
            // if (!WouldCollide(proposedPosition))
            transform.position = proposedPosition;

            Debug.Log($"Moving! Position = {transform.position}");
        }
    }
}
