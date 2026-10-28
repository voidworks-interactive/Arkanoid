using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VoidWorksOverlay : MonoBehaviour
{
    private const string LayoutPrefix = "VoidWorksOverlay.Widget.";
    private static VoidWorksOverlay instance;

    private GameObject overlay;
    private RectTransform workspace;
    private RectTransform chatWidget;
    private RectTransform friendsWidget;
    private Button showChatButton;
    private Button showFriendsButton;
    private RectTransform overlayTip;
    private CanvasGroup overlayTipCanvasGroup;
    private GameObject storeWindow;
    private RectTransform storeWindowRect;
    private Vector2 storeRestorePosition;
    private Vector2 storeRestoreSize;
    private bool storeMaximized;
    private bool isVisible;
    private Font overlayFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOverlay()
    {
        if (instance != null)
        {
            return;
        }

        var overlayObject = new GameObject("VoidWorksOverlay");
        DontDestroyOnLoad(overlayObject);
        instance = overlayObject.AddComponent<VoidWorksOverlay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        overlayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildOverlay();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("VoidWorksOverlayEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
            (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
        var f2Pressed = Input.GetKeyDown(KeyCode.F2) || (keyboard != null && keyboard.f2Key.wasPressedThisFrame);

        if (f2Pressed && shiftHeld)
        {
            SetVisible(!isVisible);
        }

        var escapePressed = Input.GetKeyDown(KeyCode.Escape) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
        if (isVisible && escapePressed)
        {
            SetVisible(false);
        }
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("VoidWorksOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        overlay = CreateRect("OverlayBackdrop", canvasObject.transform, ColorFromHex("#050914", 0.70f), true);
        Stretch(overlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var backdropButton = overlay.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(() => SetVisible(false));

        var panel = CreateRect("OverlayPanel", overlay.transform, ColorFromHex("#0B1020", 0.99f), true);
        Stretch(panel.GetComponent<RectTransform>(), 10f, 10f, 10f, 10f);
        AddOutline(panel, ColorFromHex("#536DFE"), 2f);

        var header = CreateRect("Header", panel.transform, ColorFromHex("#0B1020"), false);
        AnchorTop(header.GetComponent<RectTransform>(), 0f, 0f, 0f, 76f);
        AddText("VoidWorks Overlay", header.transform, 12f, -8f, 260f, 20f, 11, ColorFromHex("#AEB9D8"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AddText("Steam Chat", header.transform, 12f, -29f, 260f, 30f, 20, ColorFromHex("#EEF2FF"), TextAnchor.MiddleLeft, FontStyle.Bold);

        var right = CreateRect("HeaderActions", header.transform, Color.clear, false);
        var rightRect = right.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.pivot = new Vector2(1f, 0.5f);
        rightRect.sizeDelta = new Vector2(430f, 0f);
        rightRect.anchoredPosition = new Vector2(-12f, 0f);
        AddButton("Steam Store", right.transform, 0f, 0f, 112f, 34f, OpenStoreWindow);
        showChatButton = AddButton("Hide Chat", right.transform, 118f, 0f, 96f, 34f, ToggleChatWidget);
        showFriendsButton = AddButton("Hide Friends", right.transform, 220f, 0f, 108f, 34f, ToggleFriendsWidget);
        AddButton("×", right.transform, 342f, 0f, 40f, 34f, () => SetVisible(false), 22);

        AddText("Use the Steam client for friends and messaging. Widgets can be dragged, closed, and reopened here.", panel.transform,
            12f, -82f, 900f, 34f, 13, ColorFromHex("#CBD5E1"), TextAnchor.MiddleLeft);

        var workspaceObject = CreateRect("Workspace", panel.transform, ColorFromHex("#111827"), true);
        workspace = workspaceObject.GetComponent<RectTransform>();
        workspace.anchorMin = Vector2.zero;
        workspace.anchorMax = Vector2.one;
        workspace.offsetMin = new Vector2(8f, 8f);
        workspace.offsetMax = new Vector2(-8f, -124f);

        chatWidget = CreateWidget("Steam Chat", new Vector2(18f, -18f), new Vector2(360f, 220f),
            "Click below to open Steam Chat in the official Steam client.",
            "Open Steam Chat", () => OpenSteamLink("steam://open/friends"));
        friendsWidget = CreateWidget("Steam Friends", new Vector2(396f, -18f), new Vector2(320f, 190f),
            "Click below to open your friends list in the official Steam client.",
            "Open Steam Friends", () => OpenSteamLink("steam://open/friends"));

        BuildStoreWindow(panel.transform);
        SetButtonLabel(showChatButton, chatWidget.gameObject.activeSelf ? "Hide Chat" : "Show Chat");
        SetButtonLabel(showFriendsButton, friendsWidget.gameObject.activeSelf ? "Hide Friends" : "Show Friends");
        BuildOverlayTip(canvasObject.transform);
        SetVisible(false);
        StartCoroutine(ShowOverlayTip());
    }

    private void BuildOverlayTip(Transform parent)
    {
        var tip = CreateRect("OverlayTip", parent, ColorFromHex("#0B1020", 0.98f), false);
        overlayTip = tip.GetComponent<RectTransform>();
        SetTopLeftRect(overlayTip, new Vector2(-300f, -56f), new Vector2(270f, 78f));
        AddOutline(tip, ColorFromHex("#536DFE"), 1f);
        overlayTipCanvasGroup = tip.AddComponent<CanvasGroup>();
        overlayTipCanvasGroup.alpha = 0f;
        overlayTipCanvasGroup.interactable = false;
        overlayTipCanvasGroup.blocksRaycasts = false;
        AddText("VoidWorks Overlay", tip.transform, 12f, -8f, 246f, 22f, 13,
            ColorFromHex("#EEF2FF"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AddText("Press Shift+F2 to open the overlay.", tip.transform, 12f, -32f, 246f, 36f, 13,
            ColorFromHex("#CBD5E1"), TextAnchor.MiddleLeft);
    }

    private IEnumerator ShowOverlayTip()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        yield return AnimateOverlayTip(new Vector2(12f, -56f), 1f);
        yield return new WaitForSecondsRealtime(3.4f);
        yield return AnimateOverlayTip(new Vector2(-300f, -56f), 0f);
    }

    private IEnumerator AnimateOverlayTip(Vector2 targetPosition, float targetAlpha)
    {
        const float duration = 0.35f;
        var startPosition = overlayTip.anchoredPosition;
        var startAlpha = overlayTipCanvasGroup.alpha;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            overlayTip.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            overlayTipCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        overlayTip.anchoredPosition = targetPosition;
        overlayTipCanvasGroup.alpha = targetAlpha;
    }

    private RectTransform CreateWidget(string title, Vector2 position, Vector2 size, string message, string actionLabel, Action action)
    {
        var widget = CreateRect(title.Replace(" ", "") + "Widget", workspace, ColorFromHex("#111A30"), true);
        var rect = widget.GetComponent<RectTransform>();
        SetTopLeftRect(rect, position, size);
        AddOutline(widget, ColorFromHex("#536DFE", 0.85f), 1f);

        var header = CreateRect("WidgetHeader", widget.transform, ColorFromHex("#1A2541"), true);
        AnchorTop(header.GetComponent<RectTransform>(), 0f, 0f, 0f, 38f);
        AddText(title, header.transform, 10f, 0f, 230f, 38f, 13, ColorFromHex("#EEF2FF"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AddButton("×", header.transform, -40f, 0f, 34f, 30f, () => SetWidgetOpen(rect, title == "Steam Chat" ? "Chat" : "Friends", false), 20,
            true);
        AddDragHandle(header, rect, () => SaveWidgetLayout(rect, title == "Steam Chat" ? "Chat" : "Friends"));

        var content = CreateRect("WidgetContent", widget.transform, Color.clear, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(12f, 12f);
        contentRect.offsetMax = new Vector2(-12f, -48f);
        AddText(message, content.transform, 0f, 0f, 0f, 0f, 13, ColorFromHex("#AEB9D8"), TextAnchor.UpperLeft, FontStyle.Normal, true);
        var actionButton = AddButton(actionLabel, content.transform, 0f, 0f, 190f, 36f, action);
        var actionRect = actionButton.GetComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(0f, 0f);
        actionRect.anchorMax = new Vector2(0f, 0f);
        actionRect.pivot = new Vector2(0f, 0f);
        actionRect.anchoredPosition = Vector2.zero;

        LoadWidgetLayout(rect, title == "Steam Chat" ? "Chat" : "Friends");
        return rect;
    }

    private void BuildStoreWindow(Transform parent)
    {
        storeWindow = CreateRect("SteamStoreWindow", parent, ColorFromHex("#111827"), true);
        storeWindowRect = storeWindow.GetComponent<RectTransform>();
        SetTopLeftRect(storeWindowRect, new Vector2(40f, -82f), new Vector2(700f, 430f));
        AddOutline(storeWindow, ColorFromHex("#536DFE"), 2f);
        storeWindow.SetActive(false);

        var header = CreateRect("SteamStoreHeader", storeWindow.transform, ColorFromHex("#1A2541"), true);
        AnchorTop(header.GetComponent<RectTransform>(), 0f, 0f, 0f, 42f);
        AddText("Steam Store", header.transform, 10f, 0f, 220f, 42f, 14, ColorFromHex("#EEF2FF"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AddButton("□", header.transform, -90f, 0f, 38f, 32f, ToggleStoreMaximize, 16, true);
        AddButton("×", header.transform, -46f, 0f, 34f, 32f, () => storeWindow.SetActive(false), 20, true);
        AddDragHandle(header, storeWindowRect, null);

        AddText("Click below to open the Steam Store in the official Steam client.",
            storeWindow.transform, 20f, -62f, 600f, 80f, 14, ColorFromHex("#CBD5E1"), TextAnchor.MiddleLeft, FontStyle.Normal, true);
        AddButton("Open Steam Store", storeWindow.transform, 20f, -160f, 180f, 38f,
            () => OpenSteamLink("steam://store"));
    }

    private void ToggleStoreMaximize()
    {
        if (!storeMaximized)
        {
            storeRestorePosition = storeWindowRect.anchoredPosition;
            storeRestoreSize = storeWindowRect.sizeDelta;
            storeWindowRect.anchorMin = Vector2.zero;
            storeWindowRect.anchorMax = Vector2.one;
            storeWindowRect.pivot = new Vector2(0.5f, 0.5f);
            storeWindowRect.offsetMin = Vector2.zero;
            storeWindowRect.offsetMax = Vector2.zero;
        }
        else
        {
            SetTopLeftRect(storeWindowRect, storeRestorePosition, storeRestoreSize);
            ClampToParent(storeWindowRect);
        }

        storeMaximized = !storeMaximized;
    }

    private void ToggleChatWidget()
    {
        ToggleWidget(chatWidget, "Chat", showChatButton);
    }

    private void ToggleFriendsWidget()
    {
        ToggleWidget(friendsWidget, "Friends", showFriendsButton);
    }

    private void ToggleWidget(RectTransform widget, string key, Button button)
    {
        var isOpen = !widget.gameObject.activeSelf;
        SetWidgetOpen(widget, key, isOpen);
        SetButtonLabel(button, isOpen ? "Hide " + key : "Show " + key);
    }

    private void SetWidgetOpen(RectTransform widget, string key, bool isOpen)
    {
        widget.gameObject.SetActive(isOpen);
        SaveWidgetLayout(widget, key);
        var button = key == "Chat" ? showChatButton : showFriendsButton;
        if (button != null)
        {
            SetButtonLabel(button, isOpen ? "Hide " + key : "Show " + key);
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (overlay != null)
        {
            overlay.SetActive(visible);
        }

    }

    private void OpenStoreWindow()
    {
        storeWindow.SetActive(true);
        storeWindow.transform.SetAsLastSibling();
        ClampToParent(storeWindowRect);
    }

    private static void OpenSteamLink(string url)
    {
        try
        {
            Application.OpenURL(url);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not open Steam: " + exception.Message);
        }
    }

    private void AddDragHandle(GameObject handle, RectTransform target, Action onDrop)
    {
        var dragHandle = handle.AddComponent<VoidWorksOverlayDragHandle>();
        dragHandle.Configure(target, onDrop, () => target.SetAsLastSibling());
    }

    private void LoadWidgetLayout(RectTransform widget, string key)
    {
        var prefix = LayoutPrefix + key + ".";
        if (PlayerPrefs.HasKey(prefix + "X"))
        {
            widget.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(prefix + "X"), PlayerPrefs.GetFloat(prefix + "Y"));
        }

        if (PlayerPrefs.HasKey(prefix + "Open"))
        {
            widget.gameObject.SetActive(PlayerPrefs.GetInt(prefix + "Open") == 1);
        }
    }

    private void SaveWidgetLayout(RectTransform widget, string key)
    {
        if (widget == null)
        {
            return;
        }

        var prefix = LayoutPrefix + key + ".";
        PlayerPrefs.SetFloat(prefix + "X", widget.anchoredPosition.x);
        PlayerPrefs.SetFloat(prefix + "Y", widget.anchoredPosition.y);
        PlayerPrefs.SetInt(prefix + "Open", widget.gameObject.activeSelf ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ClampToParent(RectTransform rect)
    {
        var parentRect = rect.parent as RectTransform;
        if (parentRect == null || rect.anchorMin != new Vector2(0f, 1f) || rect.anchorMax != new Vector2(0f, 1f))
        {
            return;
        }

        var maxX = Mathf.Max(0f, parentRect.rect.width - rect.rect.width);
        var maxY = Mathf.Max(0f, parentRect.rect.height - rect.rect.height);
        rect.anchoredPosition = new Vector2(Mathf.Clamp(rect.anchoredPosition.x, 0f, maxX),
            Mathf.Clamp(rect.anchoredPosition.y, -maxY, 0f));
    }

    private GameObject CreateRect(string name, Transform parent, Color color, bool raycastTarget)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return item;
    }

    private Text AddText(string value, Transform parent, float x, float y, float width, float height, int size,
        Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal, bool stretch = false)
    {
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        var text = textObject.GetComponent<Text>();
        text.font = overlayFont;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-width, -height);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        return text;
    }

    private Button AddButton(string label, Transform parent, float x, float y, float width, float height, Action action,
        int fontSize = 13, bool alignRight = false)
    {
        var buttonObject = CreateRect("Button_" + label, parent, ColorFromHex("#536DFE"), true);
        var rect = buttonObject.GetComponent<RectTransform>();
        if (alignRight)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
        }
        rect.sizeDelta = new Vector2(width, height);

        var button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => action());
        AddText(label, buttonObject.transform, 4f, 0f, width - 8f, height, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        return button;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button != null)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }

    private static void AddOutline(GameObject target, Color color, float thickness)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorTop(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -height - top);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static Color ColorFromHex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        color.a = alpha;
        return color;
    }
}

internal sealed class VoidWorksOverlayDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform target;
    private Action onDrop;
    private Action onBringToFront;
    private Vector2 pointerOffset;
    private bool dragging;

    public void Configure(RectTransform draggable, Action dropped, Action bringToFront)
    {
        target = draggable;
        onDrop = dropped;
        onBringToFront = bringToFront;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.pointerPress != null && eventData.pointerPress.GetComponentInParent<Button>() != null)
        {
            return;
        }

        var parent = target.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            return;
        }

        pointerOffset = target.anchoredPosition - ToTopLeftPosition(parent, localPoint);
        dragging = true;
        onBringToFront?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        var parent = target.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            return;
        }

        target.anchoredPosition = pointerOffset + ToTopLeftPosition(parent, localPoint);
        Clamp(target, parent);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging)
        {
            dragging = false;
            onDrop?.Invoke();
        }
    }

    private static Vector2 ToTopLeftPosition(RectTransform parent, Vector2 localPoint)
    {
        return new Vector2(localPoint.x + parent.rect.width * 0.5f, localPoint.y - parent.rect.height * 0.5f);
    }

    private static void Clamp(RectTransform target, RectTransform parent)
    {
        var maxX = Mathf.Max(0f, parent.rect.width - target.rect.width);
        var maxY = Mathf.Max(0f, parent.rect.height - target.rect.height);
        target.anchoredPosition = new Vector2(Mathf.Clamp(target.anchoredPosition.x, 0f, maxX),
            Mathf.Clamp(target.anchoredPosition.y, -maxY, 0f));
    }
}
