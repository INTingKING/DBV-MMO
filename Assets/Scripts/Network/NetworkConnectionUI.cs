using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NetworkConnectionUI : MonoBehaviour
{
    public static NetworkConnectionUI Instance { get; private set; }

    [SerializeField] private string defaultAddress = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 42069;

    private GameObject _panelRoot;
    private GameObject _connectedRoot;
    private TMP_InputField _addressField;
    private TMP_InputField _portField;
    private TMP_Text _statusText;
    private TMP_Text _connectedStatusText;
    private bool _subscribed;

    public static NetworkConnectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        NetworkConnectionUI existing = FindFirstObjectByType<NetworkConnectionUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("NetworkConnectionUI");
        return go.AddComponent<NetworkConnectionUI>();
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
        SetStatus("Disconnected");
        ShowConnectionPanel(true);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!_subscribed)
            TrySubscribe();

        RefreshVisibilityFromNetworkState();
    }

    public void HideForDedicatedServer()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        if (_connectedRoot != null)
            _connectedRoot.SetActive(false);
    }

    private void TrySubscribe()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager nm = NetworkManager.Singleton;

        NetworkConfigNormalizer.Apply(nm);

        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;
        nm.OnServerStarted += HandleServerStarted;
        nm.OnTransportFailure += HandleTransportFailure;
        _subscribed = true;

        DisableDefaultNetworkHud();
        RefreshVisibilityFromNetworkState();
    }

    private void Unsubscribe()
    {
        if (!_subscribed || NetworkManager.Singleton == null)
            return;

        NetworkManager nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback -= HandleClientConnected;
        nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        nm.OnServerStarted -= HandleServerStarted;
        nm.OnTransportFailure -= HandleTransportFailure;
        _subscribed = false;
    }

    private static void DisableDefaultNetworkHud()
    {

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "NetworkManagerHUD" || typeName == "UnityTransportHUD")
                behaviour.enabled = false;
        }
    }

    private void RefreshVisibilityFromNetworkState()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            ShowConnectionPanel(true);
            SetStatus("Waiting for NetworkManager...");
            return;
        }

        bool active = nm.IsServer || nm.IsClient;
        ShowConnectionPanel(!active);

        if (!active)
            return;

        if (nm.IsHost)
            SetConnectedStatus("Connected as Host");
        else if (nm.IsServer)
            SetConnectedStatus("Running as Dedicated Server");
        else if (nm.IsClient)
            SetConnectedStatus($"Connected as Client (id {nm.LocalClientId})");
    }

    private void ShowConnectionPanel(bool showConnect)
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(showConnect);
        if (_connectedRoot != null)
            _connectedRoot.SetActive(!showConnect);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
        Debug.Log($"[NetworkUI] {message}");
    }

    private void SetConnectedStatus(string message)
    {
        if (_connectedStatusText != null)
            _connectedStatusText.text = message;
    }

    private void OnClickHost()
    {
        if (!TryPrepareTransport(listen: true))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus("Starting Host...");
        bool ok = NetworkManager.Singleton.StartHost();
        SetStatus(ok ? "Host starting..." : "StartHost failed.");
    }

    private void OnClickServer()
    {
        if (!TryPrepareTransport(listen: true))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus("Starting Dedicated Server...");
        bool ok = NetworkManager.Singleton.StartServer();
        SetStatus(ok ? "Server starting..." : "StartServer failed.");
    }

    private void OnClickClient()
    {
        if (!TryPrepareTransport(listen: false))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus($"Connecting to {ReadAddress()}:{ReadPort()}...");
        bool ok = NetworkManager.Singleton.StartClient();
        SetStatus(ok ? "Client connecting..." : "StartClient failed.");
    }

    private void OnClickDisconnect()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        if (nm.IsHost || nm.IsServer || nm.IsClient)
            nm.Shutdown();

        SetStatus("Disconnected");
        ShowConnectionPanel(true);
    }

    private bool TryPrepareTransport(bool listen)
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManager missing in scene.");
            return false;
        }

        NetworkManager nm = NetworkManager.Singleton;

        NetworkConfigNormalizer.Apply(nm);

        UnityTransport transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("UnityTransport missing on NetworkManager.");
            return false;
        }

        string address = ReadAddress();
        ushort port = ReadPort();

        if (listen)
        {

            transport.SetConnectionData(address, port, "0.0.0.0");
        }
        else
        {
            transport.SetConnectionData(address, port);
        }

        ulong hash = nm.NetworkConfig.GetConfig(cache: false);
        Debug.Log($"[NetworkUI] Pre-start ConfigHash={hash} (host and client must match)");

        return true;
    }

    private string ReadAddress()
    {
        if (_addressField == null || string.IsNullOrWhiteSpace(_addressField.text))
            return defaultAddress;
        return _addressField.text.Trim();
    }

    private ushort ReadPort()
    {
        if (_portField == null || !ushort.TryParse(_portField.text, out ushort port))
            return defaultPort;
        return port;
    }

    private void HandleClientConnected(ulong clientId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && clientId == nm.LocalClientId)
            SetStatus($"Client connected (id {clientId})");
        else if (nm != null && nm.IsServer)
            SetStatus($"Client {clientId} joined");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("Disconnected");
            ShowConnectionPanel(true);
            return;
        }

        if (!nm.IsServer && !nm.IsClient)
        {
            SetStatus($"Disconnected (client {clientId})");
            ShowConnectionPanel(true);
        }
        else if (nm.IsServer)
        {
            SetStatus($"Client {clientId} left");
        }
    }

    private void HandleServerStarted()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsHost)
            SetStatus("Host ready");
        else
            SetStatus("Dedicated server ready");
    }

    private void HandleTransportFailure()
    {
        SetStatus("Transport failure");
        ShowConnectionPanel(true);
    }

    #region UI Build

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("ConnectionCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _panelRoot = CreatePanel("ConnectPanel", canvasGo.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(460f, 360f),
            new Color(0f, 0f, 0f, 0.82f));

        CreateLabel("Title", _panelRoot.transform, "Multiplayer", 28f, new Vector2(0f, 140f), new Vector2(400f, 40f));

        CreateLabel("AddressLabel", _panelRoot.transform, "Address", 16f, new Vector2(0f, 90f), new Vector2(400f, 24f));
        _addressField = CreateInputField("AddressInput", _panelRoot.transform, defaultAddress, new Vector2(0f, 55f), new Vector2(360f, 36f));

        CreateLabel("PortLabel", _panelRoot.transform, "Port", 16f, new Vector2(0f, 10f), new Vector2(400f, 24f));
        _portField = CreateInputField("PortInput", _panelRoot.transform, defaultPort.ToString(), new Vector2(0f, -25f), new Vector2(360f, 36f));

        CreateButton("HostButton", _panelRoot.transform, "Host", new Vector2(-120f, -90f), new Vector2(110f, 40f), OnClickHost);
        CreateButton("ServerButton", _panelRoot.transform, "Server", new Vector2(0f, -90f), new Vector2(110f, 40f), OnClickServer);
        CreateButton("ClientButton", _panelRoot.transform, "Client", new Vector2(120f, -90f), new Vector2(110f, 40f), OnClickClient);

        _statusText = CreateLabel("Status", _panelRoot.transform, "Disconnected", 16f, new Vector2(0f, -145f), new Vector2(420f, 28f));

        _connectedRoot = CreatePanel("ConnectedPanel", canvasGo.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -20f), new Vector2(320f, 90f),
            new Color(0f, 0f, 0f, 0.7f));
        RectTransform connectedRt = _connectedRoot.GetComponent<RectTransform>();
        connectedRt.pivot = new Vector2(1f, 1f);

        _connectedStatusText = CreateLabel("ConnectedStatus", _connectedRoot.transform, "Connected", 16f, new Vector2(0f, 18f), new Vector2(280f, 28f));
        CreateButton("DisconnectButton", _connectedRoot.transform, "Disconnect", new Vector2(0f, -20f), new Vector2(140f, 36f), OnClickDisconnect);

        _connectedRoot.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string value, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(go.transform, false);
        RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
        Stretch(textAreaRt, 8f);
        textArea.AddComponent<RectMask2D>();

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textArea.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(textArea.transform, false);
        Stretch(placeholderGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = value;
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        if (TMP_Settings.defaultFontAsset != null)
            placeholder.font = TMP_Settings.defaultFontAsset;

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textViewport = textAreaRt;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.text = value;
        field.pointSize = 18f;
        return field;
    }

    private static void CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.85f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    #endregion
}
