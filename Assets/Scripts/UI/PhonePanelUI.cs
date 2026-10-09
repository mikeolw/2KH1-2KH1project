using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 핸드폰 UI - 퀵바의 폰 버튼을 누르면 열리는 화면 (NotePanelUI.cs와 같은 방식으로 작성)
// =====================================================================================
// ===== 디자인 =====
// 2000년대 고급 슬라이드폰(검은 몸체 + 금색 테두리/글씨)을 연 모습. 위는 화면, 아래는 키패드.
//   화면 : 상태 표시줄(안테나/배터리) / 제목줄 / 내용 / 소프트키 줄(왼쪽 "선택", 오른쪽 "닫기"·"뒤로")
//   내용 : 메인 메뉴(앱 아이콘 3열 x 4줄) / 앱 화면(공통 틀) / 전화 걸기(키패드로 누른 번호)
//   키패드: 통화 · 종료 + 숫자 0~9, *, #
//
// ===== 지금 단계 =====
// 앱 아이콘을 누르면 앱 화면으로 넘어가지만, 내용은 아직 없다("기록이 없습니다").
// 문자/통화기록 등의 실제 내용은 나중에 CSV와 함께 ShowApp()의 앱 화면에 붙인다.
// 키패드는 진짜 버튼이라 누르면 번호가 화면에 찍힌다(통화는 아직 아무 일도 안 함).
// 나중에 번호/비밀번호 퍼즐을 붙이려면 OnKeyPressed()와 dialedNumber를 쓰면 된다.
//
// ===== 닫기 / 뒤로 =====
//   앱 화면·전화 걸기 : Esc 또는 소프트키 "뒤로" -> 메인 메뉴 (UIManager가 Esc를 HandleEscape에 먼저 넘긴다)
//   메인 메뉴         : Esc, 소프트키 "닫기", 핸드폰 바깥 클릭 -> 닫힘
//
// ===== 왜 캔버스에 직접 만드나 =====
// 씬의 PhonePanel은 내용 없는 빈 껍데기다. 캔버스 바로 아래에 화면을 직접 만들어 씬이 어떻게
// 짜여 있든 항상 같은 자리/크기로 뜨게 한다. GameBootstrap이 이 컴포넌트를 PhonePanel에 붙여준다.
//
// ===== 그림 =====
// 앱/키 아이콘: Assets/Resources/Illusts/UI/Phone/ (App_*.png, Key_Call/Key_End.png).
// 흰색 128px 선 아이콘이라 Image.color로 금색을 입힌다. 그림이 없으면 아이콘 자리만 비고 글자는 나온다.
// 둥근 모서리는 에셋 없이 코드로 만든 9-slice 스프라이트로 그린다(RoundedSprite).
public class PhonePanelUI : MonoBehaviour
{
    public static PhonePanelUI Instance { get; private set; }

    private const string OverlayName = "__PhoneOverlay";
    private const string IconFolder = "Illusts/UI/Phone/";

    // ----- 크기 (캔버스 기준 1440x1080) -----
    private const float BodyWidth = 400f;
    private const float BodyHeight = 960f;
    private const float Rim = 3f;                 // 금색 테두리 두께
    private const float ScreenX = 24f, ScreenY = 34f, ScreenW = 352f, ScreenH = 520f;
    private const float StatusH = 30f, TitleH = 44f, SoftKeyH = 48f;
    private const float KeypadTop = 592f;

    // ----- 색 (검정 + 금색) -----
    private static readonly Color Gold = new Color32(0xC9, 0xA7, 0x5A, 0xFF);
    private static readonly Color GoldLight = new Color32(0xE6, 0xCF, 0x95, 0xFF);
    private static readonly Color GoldDim = new Color32(0x8A, 0x74, 0x44, 0xFF);
    private static readonly Color BodyColor = new Color32(0x0F, 0x0E, 0x0C, 0xFF);
    private static readonly Color ScreenColor = new Color32(0x07, 0x07, 0x07, 0xFF);
    private static readonly Color KeyColor = new Color32(0x1B, 0x19, 0x15, 0xFF);
    private static readonly Color KeyHoverColor = new Color32(0x2A, 0x26, 0x1E, 0xFF);
    private static readonly Color KeyPressColor = new Color32(0x3B, 0x34, 0x27, 0xFF);

    // ----- 메인 메뉴의 앱 (3열 x 4줄, 왼쪽 위부터) -----
    private static readonly (string id, string label)[] Apps =
    {
        ("CallLog", "통화기록"), ("Message", "문자"), ("Contacts", "연락처"),
        ("Gallery", "사진첩"), ("Memo", "메모"), ("Calendar", "일정"),
        ("Email", "이메일"), ("Internet", "인터넷"), ("VoiceMemo", "음성메모"),
        ("Clock", "시계"), ("Radio", "라디오"), ("Settings", "설정"),
    };

    // 키패드 숫자 키: (글자, 아래 작은 글자)
    private static readonly (string key, string sub)[] NumberKeys =
    {
        ("1", ""), ("2", "ABC"), ("3", "DEF"),
        ("4", "GHI"), ("5", "JKL"), ("6", "MNO"),
        ("7", "PQRS"), ("8", "TUV"), ("9", "WXYZ"),
        ("*", ""), ("0", "+"), ("#", ""),
    };

    private const int MaxDialDigits = 16;

    private enum Page { Menu, App, Dial }

    private GameObject overlay;
    private RectTransform phoneRoot;
    private CanvasGroup phoneGroup;
    private TMP_Text titleText;
    private TMP_Text leftSoftKeyText;
    private TMP_Text rightSoftKeyText;
    private GameObject menuPage, appPage, dialPage;
    private TMP_Text dialText;
    private Page page = Page.Menu;
    private string dialedNumber = "";

    private AudioSource sfxSource;
    private AudioClip clickClip;
    private Coroutine openRoutine;

    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    private void Awake()
    {
        Instance = this;
        HideOriginalPanelVisuals();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        AudioManager.RegisterSafe(sfxSource, AudioManager.Channel.Sfx);
        clickClip = Resources.Load<AudioClip>("Sounds/SFX/Click_SFX");

        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (overlay == null) BuildOverlay();
        if (overlay == null) return;

        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();   // 다른 UI에 가리지 않도록 항상 맨 앞으로

        dialedNumber = "";
        ShowMenu();

        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(PlayOpenAnimation());
    }

    private void OnDisable()
    {
        if (openRoutine != null) { StopCoroutine(openRoutine); openRoutine = null; }
        if (overlay != null) overlay.SetActive(false);
    }

    // UIManager가 Esc를 받았을 때 먼저 묻는다. 앱 화면/전화 걸기면 메인 메뉴로 돌아가고 true.
    // 메인 메뉴면 false -> UIManager가 핸드폰을 닫는다.
    public bool HandleEscape()
    {
        if (!isActiveAndEnabled || page == Page.Menu) return false;
        ShowMenu();
        return true;
    }

    // ---------------------------------------------------------------------------------
    // 화면 전환
    // ---------------------------------------------------------------------------------
    private void ShowMenu()
    {
        page = Page.Menu;
        SetPage("메인 메뉴", "선택", "닫기");
    }

    // 앱 화면 (공통 틀). 실제 내용은 나중에 앱별로 이 화면에 붙인다.
    private void ShowApp(string label)
    {
        page = Page.App;
        SetPage(label, "선택", "뒤로");
    }

    private void ShowDial()
    {
        page = Page.Dial;
        dialText.text = dialedNumber;
        SetPage("전화 걸기", "지우기", "뒤로");
    }

    private void SetPage(string title, string leftKey, string rightKey)
    {
        if (menuPage == null) return;
        menuPage.SetActive(page == Page.Menu);
        appPage.SetActive(page == Page.App);
        dialPage.SetActive(page == Page.Dial);
        titleText.text = title;
        leftSoftKeyText.text = leftKey;
        rightSoftKeyText.text = rightKey;
        // "선택"은 아직 하는 일이 없어서 흐리게, "지우기"는 실제로 동작하므로 밝게.
        leftSoftKeyText.color = page == Page.Dial ? GoldLight : GoldDim;
    }

    private void OnLeftSoftKey()
    {
        if (page != Page.Dial) return;
        PlayClick();
        if (dialedNumber.Length > 0) dialedNumber = dialedNumber.Substring(0, dialedNumber.Length - 1);
        dialText.text = dialedNumber;
    }

    private void OnRightSoftKey()
    {
        PlayClick();
        if (page == Page.Menu) gameObject.SetActive(false);   // 닫기 (UIManager는 이 패널이 켜져 있는지로 판단)
        else ShowMenu();
    }

    private void OnAppPressed(string label)
    {
        PlayClick();
        ShowApp(label);
    }

    // 숫자/*/# 키: 누른 번호를 화면에 찍는다.
    private void OnKeyPressed(string key)
    {
        PlayClick();
        if (dialedNumber.Length < MaxDialDigits) dialedNumber += key;
        ShowDial();
    }

    // 통화: 아직 연결할 상대가 없다. 나중에 번호 퍼즐을 붙일 자리.
    private void OnCallPressed()
    {
        PlayClick();
    }

    // 종료: 실제 폰처럼 누른 번호를 지우고 메인 메뉴로.
    private void OnEndPressed()
    {
        PlayClick();
        dialedNumber = "";
        ShowMenu();
    }

    private void PlayClick()
    {
        if (sfxSource != null && clickClip != null) sfxSource.PlayOneShot(clickClip);
    }

    // 주머니에서 꺼내듯 아래에서 짧게 올라오며 나타난다. 일시정지 중에도 돌도록 unscaled 시간을 쓴다.
    private IEnumerator PlayOpenAnimation()
    {
        const float duration = 0.2f;
        const float startOffset = -140f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);   // 끝으로 갈수록 부드럽게 멈춤
            phoneRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(startOffset, 0f, ease));
            phoneGroup.alpha = ease;
            yield return null;
        }
        phoneRoot.anchoredPosition = Vector2.zero;
        phoneGroup.alpha = 1f;
        openRoutine = null;
    }

    // ---------------------------------------------------------------------------------
    // 씬에 있던 원래 패널은 안 보이게 한다 (NotePanelUI.cs와 동일한 이유)
    // ---------------------------------------------------------------------------------
    private void HideOriginalPanelVisuals()
    {
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------------------------
    // 화면 만들기 (캔버스 바로 아래)
    // ---------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PhonePanelUI] 씬에 Canvas가 없어 핸드폰 화면을 만들 수 없습니다.");
            return;
        }

        // 예전 모양이 남아 있으면 지우고 새로 만든다 (이 컴포넌트가 들고 있는 참조를 다시 잡아야 하므로).
        var existing = canvas.transform.Find(OverlayName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        overlay = new GameObject(OverlayName, typeof(RectTransform));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());

        // ----- 화면 전체를 덮는 막 (바깥을 누르면 닫힘) -----
        // 핸드폰의 "부모"가 아니라 "형제"로 둔다. 부모에 닫기 버튼을 달면, 핸드폰 안에서 버튼이
        // 아닌 곳(키 사이 틈, 화면 테두리 등)을 누른 클릭이 부모로 올라가 닫기 버튼까지 눌러서,
        // 번호를 누르다 키를 살짝 벗어나면 핸드폰이 꺼지는 문제가 있었다.
        var dim = NewImage("Backdrop", overlay.transform, new Color(0f, 0f, 0f, 0.97f));   // 환경설정/가방/수첩과 같은 검은 막
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(() => gameObject.SetActive(false));

        // ----- 핸드폰 (금색 테두리 + 검은 몸체). 이 묶음이 열릴 때 올라오는 애니메이션 대상 -----
        var root = new GameObject("Phone", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(overlay.transform, false);
        phoneRoot = (RectTransform)root.transform;
        phoneRoot.anchorMin = phoneRoot.anchorMax = phoneRoot.pivot = new Vector2(0.5f, 0.5f);
        phoneRoot.sizeDelta = new Vector2(BodyWidth + Rim * 2f, BodyHeight + Rim * 2f);
        phoneGroup = root.GetComponent<CanvasGroup>();
        var rimImage = root.GetComponent<Image>();
        SetRounded(rimImage, 46, Gold);
        rimImage.raycastTarget = true;   // 몸체를 눌러도 뒤의 막(바깥 클릭 = 닫기)까지 새지 않게

        var body = NewImage("Body", root.transform, BodyColor);
        Stretch(body.rectTransform, Rim);
        SetRounded(body, 44, BodyColor);

        // 수화부(스피커) 구멍
        var slit = NewImage("Speaker", body.transform, GoldDim);
        PlaceTL(slit.rectTransform, BodyWidth / 2f - 30f, 14f, 60f, 6f);
        SetRounded(slit, 3, GoldDim);

        BuildScreen(body.transform);

        // 슬라이드 이음매
        var seam = NewImage("Seam", body.transform, new Color(Gold.r, Gold.g, Gold.b, 0.45f));
        PlaceTL(seam.rectTransform, 40f, KeypadTop - 18f, BodyWidth - 80f, 1f);

        BuildKeypad(body.transform);

        UIFontHelper.ApplyToChildren(overlay);
        overlay.SetActive(false);
    }

    private void BuildScreen(Transform body)
    {
        var frame = NewImage("ScreenFrame", body, Gold);
        PlaceTL(frame.rectTransform, ScreenX, ScreenY, ScreenW, ScreenH);
        SetRounded(frame, 12, new Color(Gold.r, Gold.g, Gold.b, 0.7f));

        var screen = NewImage("Screen", frame.transform, ScreenColor);
        Stretch(screen.rectTransform, 2f);
        SetRounded(screen, 10, ScreenColor);
        var s = screen.transform;
        float innerW = ScreenW - 4f;
        float innerH = ScreenH - 4f;

        // ----- 상태 표시줄: 안테나(왼쪽) / 배터리(오른쪽). 시간은 표시하지 않는다 -----
        for (int i = 0; i < 4; i++)
        {
            float h = 6f + i * 3f;
            var bar = NewImage("Signal" + i, s, GoldLight);
            PlaceTL(bar.rectTransform, 14f + i * 7f, 24f - h, 4f, h);
        }
        var battery = NewImage("Battery", s, GoldLight);
        PlaceTL(battery.rectTransform, innerW - 14f - 30f, 10f, 28f, 14f);
        var batteryInner = NewImage("Inner", battery.transform, ScreenColor);
        Stretch(batteryInner.rectTransform, 2f);
        var batteryFill = NewImage("Fill", batteryInner.transform, GoldLight);
        var bfr = batteryFill.rectTransform;
        bfr.anchorMin = new Vector2(0f, 0f);
        bfr.anchorMax = new Vector2(0.7f, 1f);
        bfr.offsetMin = new Vector2(2f, 2f);
        bfr.offsetMax = new Vector2(0f, -2f);
        var batteryNub = NewImage("Nub", s, GoldLight);
        PlaceTL(batteryNub.rectTransform, innerW - 14f - 2f, 14f, 3f, 6f);

        // ----- 제목줄 -----
        titleText = NewText("Title", s, "메인 메뉴", 22, FontStyles.Bold, GoldLight, TextAlignmentOptions.MidlineLeft);
        PlaceTL(titleText.rectTransform, 16f, StatusH, innerW - 32f, TitleH);
        var titleLine = NewImage("TitleLine", s, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
        PlaceTL(titleLine.rectTransform, 12f, StatusH + TitleH, innerW - 24f, 1f);

        // ----- 내용 영역 (제목줄 아래 ~ 소프트키 줄 위) -----
        float contentTop = StatusH + TitleH + 4f;
        float contentH = innerH - contentTop - SoftKeyH;

        menuPage = NewContainer("MenuPage", s, contentTop, contentH, innerW);
        BuildAppGrid(menuPage.transform, innerW, contentH);

        appPage = NewContainer("AppPage", s, contentTop, contentH, innerW);
        var empty = NewText("Empty", appPage.transform, "기록이 없습니다", 18, FontStyles.Normal, GoldDim, TextAlignmentOptions.Center);
        Stretch(empty.rectTransform);

        dialPage = NewContainer("DialPage", s, contentTop, contentH, innerW);
        dialText = NewText("Number", dialPage.transform, "", 40, FontStyles.Bold, GoldLight, TextAlignmentOptions.Center);
        Stretch(dialText.rectTransform, 16f);
        dialText.textWrappingMode = TextWrappingModes.Normal;
        dialText.overflowMode = TextOverflowModes.Overflow;

        // ----- 소프트키 줄 -----
        var softLine = NewImage("SoftKeyLine", s, new Color(Gold.r, Gold.g, Gold.b, 0.35f));
        PlaceTL(softLine.rectTransform, 12f, innerH - SoftKeyH, innerW - 24f, 1f);
        leftSoftKeyText = NewSoftKey("LeftSoftKey", s, 6f, innerH - SoftKeyH, TextAlignmentOptions.MidlineLeft, OnLeftSoftKey);
        rightSoftKeyText = NewSoftKey("RightSoftKey", s, innerW - 6f - 120f, innerH - SoftKeyH, TextAlignmentOptions.MidlineRight, OnRightSoftKey);
    }

    private void BuildAppGrid(Transform parent, float width, float height)
    {
        const int columns = 3, rows = 4;
        float cellW = (width - 24f) / columns;
        float cellH = height / rows;

        for (int i = 0; i < Apps.Length; i++)
        {
            var (id, label) = Apps[i];
            float x = 12f + (i % columns) * cellW;
            float y = (i / columns) * cellH;

            var cell = NewImage("App_" + id, parent, Color.white);
            PlaceTL(cell.rectTransform, x + 3f, y + 2f, cellW - 6f, cellH - 4f);
            SetRounded(cell, 8, Color.white);
            cell.raycastTarget = true;
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.colors = Tint(new Color(Gold.r, Gold.g, Gold.b, 0f),
                                 new Color(Gold.r, Gold.g, Gold.b, 0.16f),
                                 new Color(Gold.r, Gold.g, Gold.b, 0.28f));
            string appLabel = label;
            button.onClick.AddListener(() => OnAppPressed(appLabel));

            var icon = NewImage("Icon", cell.transform, Gold);
            icon.sprite = Resources.Load<Sprite>(IconFolder + "App_" + id);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(40f, 40f);
            irt.anchoredPosition = new Vector2(0f, -12f);

            var text = NewText("Label", cell.transform, label, 15, FontStyles.Normal, GoldLight, TextAlignmentOptions.Top);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(0f, 24f);
            trt.anchoredPosition = new Vector2(0f, 8f);
        }
    }

    private void BuildKeypad(Transform body)
    {
        const float side = 24f;
        float usable = BodyWidth - side * 2f;

        // ----- 통화 / 종료 -----
        const float topKeyW = 140f, topKeyH = 52f;
        var call = NewKey("Key_Call", body, side, KeypadTop, topKeyW, topKeyH, OnCallPressed);
        AddKeyIcon(call, "Key_Call", GoldLight);
        var end = NewKey("Key_End", body, BodyWidth - side - topKeyW, KeypadTop, topKeyW, topKeyH, OnEndPressed);
        AddKeyIcon(end, "Key_End", GoldDim);

        // ----- 숫자 키 4줄 -----
        const float gapX = 14f, gapY = 10f, keyH = 58f;
        float keyW = (usable - gapX * 2f) / 3f;
        float top = KeypadTop + topKeyH + 14f;
        for (int i = 0; i < NumberKeys.Length; i++)
        {
            var (key, sub) = NumberKeys[i];
            float x = side + (i % 3) * (keyW + gapX);
            float y = top + (i / 3) * (keyH + gapY);
            string k = key;
            var btn = NewKey("Key_" + key, body, x, y, keyW, keyH, () => OnKeyPressed(k));

            bool hasSub = !string.IsNullOrEmpty(sub);
            var digit = NewText("Digit", btn.transform, key, 28, FontStyles.Bold, GoldLight, TextAlignmentOptions.Center);
            var drt = digit.rectTransform;
            drt.anchorMin = new Vector2(0f, hasSub ? 0.32f : 0f);
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            if (hasSub)
            {
                var subText = NewText("Sub", btn.transform, sub, 11, FontStyles.Normal, GoldDim, TextAlignmentOptions.Center);
                var srt = subText.rectTransform;
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = new Vector2(1f, 0.38f);
                srt.offsetMin = srt.offsetMax = Vector2.zero;
                subText.characterSpacing = 8f;
            }
        }
    }

    private Image NewKey(string name, Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(name, parent, Color.white);
        PlaceTL(img.rectTransform, x, y, w, h);
        SetRounded(img, 12, Color.white);
        img.raycastTarget = true;
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.colors = Tint(KeyColor, KeyHoverColor, KeyPressColor);
        button.onClick.AddListener(onClick);
        return img;
    }

    private void AddKeyIcon(Image key, string spriteName, Color color)
    {
        var icon = NewImage("Icon", key.transform, color);
        icon.sprite = Resources.Load<Sprite>(IconFolder + spriteName);
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;
        var rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(32f, 32f);
        rt.anchoredPosition = Vector2.zero;
    }

    private TMP_Text NewSoftKey(string name, Transform parent, float x, float y, TextAlignmentOptions align, UnityEngine.Events.UnityAction onClick)
    {
        var hit = NewImage(name, parent, new Color(1f, 1f, 1f, 0f));
        PlaceTL(hit.rectTransform, x, y, 120f, SoftKeyH);
        hit.raycastTarget = true;
        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);

        var text = NewText("Label", hit.transform, "", 18, FontStyles.Normal, GoldLight, align);
        Stretch(text.rectTransform, 10f);
        return text;
    }

    // ---------------------------------------------------------------------------------
    // 작은 도우미들
    // ---------------------------------------------------------------------------------
    private static GameObject NewContainer(string name, Transform parent, float top, float height, float width)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        PlaceTL((RectTransform)go.transform, 0f, top, width, height);
        return go;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // 버튼 색: 바탕 Image를 흰색으로 두고 상태별 색을 곱해서 칠한다.
    private static ColorBlock Tint(Color normal, Color hover, Color pressed)
    {
        var c = ColorBlock.defaultColorBlock;
        c.normalColor = normal;
        c.highlightedColor = hover;
        c.selectedColor = normal;    // 누른 뒤에도 강조가 남지 않게
        c.pressedColor = pressed;
        c.disabledColor = normal;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.06f;
        return c;
    }

    // 부모의 왼쪽 위를 (0,0)으로 보고 x, y(아래 방향), 폭, 높이로 자리를 잡는다.
    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRounded(Image img, int radius, Color color)
    {
        img.sprite = RoundedSprite(radius);
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    // 모서리 반지름 radius인 둥근 사각형 9-slice 스프라이트 (가장자리를 부드럽게 칠한다).
    // 크기와 상관없이 반지름별로 하나만 만들어 재사용한다.
    private static Sprite RoundedSprite(int radius)
    {
        if (roundedCache.TryGetValue(radius, out var cached) && cached != null) return cached;

        int size = radius * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 가장 가까운 모서리 원 중심까지의 거리로 가장자리 덮임 정도를 구한다.
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var border = new Vector4(radius, radius, radius, radius);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        roundedCache[radius] = sprite;
        return sprite;
    }
}
