using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FountainInteractable : WorldInteractable
{
    public const string InteractableId = "fountain";
    public const string TilemapTag = "Fountain";

    [SerializeField] private float cooldownSeconds = 10f;

    private readonly Dictionary<ulong, float> _nextUseTime = new Dictionary<ulong, float>();

    public void Setup(Vector3 markerPosition)
    {
        transform.position = markerPosition;
        Configure(InteractableId, "[E] Rest at Fountain", TilemapTag);
    }

    public override bool ServerExecute(Player player)
    {
        if (player == null || !player.IsSpawned)
            return false;

        ulong clientId = player.OwnerClientId;
        float now = Time.time;
        if (_nextUseTime.TryGetValue(clientId, out float readyAt) && now < readyAt)
            return false;

        NetworkHealth health = player.GetComponent<NetworkHealth>();
        if (health == null || health.IsDead)
            return false;

        health.FullHeal();
        _nextUseTime[clientId] = now + cooldownSeconds;
        return true;
    }

    public override void ClientOnSuccess(Player player)
    {
        ChatUI.AddSystem("The fountain restores your strength.");

        if (player != null)
            FloatingChatText.Show(player.transform, "Refreshed!", 2f);
    }
}
