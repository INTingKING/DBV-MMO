using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.Tilemaps;

public class Player : NetworkBehaviour
{
    private const int MaxChatLength = 120;

    [SerializeField] private float moveSpeed = 4f;

    private Tilemap _collisionTilemap;
    private Vector2 _moveInput;
    private bool _chatBound;
    private NetworkHealth _health;
    private PlayerCombat _combat;
    private PlayerClass _playerClass;

    public Vector2 MoveInput => _moveInput;
    public bool IsTryingToMove => _moveInput.sqrMagnitude > 0.01f;

    void Awake()
    {

        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
            playerInput.enabled = false;

        _health = GetComponent<NetworkHealth>();
        _combat = GetComponent<PlayerCombat>();
        _playerClass = GetComponent<PlayerClass>();
        CacheCollisionTilemap();
    }

    public override void OnNetworkSpawn()
    {
        CacheCollisionTilemap();

        Debug.Log(
            $"[Player] Spawned. IsOwner={IsOwner}, IsClient={IsClient}, IsServer={IsServer}, " +
            $"OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.LocalClientId}",
            this);

        if (!IsOwner)
            return;

        EnsureOwnerCamera(snap: true);
        BindChat();

        PlayerClass pc = _playerClass != null ? _playerClass : GetComponent<PlayerClass>();
        if (pc != null)
            ClassSelectUI.EnsureOnPlayer(pc);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            UnbindChat();

            CameraFollow cameraFollow = FindCameraFollow();
            if (cameraFollow != null && cameraFollow.IsFollowing(transform))
                cameraFollow.ClearTarget();
        }
    }

    public override void OnDestroy()
    {
        UnbindChat();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (GameOptionsUI.IsOpen)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (_playerClass != null && !_playerClass.HasSelectedClass)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (_combat != null && _combat.IsRespawning)
        {
            _moveInput = Vector2.zero;
            return;
        }
        if (_health != null && _health.IsDead)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (_collisionTilemap == null)
        {
            CacheCollisionTilemap();
            if (_collisionTilemap == null)
            {
                _moveInput = Vector2.zero;
                return;
            }
        }

        _moveInput = ReadMoveInput();
        if (_moveInput.sqrMagnitude <= 0.01f)
            return;

        ApplyMovement(_moveInput);
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner)
            return;

        EnsureOwnerCamera(snap: false);
    }

    private void BindChat()
    {
        ChatUI chat = ChatUI.EnsureExists();
        if (chat == null || _chatBound)
            return;

        chat.OnMessageSubmit += HandleLocalChatSubmit;
        _chatBound = true;
        chat.AddMessage("System: Chat ready. Press Enter to talk.");
    }

    private void UnbindChat()
    {
        if (!_chatBound || ChatUI.Instance == null)
            return;

        ChatUI.Instance.OnMessageSubmit -= HandleLocalChatSubmit;
        _chatBound = false;
    }

    private void HandleLocalChatSubmit(string message)
    {
        if (!IsOwner || !IsSpawned)
            return;

        string sanitized = SanitizeChatMessage(message);
        if (sanitized == null)
            return;

        SendChatServerRpc(sanitized);
    }

    [ServerRpc]
    private void SendChatServerRpc(string message)
    {
        string sanitized = SanitizeChatMessage(message);
        if (sanitized == null)
            return;

        ReceiveChatClientRpc(OwnerClientId, sanitized);
    }

    [ClientRpc]
    private void ReceiveChatClientRpc(ulong senderClientId, string message)
    {
        ChatUI chat = ChatUI.EnsureExists();
        chat.AddMessage($"Player {senderClientId}: {message}");

        Transform speaker = FindPlayerTransform(senderClientId);
        if (speaker != null)
            FloatingChatText.Show(speaker, message, 3.5f);
    }

    private static Transform FindPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client?.PlayerObject != null)
        {
            return client.PlayerObject.transform;
        }

        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player != null && player.IsSpawned && player.OwnerClientId == clientId)
                return player.transform;
        }

        return null;
    }

    private static string SanitizeChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        string trimmed = message.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.Length > MaxChatLength)
            trimmed = trimmed.Substring(0, MaxChatLength);

        return trimmed;
    }

    private void CacheCollisionTilemap()
    {
        if (_collisionTilemap != null)
            return;

        GameObject collisionGO = GameObject.FindWithTag("Collision");
        if (collisionGO != null)
            _collisionTilemap = collisionGO.GetComponent<Tilemap>();
        else
            Debug.LogError("Could not find Tilemap with tag 'Collision'!");
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 move = ReadFromKeyboard(Keyboard.current);
        if (move.sqrMagnitude > 0.01f)
            return move;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Keyboard keyboard)
            {
                move = ReadFromKeyboard(keyboard);
                if (move.sqrMagnitude > 0.01f)
                    return move;
            }
        }

        return Vector2.zero;
    }

    private static Vector2 ReadFromKeyboard(Keyboard keyboard)
    {
        if (keyboard == null)
            return Vector2.zero;

        Vector2 move = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
        return Vector2.ClampMagnitude(move, 1f);
    }

    private void ApplyMovement(Vector2 input)
    {
        float speed = moveSpeed * Time.deltaTime;
        Vector2 direction = input.normalized;

        Vector3 proposed = transform.position + new Vector3(direction.x * speed, direction.y * speed, 0f);

        if (Overlap(proposed) == null)
        {
            transform.position = proposed;
            return;
        }

        Vector2 dirClock = RotateAndNormalizeDirection(direction, 45f, true);
        Vector3 posClock = transform.position + new Vector3(dirClock.x * speed, dirClock.y * speed, 0f);

        Vector2 dirCounter = RotateAndNormalizeDirection(direction, 45f, false);
        Vector3 posCounter = transform.position + new Vector3(dirCounter.x * speed, dirCounter.y * speed, 0f);

        if (Overlap(posClock) == null)
            transform.position = posClock;
        else if (Overlap(posCounter) == null)
            transform.position = posCounter;
    }

    private void EnsureOwnerCamera(bool snap)
    {
        CameraFollow cameraFollow = FindCameraFollow();
        if (cameraFollow == null)
            return;

        if (!cameraFollow.enabled)
            cameraFollow.enabled = true;

        if (!cameraFollow.IsFollowing(transform))
            cameraFollow.SetTarget(transform, snap);
    }

    private static CameraFollow FindCameraFollow()
    {
        if (CameraFollow.Instance != null)
            return CameraFollow.Instance;

        if (Camera.main != null)
        {
            CameraFollow onMain = Camera.main.GetComponent<CameraFollow>();
            if (onMain != null)
                return onMain;
        }

        return FindFirstObjectByType<CameraFollow>();
    }

    private static Vector2 RotateAndNormalizeDirection(Vector2 direction, float angle, bool clockwise)
    {
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2 rotated = clockwise
            ? new Vector2(direction.x * cos + direction.y * sin, -direction.x * sin + direction.y * cos)
            : new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);

        return rotated.normalized;
    }

    private TileBase Overlap(Vector3 proposedPosition)
    {
        if (_collisionTilemap == null)
            return null;

        return _collisionTilemap.GetTile(_collisionTilemap.WorldToCell(proposedPosition));
    }
}
