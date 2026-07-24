using UnityEngine;

public class NoticeBoardInteractable : WorldInteractable
{
    public const string TilemapTag = "NoticeBoard";

    [TextArea(3, 8)]
    [SerializeField] private string boardText =
        "Welcome, adventurers.\n\n" +
        "This gathering hub is safe ground.\n" +
        "Rest at the fountain, then head out to face the wilds.\n\n" +
        "— The Town Watch";

    public void Setup(Vector3 markerPosition)
    {
        transform.position = markerPosition;
        Configure("notice_board", "[E] Read Notice Board", TilemapTag);
    }

    public override bool ServerExecute(Player player)
    {
        return player != null && player.IsSpawned;
    }

    public override void ClientOnSuccess(Player player)
    {
        InteractionPromptUI.EnsureExists().ShowNoticeBoard(boardText);
    }
}
