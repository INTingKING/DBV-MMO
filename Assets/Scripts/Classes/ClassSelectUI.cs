using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-only class picker living on the player object.
/// Uses IMGUI so EventSystem / canvas cannot hide it.
/// </summary>
[DisallowMultipleComponent]
public class ClassSelectUI : MonoBehaviour
{
    public static ClassSelectUI Instance { get; private set; }

    private PlayerClass _player;
    private bool _stylesReady;
    private GUIStyle _titleStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _boxStyle;
    private Texture2D _boxTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;

    /// <summary>Kept for old call sites; attaches picker to the local player.</summary>
    public static ClassSelectUI EnsureExists()
    {
        // Prefer attaching to local player when possible.
        PlayerClass local = FindLocalPlayerClass();
        if (local != null)
            return EnsureOnPlayer(local);

        if (Instance != null)
            return Instance;

        ClassSelectUI existing = FindFirstObjectByType<ClassSelectUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("ClassSelectUI_Pending");
        DontDestroyOnLoad(go);
        return go.AddComponent<ClassSelectUI>();
    }

    public static ClassSelectUI EnsureOnPlayer(PlayerClass player)
    {
        if (player == null)
            return EnsureExists();

        ClassSelectUI onPlayer = player.GetComponent<ClassSelectUI>();
        if (onPlayer == null)
            onPlayer = player.gameObject.AddComponent<ClassSelectUI>();

        onPlayer.Bind(player);
        Instance = onPlayer;
        return onPlayer;
    }

    public static PlayerClass FindLocalPlayerClass()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return null;

        NetworkObject obj = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        if (obj == null && nm.SpawnManager != null)
            obj = nm.SpawnManager.GetLocalPlayerObject();
        if (obj == null)
            obj = NetworkPlayers.FindObject(nm.LocalClientId);

        if (obj == null)
            return null;

        PlayerClass pc = obj.GetComponent<PlayerClass>();
        return pc != null && pc.IsSpawned ? pc : null;
    }

    private void Awake()
    {
        Instance = this;
        _player = GetComponent<PlayerClass>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DestroyTex(ref _boxTex);
        DestroyTex(ref _btnTex);
        DestroyTex(ref _btnHoverTex);
    }

    public void Bind(PlayerClass playerClass)
    {
        if (playerClass != null)
            _player = playerClass;
    }

    public void Unbind(PlayerClass playerClass)
    {
        if (_player == playerClass)
            _player = null;
    }

    private void OnGUI()
    {
        if (!ShouldDraw())
            return;

        EnsureStyles();

        // Full-screen dim
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;

        float w = 500f;
        float h = 300f;
        Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(box, GUIContent.none, _boxStyle);

        GUI.Label(new Rect(box.x, box.y + 28f, box.width, 40f), "Choose Your Class", _titleStyle);
        GUI.Label(new Rect(box.x, box.y + 72f, box.width, 30f), "Warrior or Mage — required to play", _titleStyle);

        float btnW = 190f;
        float btnH = 100f;
        float gap = 28f;
        float total = btnW * 2f + gap;
        float x0 = box.x + (box.width - total) * 0.5f;
        float y = box.y + 140f;

        if (GUI.Button(new Rect(x0, y, btnW, btnH), "WARRIOR\nMelee · Slam", _buttonStyle))
            Pick(PlayerClassType.Warrior);

        if (GUI.Button(new Rect(x0 + btnW + gap, y, btnW, btnH), "MAGE\nRanged · Firebolt", _buttonStyle))
            Pick(PlayerClassType.Mage);
    }

    private bool ShouldDraw()
    {
        // Only the owning client draws this.
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return false;

        if (_player == null)
            _player = GetComponent<PlayerClass>();

        if (_player == null)
            _player = FindLocalPlayerClass();

        if (_player == null || !_player.IsSpawned)
            return false;

        // Must be local player
        if (!_player.IsOwner && _player.OwnerClientId != nm.LocalClientId)
            return false;

        return !_player.HasSelectedClass;
    }

    private void Pick(PlayerClassType type)
    {
        if (_player == null)
            _player = FindLocalPlayerClass() ?? GetComponent<PlayerClass>();

        if (_player == null)
        {
            Debug.LogError("[ClassSelect] Pick failed: no PlayerClass.");
            return;
        }

        Debug.Log($"[ClassSelect] Picking {type} on {_player.name} IsOwner={_player.IsOwner} IsServer={_player.IsServer}");
        _player.RequestSelectClass(type);
    }

    private void EnsureStyles()
    {
        if (_stylesReady)
            return;

        _boxTex = MakeTex(new Color(0.06f, 0.07f, 0.12f, 0.95f));
        _btnTex = MakeTex(new Color(0.18f, 0.42f, 0.88f, 1f));
        _btnHoverTex = MakeTex(new Color(0.28f, 0.55f, 1f, 1f));

        _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _boxTex } };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = _btnTex },
            hover = { textColor = Color.white, background = _btnHoverTex },
            active = { textColor = Color.white, background = _btnHoverTex },
            wordWrap = true
        };

        _stylesReady = true;
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private static void DestroyTex(ref Texture2D tex)
    {
        if (tex == null)
            return;
        Destroy(tex);
        tex = null;
    }
}
