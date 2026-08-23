using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    private Player _player;
    private PlayerClass _playerClass;
    private PlayerCombat _combat;
    private NetworkHealth _health;
    private PlayerInventory _inventory;
    private WorldInteractable _current;
    private LootDrop _currentLoot;

    public override void OnNetworkSpawn()
    {
        _player = GetComponent<Player>();
        _playerClass = GetComponent<PlayerClass>();
        _combat = GetComponent<PlayerCombat>();
        _health = GetComponent<NetworkHealth>();
        _inventory = GetComponent<PlayerInventory>();

        if (IsOwner)
        {
            // Always (re)create prompt UI after re-host; previous Instance may be destroyed.
            InteractionPromptUI.EnsureExists();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && InteractionPromptUI.Instance != null)
            InteractionPromptUI.Instance.SetPrompt(null);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (!GameplayInput.CanOwnerAct(true, true, _playerClass, _combat, _health))
        {
            ClearPrompt();
            return;
        }

        _current = WorldInteractable.FindAtPosition(transform.position);
        _currentLoot = null;

        if (_current != null)
        {
            Player p = _player != null ? _player : GetComponent<Player>();
            InteractionPromptUI.EnsureExists().SetPrompt(_current.GetPromptFor(p));
        }
        else
        {
            _currentLoot = LootDrop.FindNearestFor(OwnerClientId, transform.position);
            if (_currentLoot != null)
                InteractionPromptUI.EnsureExists().SetPrompt(_currentLoot.GetPickupPrompt());
            else
                ClearPrompt();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard.eKey.wasPressedThisFrame)
            return;

        if (_current != null)
            InteractServerRpc(_current.Id);
        else if (_currentLoot != null && _currentLoot.NetworkObject != null)
            PickupLootServerRpc(_currentLoot.NetworkObjectId);
    }

    private void ClearPrompt()
    {
        _current = null;
        _currentLoot = null;
        if (InteractionPromptUI.Instance != null)
            InteractionPromptUI.Instance.SetPrompt(null);
    }

    [ServerRpc]
    private void PickupLootServerRpc(ulong lootNetworkObjectId)
    {
        if (!GameplayInput.CanAct(_playerClass, _combat, _health))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(lootNetworkObjectId, out NetworkObject lootNet))
            return;

        LootDrop drop = lootNet.GetComponent<LootDrop>();
        if (drop == null)
            return;

        PlayerInventory inv = _inventory != null ? _inventory : GetComponent<PlayerInventory>();
        if (inv == null)
            return;

        if (!drop.ServerTryPickup(inv))
            PickupLootFailedClientRpc();
    }

    [ClientRpc]
    private void PickupLootFailedClientRpc()
    {
        if (!IsOwner)
            return;

        ChatUI.AddSystem("Could not pick up that loot.");
    }

    [ServerRpc]
    private void InteractServerRpc(string interactableId)
    {
        if (string.IsNullOrEmpty(interactableId))
            return;

        if (!GameplayInput.CanAct(_playerClass, _combat, _health))
            return;

        if (!WorldInteractable.TryGet(interactableId, out WorldInteractable interactable))
            return;

        if (!interactable.IsInRange(transform.position))
            return;

        Player player = _player != null ? _player : GetComponent<Player>();
        if (player == null)
            return;

        if (interactableId == QuestNpcInteractable.InteractableId)
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

        if (interactableId == FountainInteractable.InteractableId)
            ChatUI.AddSystem("The fountain needs a moment to refill.");
        else if (interactableId == "need_class")
            ChatUI.AddSystem("Choose a class before taking the quest.");
    }
}
