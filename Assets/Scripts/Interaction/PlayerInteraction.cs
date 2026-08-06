using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    private Player _player;
    private PlayerClass _playerClass;
    private PlayerCombat _combat;
    private NetworkHealth _health;
    private WorldInteractable _current;

    public override void OnNetworkSpawn()
    {
        _player = GetComponent<Player>();
        _playerClass = GetComponent<PlayerClass>();
        _combat = GetComponent<PlayerCombat>();
        _health = GetComponent<NetworkHealth>();

        if (IsOwner)
            InteractionPromptUI.EnsureExists();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            InteractionPromptUI.EnsureExists().SetPrompt(null);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
        {
            ClearPrompt();
            return;
        }

        if (_playerClass != null && !_playerClass.HasSelectedClass)
        {
            ClearPrompt();
            return;
        }

        if (_combat != null && _combat.IsRespawning)
        {
            ClearPrompt();
            return;
        }

        if (_health != null && _health.IsDead)
        {
            ClearPrompt();
            return;
        }

        _current = WorldInteractable.FindAtPosition(transform.position);
        if (_current != null)
        {
            Player p = _player != null ? _player : GetComponent<Player>();
            InteractionPromptUI.EnsureExists().SetPrompt(_current.GetPromptFor(p));
        }
        else
            ClearPrompt();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame && _current != null)
            InteractServerRpc(_current.Id);
    }

    private void ClearPrompt()
    {
        _current = null;
        if (InteractionPromptUI.Instance != null)
            InteractionPromptUI.Instance.SetPrompt(null);
    }

    [ServerRpc]
    private void InteractServerRpc(string interactableId)
    {
        if (string.IsNullOrEmpty(interactableId))
            return;

        if (_playerClass != null && !_playerClass.HasSelectedClass)
            return;

        if (_combat != null && _combat.IsRespawning)
            return;

        if (_health != null && _health.IsDead)
            return;

        if (!WorldInteractable.TryGet(interactableId, out WorldInteractable interactable))
            return;

        if (!interactable.IsInRange(transform.position))
            return;

        Player player = _player != null ? _player : GetComponent<Player>();
        if (player == null)
            return;

        if (interactableId == "quest_npc")
        {
            PlayerClass pc = GetComponent<PlayerClass>();
            if (pc == null || !pc.HasSelectedClass)
            {
                InteractFailedClientRpc("need_class");
                return;
            }
        }

        if (!interactable.ServerExecute(player))
        {
            InteractFailedClientRpc(interactableId);
            return;
        }

        InteractSuccessClientRpc(interactableId);
    }

    [ClientRpc]
    private void InteractSuccessClientRpc(string interactableId)
    {
        if (!IsOwner)
            return;

        if (!WorldInteractable.TryGet(interactableId, out WorldInteractable interactable))
            return;

        Player player = _player != null ? _player : GetComponent<Player>();
        interactable.ClientOnSuccess(player);
    }

    [ClientRpc]
    private void InteractFailedClientRpc(string interactableId)
    {
        if (!IsOwner)
            return;

        if (ChatUI.Instance == null)
            return;

        if (interactableId == "fountain")
            ChatUI.Instance.AddMessage("System: The fountain needs a moment to refill.");
        else if (interactableId == "need_class")
            ChatUI.Instance.AddMessage("System: Choose a class before taking the quest.");
    }
}
