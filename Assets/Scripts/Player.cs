using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.Tilemaps;

public class Player : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private Vector2 _moveInput;
    private Tilemap _collisionTilemap;

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput != null) _moveAction = _playerInput.actions["Move"];

        GameObject collisionGO = GameObject.FindWithTag("Collision");
        if (collisionGO != null)
        {
            _collisionTilemap = collisionGO.GetComponent<Tilemap>();
        }
        else
        {
            Debug.LogError("Could not find Tilemap with tag 'Collision'!");
        }
    }

    private void Update()
    {
        if (_moveAction == null || _collisionTilemap == null) return;

        _moveInput = _moveAction.ReadValue<Vector2>();
        if(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) if(!IsOwner) return;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            Movement();
            // Debug.Log($"Moving! Position = {transform.position}");
        }
    }
    private int Movement()
    {
        float speed = moveSpeed * Time.deltaTime;

        Vector2 direction = new Vector2(_moveInput.x, _moveInput.y).normalized;
        Vector3 proposedPosition = transform.position + new Vector3(direction.x * speed, direction.y * speed, 0);
        
        if (Overlap(proposedPosition) == null)
        {
            transform.position = proposedPosition;
            return 0;
        }
        
        Vector2 directionClock = RotateAndNormalizeDirection(direction, 45f, true);
        Vector3 proposedPositionClock = transform.position + new Vector3(directionClock.x * speed, directionClock.y * speed, 0)*Mathf.Sqrt(1);

        Vector2 directionCounterclock = RotateAndNormalizeDirection(direction, 45f, false);
        Vector3 proposedPositionCounterclock = transform.position + new Vector3(directionCounterclock.x * speed, directionCounterclock.y * speed, 0)*Mathf.Sqrt(1);
        
        if(Overlap(proposedPositionClock) == null || Overlap(proposedPositionCounterclock) == null)
        {
            if(Overlap(proposedPositionClock) == null && Overlap(proposedPositionCounterclock) == null) 
            {
                Vector3 proposedPositionQuartered = transform.position + new Vector3(direction.x * speed, direction.y * speed, 0)/4;
                if (Overlap(proposedPositionQuartered) == null) transform.position = proposedPositionQuartered;
                return 0;
            }
            else if(Overlap(proposedPositionClock) == null)
            {
                transform.position = proposedPositionClock; 
            } 
            else if(Overlap(proposedPositionCounterclock) == null) 
            {
                transform.position = proposedPositionCounterclock;
            } 
        }
        return 0;
    }

    private Vector2 RotateAndNormalizeDirection(Vector2 direction, float angle, bool clockwise)
    {
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2 rotated;

        if (clockwise) rotated = new Vector2(direction.x * cos + direction.y * sin, -direction.x * sin + direction.y * cos).normalized;
        else rotated = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
        
        return rotated;
    }

    private TileBase Overlap(Vector3 proposedPosition)
    {
        return _collisionTilemap.GetTile(_collisionTilemap.WorldToCell(proposedPosition));
    }
}
