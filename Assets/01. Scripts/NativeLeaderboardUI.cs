using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime-built leaderboard UI for Android and iOS. WebGL keeps using the
/// HTML overlay, so no WebGL scene or template behavior changes here.
/// </summary>
public sealed class NativeLeaderboardUI : MonoBehaviour
{
    private static NativeLeaderboardUI instance;
    private static bool requestedEnabled = true;
    private static bool subscribedToSceneLoad;
    private static bool showAfterBuild;
    private static int lastBodyCount;
    private static int lastSurvivalMs;

    private Button openButton;
    private GameObject modal;
    private RectTransform card;
    private RectTransform rowsScrollRect;
    private RectTransform rowsContent;
    private RectTransform safeAreaRect;
    private RectTransform rootSafeRect;
    private Text participationText;
    private Button privacyButton;
    private Button deleteButton;
    private GameObject deletionConfirmation;
    private bool gameOverShown;
    private Rect lastSafeArea;
    private bool lastKeyboardVisible;
    private Text titleText;
    private Text kickerText;
    private Text nicknameLabel;
    private Text headingText;
    private Text localBestText;
    private Text runText;
    private Text statusText;
    private Text rowsText;
    private Canvas uiCanvas;
    private float pointToCanvas = 1f;
    private bool suppressEndEdit;
    private ContentSizeFitter rowsFitter;
    private InputField nicknameInput;
    private Button saveButton;
    private Button cancelButton;
    private Button closeButton;
    private Font font;
    private float RowHeight = 34f;
    private const float RowHeightPoints = 34f;
    private const float RowHeightCompactPoints = 29f;
    private static readonly float[] ColumnEdges = new float[] { 0f, 0.17f, 0.62f, 0.79f, 1f };
    private static readonly string[] HeaderLabels = new string[] { "순위", "닉네임", "코인", "시간" };
    private Text[] headerCells;
    private readonly System.Collections.Generic.List<GameObject> rowObjects = new System.Collections.Generic.List<GameObject>();
    private bool pausedGame;
    private string nicknameBeforeEdit = string.Empty;
    private int lastScreenW;
    private int lastScreenH;
    private float lastKeyboardH;

    public static bool IsModalOpen
    {
        get { return instance != null && instance.modal != null && instance.modal.activeSelf; }
    }

    private static bool PlatformEnabled
    {
        get
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_WEBGL
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        requestedEnabled = true;
        subscribedToSceneLoad = false;
        showAfterBuild = false;
        lastBodyCount = 0;
        lastSurvivalMs = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!PlatformEnabled || subscribedToSceneLoad)
        {
            return;
        }

        subscribedToSceneLoad = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void SetEnabled(bool enabled)
    {
        requestedEnabled = enabled;
        if (instance != null && instance.openButton != null)
        {
            instance.openButton.interactable = enabled;
        }
    }

    public static void ShowGameOver(int bodyCount, int survivalMs)
    {
        if (!PlatformEnabled)
        {
            return;
        }

        lastBodyCount = Mathf.Max(0, bodyCount);
        lastSurvivalMs = Mathf.Max(0, survivalMs);
        showAfterBuild = true;
        EnsureCreated();
        if (instance != null && instance.modal != null)
        {
            instance.Show(true);
        }
    }

    public static void CloseModal()
    {
        if (instance != null)
        {
            if (instance.deletionConfirmation.activeSelf) instance.deletionConfirmation.SetActive(false);
            else if (instance.nicknameInput.isFocused) instance.CancelNicknameEdit();
            else instance.Close();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureCreated();
    }

    private static void EnsureCreated()
    {
        if (!PlatformEnabled || instance != null)
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject host = new GameObject("NativeLeaderboardUI", typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        instance = host.AddComponent<NativeLeaderboardUI>();
        instance.Build(canvas);
    }

    private static float CurrentDensity()
    {
#if UNITY_EDITOR
        return 1f;
#else
        float dpi = Screen.dpi;
        if (dpi <= 0f) return 1f;
        return Mathf.Clamp(Mathf.Round(dpi / 160f), 1f, 4f);
#endif
    }

    private static float PointsToCanvasUnits(Canvas canvas, float density)
    {
        float s = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        if (s <= 0f) s = 1f;
        return Mathf.Max(0.01f, density / s);
    }

    private void Build(Canvas canvas)
    {
        uiCanvas = canvas;
        pointToCanvas = PointsToCanvasUnits(canvas, CurrentDensity());
        Font rankingFont = Resources.Load<Font>("ZineRankingFont");
        Text sceneText = FindObjectOfType<Text>();
        font = rankingFont != null ? rankingFont
            : sceneText != null && sceneText.font != null
            ? sceneText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        root.SetAsLastSibling();

        // Ranking button lives in its own full safe-area container outside the modal.
        GameObject rootSafe = MakeTransparent(root, "RootSafeArea");
        rootSafeRect = rootSafe.GetComponent<RectTransform>();
        ApplySafeArea(rootSafeRect);

        openButton = MakeButton(rootSafe.transform, "랭킹", new Color(0.07f, 0.17f, 0.10f, 0.96f), Open, false);
        SetRect(openButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 24f), new Vector2(230f, 82f), new Vector2(0.5f, 0f));
        openButton.interactable = requestedEnabled;

        modal = MakePanel(root, "LeaderboardModal", new Color(1f / 255f, 7f / 255f, 4f / 255f, 0.78f));
        Stretch(modal.GetComponent<RectTransform>());

        // Safe-area container so notches / home indicators never cover the card.
        GameObject safe = MakeTransparent(modal.transform, "SafeArea");
        safeAreaRect = safe.GetComponent<RectTransform>();
        ApplySafeArea(safeAreaRect);

        GameObject cardGo = MakePanel(safe.transform, "Card", new Color(0.035f, 0.10f, 0.06f, 0.99f));
        card = cardGo.GetComponent<RectTransform>();
        SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1180f, 900f), new Vector2(0.5f, 0.5f));

        Color body = new Color(239f / 255f, 1f, 232f / 255f);
        Color muted = new Color(body.r, body.g, body.b, 0.68f);
        Color lime = new Color(184f / 255f, 1f, 87f / 255f);
        kickerText = MakeText(card.transform, "ZINE3D", 11, TextAnchor.MiddleLeft, lime);
        kickerText.fontStyle = FontStyle.Bold;
        titleText = MakeText(card.transform, "명예의 전당", 26, TextAnchor.MiddleLeft, body);
        titleText.fontStyle = FontStyle.Bold;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        closeButton = MakeButton(card.transform, "×", Color.clear, Close, true, 26);
        runText = MakeText(card.transform, string.Empty, 12, TextAnchor.MiddleLeft, new Color(1f, 0.98f, 0.82f));
        localBestText = MakeText(card.transform, string.Empty, 11, TextAnchor.MiddleLeft, muted);
        nicknameLabel = MakeText(card.transform, "내 닉네임", 12, TextAnchor.MiddleLeft, muted);
        nicknameInput = MakeInput(card.transform);
        nicknameInput.text = ZineLeaderboardClient.Nickname;
        nicknameBeforeEdit = nicknameInput.text;
        nicknameInput.onEndEdit.AddListener(OnNicknameEndEdit);
        saveButton = MakeButton(card.transform, "저장", lime, SaveNickname, true, 13,
            new Color(19f / 255f, 32f / 255f, 7f / 255f), true);
        StyleWebPanel(saveButton.gameObject, lime, Color.clear, 11f);
        cancelButton = MakeButton(card.transform, "취소", Color.clear, CancelNicknameEdit, true, 12);
        statusText = MakeText(card.transform, string.Empty, 12, TextAnchor.MiddleLeft, muted);
        headingText = MakeText(card.transform, string.Empty, 11, TextAnchor.MiddleLeft,
            new Color(body.r, body.g, body.b, 0.56f));
        GameObject headingBackground = new GameObject("HeaderBackground", typeof(RectTransform), typeof(Image));
        headingBackground.transform.SetParent(headingText.transform, false);
        Stretch(headingBackground.GetComponent<RectTransform>());
        headingBackground.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        headingBackground.GetComponent<Image>().raycastTarget = false;

        // Scrollable rank list: all returned rows reachable on small landscape screens.
        GameObject scrollGo = new GameObject("RowsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
        scrollGo.transform.SetParent(card.transform, false);
        Image scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.18f);
        scrollGo.GetComponent<Mask>().showMaskGraphic = false;
        rowsScrollRect = scrollGo.GetComponent<RectTransform>();
        SetRect(rowsScrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(45f, 42f), new Vector2(-90f, -448f), new Vector2(0f, 0f));

        GameObject contentGo = new GameObject("RowsContent", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        rowsContent = contentGo.GetComponent<RectTransform>();
        rowsContent.anchorMin = new Vector2(0f, 1f);
        rowsContent.anchorMax = new Vector2(1f, 1f);
        rowsContent.pivot = new Vector2(0.5f, 1f);
        rowsContent.anchoredPosition = Vector2.zero;
        rowsContent.sizeDelta = new Vector2(0f, 0f);

        rowsText = MakeText(rowsContent.transform, "랭킹을 불러오는 중…", 27, TextAnchor.UpperLeft, Color.white);
        rowsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        rowsText.verticalOverflow = VerticalWrapMode.Overflow;
        rowsText.rectTransform.anchorMin = new Vector2(0f, 1f);
        rowsText.rectTransform.anchorMax = new Vector2(1f, 1f);
        rowsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        rowsText.rectTransform.anchoredPosition = Vector2.zero;
        rowsText.rectTransform.sizeDelta = new Vector2(-20f, 0f);
        rowsFitter = rowsText.gameObject.AddComponent<ContentSizeFitter>();
        rowsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = rowsContent;
        scroll.viewport = rowsScrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        participationText = MakeText(card.transform,
            "저장하면 닉네임과 최고 코인·시간이 공개됩니다. 오프라인 플레이도 가능합니다.",
            11, TextAnchor.MiddleLeft, new Color(0.80f, 0.90f, 0.82f));
        privacyButton = MakeButton(card.transform, "개인정보 처리방침", new Color(0.18f, 0.25f, 0.20f),
            () => Application.OpenURL("https://ghtnql.github.io/Zine3D/privacy-policy.html"));
        deleteButton = MakeButton(card.transform, "랭킹 등록 삭제", new Color(0.35f, 0.15f, 0.13f), RequestDeletion);
        deletionConfirmation = MakePanel(card.transform, "DeleteConfirmation", new Color(0.025f, 0.055f, 0.035f, 1f));
        Stretch(deletionConfirmation.GetComponent<RectTransform>());
        Text deletionText = MakeText(deletionConfirmation.transform,
            "공개 랭킹의 닉네임과 기록을 삭제할까요?\n기기에 저장된 최고기록은 유지됩니다.", 30, TextAnchor.MiddleCenter, Color.white);
        SetRect(deletionText.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.85f),
            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Button confirmDeletion = MakeButton(deletionConfirmation.transform, "삭제 확인", new Color(0.45f, 0.18f, 0.14f), ConfirmDeletion);
        SetRect(confirmDeletion.GetComponent<RectTransform>(), new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.30f),
            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Button cancelDeletion = MakeButton(deletionConfirmation.transform, "취소", new Color(0.18f, 0.25f, 0.20f),
            () => deletionConfirmation.SetActive(false));
        SetRect(cancelDeletion.GetComponent<RectTransform>(), new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.30f),
            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        deletionConfirmation.SetActive(false);

        modal.SetActive(false);
        RefreshLocalText();
        ApplyResponsiveLayout(false);
        Subscribe();

        if (showAfterBuild)
        {
            Show(true);
        }
    }

    private void Update()
    {
        // Snake.Update owns Escape; processing it here too can close then quit in one frame.
        float keyboardH = GetKeyboardHeight();
        if (Screen.width != lastScreenW || Screen.height != lastScreenH || Screen.safeArea != lastSafeArea
            || TouchScreenKeyboard.visible != lastKeyboardVisible || !Mathf.Approximately(keyboardH, lastKeyboardH))
        {
            ApplyResponsiveLayout(keyboardH > 0f);
        }
    }

    private static float GetKeyboardHeight()
    {
        if (TouchScreenKeyboard.visible)
        {
            float height = TouchScreenKeyboard.area.height;
            if (height <= 0f)
            {
                height = Screen.height * 0.55f;
            }
            return height;
        }
        return 0f;
    }

    private void ApplySafeArea(RectTransform safe)
    {
        Rect area = Screen.safeArea;
        Vector2 min = area.position;
        Vector2 max = area.position + area.size;
        if (Screen.width > 0f) { min.x /= Screen.width; max.x /= Screen.width; }
        if (Screen.height > 0f) { min.y /= Screen.height; max.y /= Screen.height; }
        safe.anchorMin = min;
        safe.anchorMax = max;
        safe.offsetMin = Vector2.zero;
        safe.offsetMax = Vector2.zero;
    }

    private void ApplyResponsiveLayout(bool keyboardOpen)
    {
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastSafeArea = Screen.safeArea;
        lastKeyboardVisible = TouchScreenKeyboard.visible;
        lastKeyboardH = GetKeyboardHeight();
        if (card == null || Screen.width <= 0 || Screen.height <= 0) return;

        ApplySafeArea(rootSafeRect);
        ApplySafeArea(safeAreaRect);
        if (keyboardOpen)
        {
            // Keyboard area is in screen pixels; anchors convert it to Canvas space.
            Rect keyboard = TouchScreenKeyboard.area;
            float keyboardTop = keyboard.height > 0f ? keyboard.yMax : lastKeyboardH;
            Vector2 min = safeAreaRect.anchorMin;
            min.y = Mathf.Clamp(keyboardTop / Screen.height, min.y, safeAreaRect.anchorMax.y - 0.01f);
            safeAreaRect.anchorMin = min;
        }
        RectTransform root = GetComponent<RectTransform>();
        float availW = root.rect.width * (safeAreaRect.anchorMax.x - safeAreaRect.anchorMin.x);
        float availH = root.rect.height * (safeAreaRect.anchorMax.y - safeAreaRect.anchorMin.y);
        pointToCanvas = PointsToCanvasUnits(uiCanvas, CurrentDensity());
        float unit = pointToCanvas;
        float logicalH = availH / unit;
        bool compact = logicalH < 520f || keyboardOpen;
        float padX = compact ? 16f : 20f;
        float padY = compact ? 13f : 20f;
        float width = Mathf.Max(1f, Mathf.Min(620f, availW / unit * 0.94f));
        float height = Mathf.Max(1f, Mathf.Min(keyboardOpen ? 172f : gameOverShown ? 640f : 608f,
            Mathf.Min(720f, logicalH * 0.92f)));
        card.sizeDelta = new Vector2(width * unit, height * unit);
        float innerW = Mathf.Max(1f, width - padX * 2f);
        RowHeight = (compact ? RowHeightCompactPoints : RowHeightPoints) * unit;

        // Dimensions are logical points, mapped once to Canvas units. Short screens
        // scroll the table rather than shrinking body text and touch controls.
        foreach (ZineWebPanel panel in card.GetComponentsInChildren<ZineWebPanel>(true))
        {
            panel.Radius = (panel.gameObject == card.gameObject ? 22f : 11f) * unit;
            panel.BorderWidth = unit;
            panel.SetVerticesDirty();
        }
        SetFontPoints(kickerText, 11f);
        SetFontPoints(titleText, compact ? 20f : 26f);
        SetFontPoints(nicknameLabel, 12f);
        SetFontPoints(runText, 12f);
        SetFontPoints(localBestText, 11f);
        SetFontPoints(statusText, 12f);
        SetFontPoints(headingText, 11f);
        SetFontPoints(rowsText, 13f);
        SetFontPoints(participationText, 11f);
        SetButtonFontPoints(closeButton, 26f);
        SetButtonFontPoints(saveButton, 13f);
        SetButtonFontPoints(cancelButton, 12f);
        SetButtonFontPoints(privacyButton, 11f);
        SetButtonFontPoints(deleteButton, 11f);
        foreach (Text text in nicknameInput.GetComponentsInChildren<Text>(true)) SetFontPoints(text, 15f);
        foreach (Text text in deletionConfirmation.GetComponentsInChildren<Text>(true))
            SetFontPoints(text, text.transform.parent == deletionConfirmation.transform ? 15f : 13f);
        foreach (Text text in nicknameInput.GetComponentsInChildren<Text>(true))
        {
            text.rectTransform.offsetMin = new Vector2(12f * unit, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f * unit, 0f);
        }

        bool showKicker = !keyboardOpen || height >= 150f;
        kickerText.gameObject.SetActive(showKicker);
        PlacePoints(kickerText.rectTransform, padX, padY, innerW - 54f, 14f);
        PlacePoints(titleText.rectTransform, padX, padY + (showKicker ? 16f : 0f), innerW - 54f, compact ? 26f : 32f);
        PlacePoints(closeButton.GetComponent<RectTransform>(), width - padX - 38f, padY, 38f, 38f);
        float top = padY + (keyboardOpen && !showKicker ? 30f : compact ? 44f : 50f) + (compact ? 8f : 14f);
        runText.gameObject.SetActive(!keyboardOpen && gameOverShown);
        if (!keyboardOpen && gameOverShown)
        {
            PlacePoints(runText.rectTransform, padX, top, innerW, 24f);
            top += 32f;
        }
        nicknameLabel.gameObject.SetActive(!compact);
        if (!compact)
        {
            PlacePoints(nicknameLabel.rectTransform, padX, top, innerW, 16f);
            top += 22f;
        }
        float saveW = 64f;
        float cancelW = 52f;
        float inputW = Mathf.Max(1f, innerW - saveW - cancelW - 16f);
        PlacePoints(nicknameInput.GetComponent<RectTransform>(), padX, top, inputW, 42f);
        PlacePoints(saveButton.GetComponent<RectTransform>(), padX + inputW + 8f, top, saveW, 42f);
        PlacePoints(cancelButton.GetComponent<RectTransform>(), padX + inputW + saveW + 16f, top, cancelW, 42f);
        top += 42f + (compact ? 5f : 8f);
        PlacePoints(statusText.rectTransform, padX, top, innerW, 20f);
        top += 20f + (compact ? 5f : 8f);
        localBestText.gameObject.SetActive(!keyboardOpen);
        headingText.gameObject.SetActive(!keyboardOpen);
        rowsScrollRect.gameObject.SetActive(!keyboardOpen);
        participationText.gameObject.SetActive(!keyboardOpen);
        privacyButton.gameObject.SetActive(!keyboardOpen);
        deleteButton.gameObject.SetActive(!keyboardOpen);
        deleteButton.interactable = ZineLeaderboardClient.HasPlayer;
        if (!keyboardOpen)
        {
            float footerHeight = compact ? 85f : 94f;
            float footerTop = height - padY - footerHeight;
            PlacePoints(headingText.rectTransform, padX, top, innerW, compact ? 26f : 30f);
            top += compact ? 26f : 30f;
            PlacePoints(rowsScrollRect, padX, top, innerW, Mathf.Max(1f, footerTop - top - 8f));
            PlacePoints(localBestText.rectTransform, padX, footerTop, innerW, 18f);
            PlacePoints(participationText.rectTransform, padX, footerTop + 20f, innerW, compact ? 28f : 36f);
            float footerButtonTop = height - padY - 30f;
            float footerButtonW = (innerW - 8f) * 0.5f;
            PlacePoints(privacyButton.GetComponent<RectTransform>(), padX, footerButtonTop, footerButtonW, 30f);
            PlacePoints(deleteButton.GetComponent<RectTransform>(), padX + footerButtonW + 8f, footerButtonTop, footerButtonW, 30f);
        }
        SetRect(openButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f * unit), new Vector2(128f * unit, 42f * unit), new Vector2(0.5f, 0f));
        SetButtonFontPoints(openButton, 15f);
        ResizeRows();
    }

    private void PlacePoints(RectTransform rect, float left, float top, float width, float height)
    {
        Place(rect, left * pointToCanvas, top * pointToCanvas,
            Mathf.Max(1f, width) * pointToCanvas, Mathf.Max(1f, height) * pointToCanvas);
    }

    private void SetFontPoints(Text text, float points)
    {
        if (text != null) text.fontSize = Mathf.Max(1, Mathf.RoundToInt(points * pointToCanvas));
    }

    private void SetButtonFontPoints(Button button, float points)
    {
        SetFontPoints(button.GetComponentInChildren<Text>(true), points);
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top),
            new Vector2(width, height), new Vector2(0f, 1f));
    }

    private void EnsureHeaderCells()
    {
        if (headingText == null || headerCells != null) return;
        headingText.text = string.Empty;
        headerCells = new Text[HeaderLabels.Length];
        for (int i = 0; i < HeaderLabels.Length; i++)
        {
            Text cell = MakeText(headingText.transform, HeaderLabels[i], 11, TextAnchor.MiddleLeft,
                new Color(239f / 255f, 1f, 232f / 255f, 0.56f));
            cell.gameObject.name = "HeaderCell" + i;
            cell.horizontalOverflow = HorizontalWrapMode.Wrap;
            cell.verticalOverflow = VerticalWrapMode.Truncate;
            SetColumnRect(cell.rectTransform, ColumnEdges[i], ColumnEdges[i + 1]);
            headerCells[i] = cell;
        }
    }

    private void SetColumnRect(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(left, 0f);
        rect.anchorMax = new Vector2(right, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(10f * pointToCanvas, 5f * pointToCanvas);
        rect.offsetMax = new Vector2(-4f * pointToCanvas, -5f * pointToCanvas);
    }

    private Text MakeRowCell(Transform parent, string name, string value, Color color)
    {
        Text cell = MakeText(parent, value, 13, TextAnchor.MiddleLeft, color);
        cell.gameObject.name = name;
        cell.horizontalOverflow = HorizontalWrapMode.Wrap;
        cell.verticalOverflow = VerticalWrapMode.Truncate;
        return cell;
    }

    private void ClearRows()
    {
        rowObjects.Clear();
        if (rowsContent == null) return;
        for (int i = rowsContent.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = rowsContent.transform.GetChild(i).gameObject;
            if (rowsText != null && child == rowsText.gameObject) continue;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void ShowRowsStatus(string message)
    {
        ClearRows();
        if (rowsText != null)
        {
            rowsText.gameObject.SetActive(true);
            rowsText.text = message;
        }
    }

    private void ResizeRows()
    {
        EnsureHeaderCells();
        if (rowsContent == null || rowsScrollRect == null || rowsText == null) return;
        if (headerCells != null && headingText != null)
        {
            for (int i = 0; i < headerCells.Length; i++)
            {
                if (headerCells[i] != null)
                {
                    headerCells[i].fontSize = headingText.fontSize;
                    SetColumnRect(headerCells[i].rectTransform, ColumnEdges[i], ColumnEdges[i + 1]);
                }
            }
        }
        if (rowObjects.Count > 0)
        {
            float width = rowsScrollRect.rect.width;
            for (int i = 0; i < rowObjects.Count; i++)
            {
                GameObject row = rowObjects[i];
                if (row == null || !row.activeSelf) continue;
                RectTransform rowRect = row.GetComponent<RectTransform>();
                if (rowRect != null) Place(rowRect, 0f, i * RowHeight, width, RowHeight);
                foreach (Text cell in row.GetComponentsInChildren<Text>(true))
                {
                    cell.fontSize = rowsText.fontSize;
                    cell.fontStyle = cell.gameObject.name == "RankCell" ? FontStyle.Bold : FontStyle.Normal;
                    int column = cell.gameObject.name == "RankCell" ? 0 : cell.gameObject.name == "NicknameCell" ? 1
                        : cell.gameObject.name == "CoinCell" ? 2 : 3;
                    SetColumnRect(cell.rectTransform, ColumnEdges[column], ColumnEdges[column + 1]);
                }
            }
            float height = rowObjects.Count * RowHeight;
            rowsContent.sizeDelta = new Vector2(0f, Mathf.Max(rowsScrollRect.rect.height, height));
            return;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowsText.rectTransform);
        rowsContent.sizeDelta = new Vector2(0f, Mathf.Max(rowsScrollRect.rect.height, rowsText.preferredHeight + 16f * pointToCanvas));
    }

    private void Subscribe()
    {
        ZineLeaderboardClient.LocalBestChanged += OnLocalBestChanged;
        ZineLeaderboardClient.LeaderboardLoaded += OnLeaderboardLoaded;
        ZineLeaderboardClient.NicknameSaved += OnNicknameSaved;
        ZineLeaderboardClient.PlayerDeleted += OnPlayerDeleted;
        ZineLeaderboardClient.RequestFailed += OnRequestFailed;
    }

    private void OnDestroy()
    {
        ZineLeaderboardClient.LocalBestChanged -= OnLocalBestChanged;
        ZineLeaderboardClient.LeaderboardLoaded -= OnLeaderboardLoaded;
        ZineLeaderboardClient.NicknameSaved -= OnNicknameSaved;
        ZineLeaderboardClient.PlayerDeleted -= OnPlayerDeleted;
        ZineLeaderboardClient.RequestFailed -= OnRequestFailed;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Open()
    {
        pausedGame = true;
        Snake snake = FindObjectOfType<Snake>();
        if (snake != null)
        {
            snake.PauseForLeaderboard();
        }
        nicknameBeforeEdit = ZineLeaderboardClient.Nickname;
        Show(false);
    }

    private void Show(bool gameOver)
    {
        showAfterBuild = false;
        modal.SetActive(true);
        modal.transform.SetAsLastSibling();
        ApplySafeArea(modal.transform.GetChild(0) as RectTransform);
        gameOverShown = gameOver;
        deletionConfirmation.SetActive(false);
        ApplyResponsiveLayout(false);
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
        }
        nicknameBeforeEdit = ZineLeaderboardClient.Nickname;
        nicknameInput.text = nicknameBeforeEdit;
        runText.gameObject.SetActive(gameOver);
        runText.text = gameOver
            ? "이번 기록 · 코인 " + lastBodyCount + " · " + FormatDuration(lastSurvivalMs)
            : string.Empty;
        RefreshLocalText();
        statusText.text = Application.internetReachability == NetworkReachability.NotReachable
            ? "오프라인입니다. 로컬 최고기록은 기기에 저장됩니다."
            : "랭킹을 불러오는 중…";
        ShowRowsStatus("랭킹을 불러오는 중…");
        ResizeRows();
        rowsScrollRect.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
        ZineLeaderboardClient.LoadLeaderboard(10);
        ZineLeaderboardClient.RetryPendingScore();
    }

    private void Close()
    {
        CancelNicknameEdit();
        deletionConfirmation.SetActive(false);
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
        }
        modal.SetActive(false);
        if (pausedGame)
        {
            Snake snake = FindObjectOfType<Snake>();
            if (snake != null)
            {
                snake.ResumeAfterLeaderboard();
            }
        }
        pausedGame = false;
    }

    private void SaveNickname()
    {
        statusText.text = "닉네임을 저장하는 중…";
        string typedNickname = nicknameInput.text;
        DismissKeyboard();
        ZineLeaderboardClient.SaveNickname(typedNickname);
    }

    private void CancelNicknameEdit()
    {
        // Preserve the last saved nickname; cancel never clears the field.
        DismissKeyboard();
        nicknameInput.text = nicknameBeforeEdit;
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
        }
        statusText.text = "닉네임 입력을 취소했습니다.";
    }

    private void OnNicknameEndEdit(string value)
    {
        // Done only dismisses input; registration always requires the explicit button.
        if (suppressEndEdit) return;
        if (nicknameInput.touchScreenKeyboard != null
            && nicknameInput.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Canceled)
        {
            CancelNicknameEdit();
            return;
        }
        DismissKeyboard();
    }

    private void DismissKeyboard()
    {
        suppressEndEdit = true;
        if (nicknameInput != null)
        {
            if (nicknameInput.touchScreenKeyboard != null) nicknameInput.touchScreenKeyboard.active = false;
            nicknameInput.DeactivateInputField();
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        suppressEndEdit = false;
        ApplyResponsiveLayout(GetKeyboardHeight() > 0f);
    }

    private void RequestDeletion()
    {
        CancelNicknameEdit();
        deletionConfirmation.SetActive(true);
        deletionConfirmation.transform.SetAsLastSibling();
    }

    private void ConfirmDeletion()
    {
        deletionConfirmation.SetActive(false);
        statusText.text = "랭킹 등록을 삭제하는 중…";
        ZineLeaderboardClient.DeletePlayer();
    }

    private void OnPlayerDeleted()
    {
        nicknameBeforeEdit = string.Empty;
        nicknameInput.text = string.Empty;
        deleteButton.interactable = false;
        RefreshLocalText();
        statusText.text = "공개 랭킹 등록을 삭제했습니다. 로컬 최고기록은 유지됩니다.";
        ZineLeaderboardClient.LoadLeaderboard(10);
    }

    private void OnLocalBestChanged(ZineScore score)
    {
        RefreshLocalText();
    }

    private void OnNicknameSaved(string nickname)
    {
        nicknameInput.text = nickname;
        nicknameBeforeEdit = nickname;
        statusText.text = "닉네임을 등록했습니다.";
        deleteButton.interactable = true;
        ZineLeaderboardClient.RetryPendingScore();
        ZineLeaderboardClient.LoadLeaderboard(10);
    }

    private void OnRequestFailed(string message)
    {
        statusText.text = string.IsNullOrEmpty(message) ? "랭킹을 불러오지 못했습니다." : message;
        if (ZineLeaderboardClient.CurrentLeaderboard.Length == 0)
        {
            ShowRowsStatus("랭킹을 불러오지 못했습니다.\n게임은 오프라인에서도 계속할 수 있습니다.");
            ResizeRows();
        }
    }

    private void OnLeaderboardLoaded(ZineLeaderboardEntry[] entries)
    {
        if (ZineLeaderboardClient.HasPendingScore && !ZineLeaderboardClient.HasPlayer)
        {
            statusText.text = "닉네임을 등록하면 로컬 최고기록이 서버 랭킹에 등록됩니다.";
        }
        else if (ZineLeaderboardClient.HasPendingScore)
        {
            statusText.text = "로컬 최고기록을 서버와 동기화하는 중입니다.";
        }
        else
        {
            statusText.text = string.Empty;
        }

        ClearRows();
        EnsureHeaderCells();

        if (entries == null || entries.Length == 0)
        {
            ShowRowsStatus("첫 기록의 주인공이 되어보세요.");
            ResizeRows();
            return;
        }

        if (rowsText != null)
        {
            rowsText.gameObject.SetActive(false);
        }

        Color rankColor = new Color(184f / 255f, 1f, 87f / 255f);
        Color bodyColor = new Color(239f / 255f, 1f, 232f / 255f);
        float width = rowsScrollRect != null ? rowsScrollRect.rect.width : 0f;
        int count = Mathf.Min(10, entries.Length);
        for (int i = 0; i < count; i++)
        {
            ZineLeaderboardEntry entry = entries[i];
            GameObject row = new GameObject("RankRow" + entry.rank, typeof(RectTransform));
            row.transform.SetParent(rowsContent.transform, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            Place(rowRect, 0f, i * RowHeight, width, RowHeight);

            Text rankCell = MakeRowCell(row.transform, "RankCell", "#" + entry.rank, rankColor);
            SetColumnRect(rankCell.rectTransform, ColumnEdges[0], ColumnEdges[1]);
            Text nicknameCell = MakeRowCell(row.transform, "NicknameCell", entry.nickname ?? string.Empty, bodyColor);
            SetColumnRect(nicknameCell.rectTransform, ColumnEdges[1], ColumnEdges[2]);
            Text coinCell = MakeRowCell(row.transform, "CoinCell", entry.body_count.ToString(), bodyColor);
            SetColumnRect(coinCell.rectTransform, ColumnEdges[2], ColumnEdges[3]);
            Text timeCell = MakeRowCell(row.transform, "TimeCell", FormatDuration(entry.survival_ms), bodyColor);
            SetColumnRect(timeCell.rectTransform, ColumnEdges[3], ColumnEdges[4]);

            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(row.transform, false);
            divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.09f);
            RectTransform dividerRect = divider.GetComponent<RectTransform>();
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.anchoredPosition = Vector2.zero;
            dividerRect.sizeDelta = new Vector2(0f, pointToCanvas);

            rowObjects.Add(row);
        }
        ResizeRows();
        rowsScrollRect.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
    }

    private void RefreshLocalText()
    {
        ZineScore best = ZineLeaderboardClient.LocalBest;
        localBestText.text = best.bodyCount <= 0 && best.survivalMs <= 0
            ? "내 로컬 최고기록 · 아직 없음"
            : "내 로컬 최고기록 · 코인 " + best.bodyCount + " · " + FormatDuration(best.survivalMs);
    }

    private static string FormatDuration(int milliseconds)
    {
        int seconds = Mathf.Max(0, milliseconds / 1000);
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int remainder = seconds % 60;
        return hours > 0
            ? string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, remainder)
            : string.Format("{0:00}:{1:00}", minutes, remainder);
    }

    private GameObject MakePanel(Transform parent, string name, Color color)
    {
        if (name == "Card")
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(ZineWebPanel));
            panel.transform.SetParent(parent, false);
            ZineWebPanel web = panel.GetComponent<ZineWebPanel>();
            web.SetStyle(new Color(0x14 / 255f, 0x24 / 255f, 0x17 / 255f, 1f),
                new Color(0x05 / 255f, 0x0F / 255f, 0x09 / 255f, 1f),
                new Color(184f / 255f, 1f, 87f / 255f, 0.3f), 22f, 1f);
            return panel;
        }
        GameObject flat = new GameObject(name, typeof(RectTransform), typeof(Image));
        flat.transform.SetParent(parent, false);
        flat.GetComponent<Image>().color = color;
        return flat;
    }

    private static void StyleWebPanel(GameObject go, Color fill, Color border, float radius)
    {
        ZineWebPanel web = go.GetComponent<ZineWebPanel>();
        if (web != null)
        {
            web.SetStyle(fill, fill, border, radius, 1f);
        }
    }

    private GameObject MakeTransparent(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go;
    }

    private Button MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, bool rounded = true, int textSize = 30, Color textColor = default(Color), bool hasTextColor = false)
    {
        GameObject go;
        if (rounded)
        {
            go = new GameObject(label + "Button", typeof(RectTransform), typeof(ZineWebPanel));
            go.transform.SetParent(parent, false);
            StyleWebPanel(go, new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0.2f), 11f);
        }
        else
        {
            go = MakePanel(parent, label + "Button", color);
        }
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Graphic>();
        button.onClick.AddListener(action);
        Text text = MakeText(go.transform, label, textSize, TextAnchor.MiddleCenter,
            hasTextColor ? textColor : Color.white);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private InputField MakeInput(Transform parent)
    {
        GameObject go = new GameObject("NicknameInput", typeof(RectTransform), typeof(ZineWebPanel));
        go.transform.SetParent(parent, false);
        StyleWebPanel(go, new Color(0f, 0f, 0f, 0.32f), new Color(1f, 1f, 1f, 0.2f), 11f);
        InputField input = go.AddComponent<InputField>();
        input.targetGraphic = go.GetComponent<Graphic>();
        input.characterLimit = 16;
        input.lineType = InputField.LineType.SingleLine;
        input.keyboardType = TouchScreenKeyboardType.Default;
        // Unity 2022.3 legacy InputField has no returnKeyType: explicit Save/Cancel only.
        input.shouldHideMobileInput = false;

        Text value = MakeText(go.transform, string.Empty, 28, TextAnchor.MiddleLeft, Color.white);
        SetRect(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-36f, 0f), Vector2.zero);
        Text placeholder = MakeText(go.transform, "닉네임 2~16자", 28, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.42f));
        Stretch(placeholder.rectTransform);
        placeholder.rectTransform.offsetMin = new Vector2(18f, 0f);
        placeholder.rectTransform.offsetMax = new Vector2(-18f, 0f);

        input.textComponent = value;
        input.placeholder = placeholder;
        return input;
    }

    private Text MakeText(Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
