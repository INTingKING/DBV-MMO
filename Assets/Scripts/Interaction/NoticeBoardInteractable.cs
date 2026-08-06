using UnityEngine;

public class NoticeBoardInteractable : WorldInteractable
{
    public const string TilemapTag = "NoticeBoard";

    [TextArea(3, 8)]
    [SerializeField] private string boardText =
        "Never gonna give you up\n" +
        "Never gonna let you down\n" +
        "Never gonna run around and desert you\n" +
        "Never gonna make you cry\n" +
        "Never gonna say goodbye\n" +
        "Never gonna tell a lie and hurt you";

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
