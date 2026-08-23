using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    private GameObject _promptRoot;
    private TMP_Text _promptText;
    private GameObject _boardRoot;
    private TMP_Text _boardBody;

    public static InteractionPromptUI EnsureExists()
    {
        // Unity fake-null: destroyed Instance must not be reused after disconnect.
        if (Instance != null)
            return Instance;
        return RuntimeSingleton.Ensure<InteractionPromptUI>("InteractionPromptUI");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
        SetPrompt(null);
        HideNoticeBoard();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPrompt(string text)
    {
        if (_promptRoot == null || _promptText == null)
            return;

        bool show = !string.IsNullOrEmpty(text);
        _promptRoot.SetActive(show);
        if (show)
            _promptText.text = text;
    }

    public void ShowNoticeBoard(string body)
    {
        if (_boardRoot == null || _boardBody == null)
            return;

        _boardBody.text = body;
        _boardRoot.SetActive(true);
    }

    public void HideNoticeBoard()
    {
        if (_boardRoot != null)
            _boardRoot.SetActive(false);
    }

    private void BuildUI()
    {
        UIEventSystem.Ensure();

        GameObject canvasGo = UiFactory.CreateOverlayCanvas(transform, "InteractionCanvas", 140);

        _promptRoot = UiFactory.CreatePanel("Prompt", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 120f), new Vector2(420f, 36f),
            new Color(0f, 0f, 0f, 0.65f));
        _promptText = UiFactory.CreateLabel("PromptText", _promptRoot.transform, "[E] Interact", 18f, Vector2.zero, new Vector2(400f, 28f));

        _boardRoot = UiFactory.CreatePanel("NoticeBoard", canvasGo.transform,
            UiFactory.Center, UiFactory.Center,
            Vector2.zero, new Vector2(520f, 340f),
            new Color(0.05f, 0.05f, 0.08f, 0.92f));

        UiFactory.CreateLabel("BoardTitle", _boardRoot.transform, "Notice Board", 26f, new Vector2(0f, 130f), new Vector2(480f, 36f));
        _boardBody = UiFactory.CreateLabel("BoardBody", _boardRoot.transform, "", 18f, new Vector2(0f, 10f), new Vector2(460f, 200f), TextAlignmentOptions.TopLeft);

        UiFactory.CreateButton("CloseBoard", _boardRoot.transform, "Close", new Vector2(0f, -130f), new Vector2(140f, 40f), HideNoticeBoard);
    }
}
