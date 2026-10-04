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
    private Text headingText;
    private Text localBestText;
    private Text runText;
    private Text statusText;
    private Text rowsText;
    private bool suppressEndEdit;
    private ContentSizeFitter rowsFitter;
    private InputField nicknameInput;
    private Button saveButton;
    private Button cancelButton;
    private Button closeButton;
    private Font font;
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

    private void Build(Canvas canvas)
    {
        Text sceneText = FindObjectOfType<Text>();
        font = sceneText != null && sceneText.font != null
            ? sceneText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        root.SetAsLastSibling();

        // Ranking button lives in its own full safe-area container outside the modal.
        GameObject rootSafe = MakeTransparent(root, "RootSafeArea");
        rootSafeRect = rootSafe.GetComponent<RectTransform>();
        ApplySafeArea(rootSafeRect);

        openButton = MakeButton(rootSafe.transform, "🏆 랭킹", new Color(0.07f, 0.17f, 0.10f, 0.96f), Open);
        SetRect(openButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 24f), new Vector2(230f, 82f), new Vector2(0.5f, 0f));
        openButton.interactable = requestedEnabled;

        modal = MakePanel(root, "LeaderboardModal", new Color(0.01f, 0.035f, 0.02f, 0.94f));
        Stretch(modal.GetComponent<RectTransform>());

        // Safe-area container so notches / home indicators never cover the card.
        GameObject safe = MakeTransparent(modal.transform, "SafeArea");
        safeAreaRect = safe.GetComponent<RectTransform>();
        ApplySafeArea(safeAreaRect);

        GameObject cardGo = MakePanel(safe.transform, "Card", new Color(0.035f, 0.10f, 0.06f, 0.99f));
        card = cardGo.GetComponent<RectTransform>();
        SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1180f, 900f), new Vector2(0.5f, 0.5f));

        Text title = MakeText(card.transform, "ZINE 3D · 명예의 전당", 48, TextAnchor.MiddleLeft, Color.white);
        titleText = title;
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -42f), new Vector2(-170f, 80f), new Vector2(0f, 1f));

        closeButton = MakeButton(card.transform, "닫기 ✕", new Color(0.18f, 0.25f, 0.20f, 1f), Close);
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-34f, -30f), new Vector2(150f, 70f), new Vector2(1f, 1f));

        runText = MakeText(card.transform, string.Empty, 30, TextAnchor.MiddleLeft, new Color(0.75f, 1f, 0.55f));
        SetRect(runText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -132f), new Vector2(-90f, 54f), new Vector2(0f, 1f));

        localBestText = MakeText(card.transform, string.Empty, 27, TextAnchor.MiddleLeft, new Color(0.80f, 0.90f, 0.82f));
        SetRect(localBestText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -190f), new Vector2(-90f, 50f), new Vector2(0f, 1f));

        nicknameInput = MakeInput(card.transform);
        SetRect(nicknameInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -254f), new Vector2(-560f, 66f), new Vector2(0f, 1f));
        nicknameInput.text = ZineLeaderboardClient.Nickname;
        nicknameBeforeEdit = nicknameInput.text;
        nicknameInput.onEndEdit.AddListener(OnNicknameEndEdit);

        saveButton = MakeButton(card.transform, "닉네임 등록", new Color(0.34f, 0.58f, 0.13f, 1f), SaveNickname);
        SetRect(saveButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-300f, -254f), new Vector2(235f, 66f), new Vector2(1f, 1f));

        cancelButton = MakeButton(card.transform, "취소", new Color(0.22f, 0.28f, 0.24f, 1f), CancelNicknameEdit);
        SetRect(cancelButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-45f, -254f), new Vector2(180f, 66f), new Vector2(1f, 1f));

        statusText = MakeText(card.transform, string.Empty, 24, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.45f));
        SetRect(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -328f), new Vector2(-90f, 48f), new Vector2(0f, 1f));

        headingText = MakeText(card.transform, "순위      닉네임      코인      시간", 25,
            TextAnchor.MiddleLeft, new Color(0.65f, 0.75f, 0.68f));
        SetRect(headingText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(45f, -388f), new Vector2(-90f, 48f), new Vector2(0f, 1f));

        // Scrollable rank list: all 10 rows reachable on small landscape screens.
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
            "닉네임 등록을 누르면 닉네임과 최고 코인·시간이 공개 랭킹에 등록됩니다. 오프라인 플레이도 항상 가능합니다.",
            23, TextAnchor.MiddleLeft, new Color(0.80f, 0.90f, 0.82f));
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
        float margin = Mathf.Min(24f, availH * 0.04f);
        float cardW = Mathf.Min(1180f, availW - margin * 2f);
        float cardH = Mathf.Min(keyboardOpen ? 290f : 900f, availH - margin * 2f);
        card.sizeDelta = new Vector2(cardW, cardH);
        float scale = Mathf.Min(1f, cardW / 1180f, cardH / (keyboardOpen ? 290f : 900f));
        float pad = 32f * scale;

        titleText.fontSize = Mathf.Max(16, Mathf.RoundToInt((keyboardOpen ? 32f : 42f) * scale));
        Place(titleText.rectTransform, pad, 20f * scale, cardW - pad * 2f - 160f * scale, 56f * scale);
        Place(closeButton.GetComponent<RectTransform>(), cardW - pad - 145f * scale, 20f * scale, 145f * scale, 56f * scale);
        runText.gameObject.SetActive(!keyboardOpen && gameOverShown);
        localBestText.gameObject.SetActive(!keyboardOpen);
        headingText.gameObject.SetActive(!keyboardOpen);
        rowsScrollRect.gameObject.SetActive(!keyboardOpen);
        participationText.gameObject.SetActive(!keyboardOpen);
        privacyButton.gameObject.SetActive(!keyboardOpen);
        deleteButton.gameObject.SetActive(!keyboardOpen);
        deleteButton.interactable = ZineLeaderboardClient.HasPlayer;
        if (keyboardOpen)
        {
            Place(nicknameInput.GetComponent<RectTransform>(), pad, 88f * scale, cardW - pad * 2f, 58f * scale);
            float buttonW = (cardW - pad * 2f - 16f * scale) * 0.5f;
            Place(saveButton.GetComponent<RectTransform>(), pad, 162f * scale, buttonW, 58f * scale);
            Place(cancelButton.GetComponent<RectTransform>(), pad + buttonW + 16f * scale, 162f * scale, buttonW, 58f * scale);
            Place(statusText.rectTransform, pad, 232f * scale, cardW - pad * 2f, 40f * scale);
        }
        else
        {
            Place(runText.rectTransform, pad, 100f * scale, cardW - pad * 2f, 42f * scale);
            Place(localBestText.rectTransform, pad, 150f * scale, cardW - pad * 2f, 44f * scale);
            float inputW = cardW - pad * 2f - 440f * scale;
            Place(nicknameInput.GetComponent<RectTransform>(), pad, 212f * scale, inputW, 64f * scale);
            Place(saveButton.GetComponent<RectTransform>(), pad + inputW + 16f * scale, 212f * scale, 240f * scale, 64f * scale);
            Place(cancelButton.GetComponent<RectTransform>(), cardW - pad - 168f * scale, 212f * scale, 168f * scale, 64f * scale);
            Place(statusText.rectTransform, pad, 290f * scale, cardW - pad * 2f, 50f * scale);
            Place(participationText.rectTransform, pad, 348f * scale, cardW - pad * 2f, 66f * scale);
            Place(headingText.rectTransform, pad, 428f * scale, cardW - pad * 2f, 42f * scale);
            Place(rowsScrollRect, pad, 482f * scale, cardW - pad * 2f, cardH - 578f * scale);
            float footerW = (cardW - pad * 2f - 20f * scale) * 0.5f;
            Place(privacyButton.GetComponent<RectTransform>(), pad, cardH - 78f * scale, footerW, 54f * scale);
            Place(deleteButton.GetComponent<RectTransform>(), pad + footerW + 20f * scale, cardH - 78f * scale, footerW, 54f * scale);
        }
        foreach (Text text in card.GetComponentsInChildren<Text>(true))
        {
            if (text == titleText) continue;
            text.fontSize = Mathf.Max(14, Mathf.RoundToInt((text == participationText ? 23f : text == rowsText ? 27f : 28f) * scale));
        }
        ResizeRows();
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top),
            new Vector2(width, height), new Vector2(0f, 1f));
    }

    private void ResizeRows()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowsText.rectTransform);
        rowsContent.sizeDelta = new Vector2(0f, Mathf.Max(rowsScrollRect.rect.height, rowsText.preferredHeight + 16f));
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
        rowsText.text = "랭킹을 불러오는 중…";
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
            rowsText.text = "랭킹을 불러오지 못했습니다.\n게임은 오프라인에서도 계속할 수 있습니다.";
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

        if (entries == null || entries.Length == 0)
        {
            rowsText.text = "등록된 기록이 없습니다.";
            ResizeRows();
            return;
        }

        StringBuilder builder = new StringBuilder();
        int count = Mathf.Min(10, entries.Length);
        for (int i = 0; i < count; i++)
        {
            ZineLeaderboardEntry entry = entries[i];
            string name = string.IsNullOrEmpty(entry.nickname) ? "-" : entry.nickname.Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            builder.Append(i + 1 <= 3 ? "★ " : "　 ");
            builder.Append(entry.rank).Append("위  ").Append(name)
                .Append("  ·  코인 ").Append(entry.body_count)
                .Append("  ·  ").Append(FormatDuration(entry.survival_ms));
            if (i < count - 1) builder.Append('\n');
        }
        rowsText.text = builder.ToString();
        ResizeRows();
        rowsContent.sizeDelta = new Vector2(0f, Mathf.Max(rowsScrollRect.rect.height, rowsText.preferredHeight + 16f));
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
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private GameObject MakeTransparent(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go;
    }

    private Button MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = MakePanel(parent, label + "Button", color);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(action);
        Text text = MakeText(go.transform, label, 30, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private InputField MakeInput(Transform parent)
    {
        GameObject go = MakePanel(parent, "NicknameInput", new Color(0.02f, 0.055f, 0.035f, 1f));
        InputField input = go.AddComponent<InputField>();
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
        text.alignment = alignment;
        text.color = color;
        text.text = value;
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
