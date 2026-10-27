using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using static UIKit;   // CreateText, PlaceTopLeft, AddRect, Stretch ... (UIKit.cs)

// =====================================================================================
// 핸드폰 UI - 퀵바의 폰 버튼을 누르면 열리는 화면 (NotePanelUI.cs와 같은 방식으로 작성)
// =====================================================================================
// ===== 디자인 =====
// 2000년대 고급 폴더폰(검은 몸체 + 금색 테두리/경첩/글씨)을 펼친 모습. 위 덮개는 화면,
// 가운데는 경첩, 아래 몸체는 둥근 방향키 + 소프트키/통화/종료 + 숫자 키. (조사 오브젝트 그림과 맞춤)
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
    // 폴더폰을 펼친 모습: 위 덮개(화면) / 경첩 / 아래 몸체(방향키 + 키패드).
    //
    //         ╭────────────╮     ← 덮개 (위 모서리가 크게 둥글다)
    //         │ ┌────────┐ │
    //         │ │  화면   │ │
    //         │ └────────┘ │
    //       ▐█▌════════════▐█▌   ← 경첩 (양 끝 금색)
    //        /[L]  (◎)  [R]\     ← 아래 몸체는 바닥에 눕힌 것처럼 원근으로 그린다:
    //       / [☎]       [⏻] \       경첩 쪽은 좁고 멀리, 아래로 올수록 넓고 가깝게,
    //      /  1    2    3    \      세로는 납작하게 (Tilt 참고)
    //      ╰─────────────────╯
    // 몸체를 눕혀서 생긴 세로 여유만큼 덮개(화면)를 길게 쓴다.
    private const float BodyWidth = 452f;          // 가장 넓은 곳 = 몸체 아래쪽 끝
    private const float Rim = 3f;                  // 금색 테두리 두께
    private const float LidWidth = 384f, LidHeight = 620f;
    private const int LidTopRadius = 92, LidBottomRadius = 24;
    private const float HingeWidth = 400f, HingeHeight = 40f;
    private const float BaseTop = LidHeight + HingeHeight;          // 660
    private const float BaseFlatHeight = 470f;                      // 눕히기 전(평면) 높이
    private const int BaseTopRadius = 18, BaseBottomRadius = 92;
    private const float BaseTiltSquash = 0.6f;                      // 눕혔을 때 세로로 줄어드는 비율
    private const float BaseThickness = 28f;                        // 눕힌 몸체 아래로 보이는 옆면 두께
    private const float BodyHeight = BaseTop + BaseFlatHeight * BaseTiltSquash + BaseThickness;   // 970
    private const float ScreenX = 19f, ScreenY = 58f, ScreenW = 340f, ScreenH = 532f;  // 덮개 안 좌표 (안쪽 378x614)
    private const float StatusH = 30f, TitleH = 44f, SoftKeyH = 48f;

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
    private RectTransform lidRoot;      // 덮개 (열 때 경첩을 축으로 펼쳐진다)
    private CanvasGroup phoneGroup;
    private TMP_Text titleText;
    private TMP_Text leftSoftKeyText;
    private TMP_Text rightSoftKeyText;
    private GameObject menuPage, appPage, dialPage;
    private TMP_Text dialText;
    private Page page = Page.Menu;
    private string dialedNumber = "";

    private Coroutine openRoutine;

    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    private void Awake()
    {
        Instance = this;
        HideOriginalPanelVisuals();

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
        UILayers.Place(overlay, UILayer.Panel);   // 퀵바 창 층 (UILayers.cs 참고)

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

    private void PlayClick() => AudioManager.PlaySfx(AudioManager.Sfx.Click);

    // 주머니에서 꺼내듯 아래에서 짧게 올라오면서, 덮개가 경첩을 축으로 탁 펼쳐진다.
    // 일시정지 중에도 돌도록 unscaled 시간을 쓴다.
    private IEnumerator PlayOpenAnimation()
    {
        const float duration = 0.22f;
        const float startOffset = -140f;
        const float lidStartScale = 0.12f;   // 덮개가 거의 접힌 상태에서 시작
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);   // 끝으로 갈수록 부드럽게 멈춤
            phoneRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(startOffset, 0f, ease));
            phoneGroup.alpha = ease;
            if (lidRoot != null) lidRoot.localScale = new Vector3(1f, Mathf.Lerp(lidStartScale, 1f, ease), 1f);
            yield return null;
        }
        phoneRoot.anchoredPosition = Vector2.zero;
        phoneGroup.alpha = 1f;
        if (lidRoot != null) lidRoot.localScale = Vector3.one;
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
        Canvas canvas = UILayers.RootCanvas();
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
        // 창 등록(Esc/대사 막기)은 UIManager가 phonePanel로 하고, 여기는 그 창의 실제 화면이다 (ModalStack.cs 참고).
        ModalStack.MarkNotWindow(overlay, "핸드폰 창의 화면. 창 등록은 UIManager가 phonePanel로 한다.");

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

        // ----- 핸드폰 전체 묶음. 이 묶음이 열릴 때 올라오는 애니메이션 대상 -----
        var root = new GameObject("Phone", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(overlay.transform, false);
        phoneRoot = (RectTransform)root.transform;
        phoneRoot.anchorMin = phoneRoot.anchorMax = phoneRoot.pivot = new Vector2(0.5f, 0.5f);
        phoneRoot.sizeDelta = new Vector2(BodyWidth, BodyHeight);
        phoneGroup = root.GetComponent<CanvasGroup>();
        var hitArea = root.GetComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;   // 핸드폰 위를 눌러도 뒤의 막(바깥 클릭 = 닫기)까지 새지 않게

        // 몸체 옆면(두께) -> 아래 몸체 -> 덮개 -> 경첩 순으로 그린다 (경첩이 둘 사이를 덮는다).
        // 몸체는 평면으로 짠 뒤 통째로 원근 변형한다 (ApplyTilt).
        BuildBaseEdge(root.transform);
        var baseBody = BuildShell("Base", root.transform, 0f, BaseTop, BodyWidth, BaseFlatHeight, BaseTopRadius, BaseBottomRadius);
        BuildKeypad(baseBody);
        ApplyTilt((RectTransform)baseBody.parent);

        // 덮개: 경첩(아래쪽 가운데)을 축으로 펼쳐지는 애니메이션을 위해 피벗을 아래로 둔다.
        var lid = BuildShell("Lid", root.transform, (BodyWidth - LidWidth) / 2f, 0f, LidWidth, LidHeight, LidTopRadius, LidBottomRadius);
        lidRoot = (RectTransform)lid.parent;
        lidRoot.pivot = new Vector2(0.5f, 0f);
        lidRoot.anchoredPosition = new Vector2(lidRoot.anchoredPosition.x + LidWidth / 2f, -LidHeight);

        var slit = NewImage("Speaker", lid, GoldDim);
        PlaceTL(slit.rectTransform, LidWidth / 2f - 30f, 16f, 60f, 6f);
        SetRounded(slit, 3, GoldDim);
        BuildScreen(lid);

        BuildHinge(root.transform);

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
            irt.sizeDelta = new Vector2(42f, 42f);
            irt.anchoredPosition = new Vector2(0f, -14f);

            // 칸 높이 약 100px: 아이콘(위 14~56) 아래로 글자(아래 10~32)가 겹치지 않게 놓는다.
            var text = NewText("Label", cell.transform, label, 16, FontStyles.Normal, GoldLight, TextAlignmentOptions.Bottom);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(0f, 22f);
            trt.anchoredPosition = new Vector2(0f, 10f);
        }
    }

    // ---------------------------------------------------------------------------------
    // 껍데기 (덮개 / 아래 몸체)
    // ---------------------------------------------------------------------------------
    // 금색 테두리 + 검은 면. 위/아래 모서리 둥글기가 달라서(덮개는 위가 둥글고, 몸체는 아래가 둥글다)
    // 둥근 사각형 두 장을 겹쳐 그린다: 큰 반지름 한 장(전체) + 작은 반지름 한 장(작은 쪽 절반).
    // 내용물을 놓을 안쪽(테두리 안) Transform을 돌려준다. 그 부모가 껍데기 전체 묶음이다.
    private Transform BuildShell(string name, Transform parent, float x, float y, float w, float h, int topRadius, int bottomRadius)
    {
        var group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        var grt = (RectTransform)group.transform;
        PlaceTL(grt, x, y, w, h);

        AddMixedRounded(grt, "Rim", Gold, 0f, topRadius, bottomRadius);
        AddMixedRounded(grt, "Face", BodyColor, Rim, Mathf.Max(2, topRadius - (int)Rim), Mathf.Max(2, bottomRadius - (int)Rim));

        // 테두리 안쪽에 한 줄 더 들어간 가는 금색 선 (그림의 몸체 가장자리 장식)
        var innerLine = new GameObject("InnerLine", typeof(RectTransform));
        innerLine.transform.SetParent(grt, false);
        Stretch((RectTransform)innerLine.transform, Rim + 6f);
        AddMixedRounded((RectTransform)innerLine.transform, "Line", new Color(Gold.r, Gold.g, Gold.b, 0.22f), 0f,
                        Mathf.Max(2, topRadius - 9), Mathf.Max(2, bottomRadius - 9));
        AddMixedRounded((RectTransform)innerLine.transform, "Fill", BodyColor, 1f,
                        Mathf.Max(2, topRadius - 10), Mathf.Max(2, bottomRadius - 10));

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(grt, false);
        Stretch((RectTransform)inner.transform, Rim);
        return inner.transform;
    }

    private static void AddMixedRounded(RectTransform parent, string name, Color color, float inset, int topRadius, int bottomRadius)
    {
        int big = Mathf.Max(topRadius, bottomRadius);
        int small = Mathf.Min(topRadius, bottomRadius);
        bool smallAtTop = topRadius < bottomRadius;

        var full = NewImage(name + "_A", parent, color);
        Stretch(full.rectTransform, inset);
        SetRounded(full, big, color);

        if (small == big) return;
        var half = NewImage(name + "_B", parent, color);
        var hrt = half.rectTransform;
        hrt.anchorMin = new Vector2(0f, smallAtTop ? 0.5f : 0f);
        hrt.anchorMax = new Vector2(1f, smallAtTop ? 1f : 0.5f);
        hrt.offsetMin = new Vector2(inset, smallAtTop ? 0f : inset);
        hrt.offsetMax = new Vector2(-inset, smallAtTop ? -inset : 0f);
        SetRounded(half, small, color);
    }

    // 몸체 옆면: 몸체와 같은 모양(눕힌 사다리꼴)을 BaseThickness만큼 아래로 내려 뒤에 깐다.
    // 몸체에 가려지고 남은 아래쪽 띠(와 아래로 넓어지는 양옆 가장자리)가 두께처럼 보인다.
    //   위에서 아래로: 흐린 금색 테 -> 어두운 옆면 -> 아래 끝에 한 번 더 흐린 금색 선(바닥에 닿는 모서리)
    private static void BuildBaseEdge(Transform root)
    {
        var group = new GameObject("BaseEdge", typeof(RectTransform));
        group.transform.SetParent(root, false);
        var grt = (RectTransform)group.transform;
        PlaceTL(grt, 0f, BaseTop + BaseThickness, BodyWidth, BaseFlatHeight);

        Color side = new Color32(0x06, 0x06, 0x05, 0xFF);
        AddMixedRounded(grt, "EdgeRim", GoldDim, 0f, BaseTopRadius, BaseBottomRadius);
        AddMixedRounded(grt, "EdgeFace", side, 2f, BaseTopRadius - 2, BaseBottomRadius - 2);

        foreach (var image in grt.GetComponentsInChildren<Image>(true))
        {
            image.gameObject.AddComponent<TiltMesh>().plane = grt;
            image.raycastTarget = false;
        }
    }

    // 경첩: 덮개와 몸체 사이를 가로지르는 원통 + 양 끝 금색 마개.
    private void BuildHinge(Transform root)
    {
        float top = LidHeight - 8f;
        float height = HingeHeight + 16f;
        float hx = (BodyWidth - HingeWidth) / 2f;

        var barrel = NewImage("Hinge", root, new Color32(0x1A, 0x18, 0x14, 0xFF));
        PlaceTL(barrel.rectTransform, hx + 30f, top, HingeWidth - 60f, height);
        SetRounded(barrel, 14, new Color32(0x1A, 0x18, 0x14, 0xFF));
        // 원통처럼 보이게 가운데 밝은 줄 + 위아래 금색 가는 선
        var shine = NewImage("Shine", barrel.transform, new Color32(0x2E, 0x2A, 0x23, 0xFF));
        var srt = shine.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.42f); srt.anchorMax = new Vector2(1f, 0.62f);
        srt.offsetMin = new Vector2(10f, 0f); srt.offsetMax = new Vector2(-10f, 0f);
        foreach (float yLine in new[] { 2f, height - 3f })
        {
            var line = NewImage("Edge", barrel.transform, new Color(Gold.r, Gold.g, Gold.b, 0.35f));
            PlaceTL(line.rectTransform, 12f, yLine, HingeWidth - 84f, 1f);
        }

        // 양 끝 금색 마개
        foreach (float x in new[] { hx, hx + HingeWidth - 62f })
        {
            var cap = NewImage("HingeCap", root, Gold);
            PlaceTL(cap.rectTransform, x, top, 62f, height);
            SetRounded(cap, 16, Gold);
            var capShine = NewImage("Shine", cap.transform, GoldLight);
            var crt = capShine.rectTransform;
            crt.anchorMin = new Vector2(0f, 0.55f); crt.anchorMax = new Vector2(1f, 0.75f);
            crt.offsetMin = new Vector2(8f, 0f); crt.offsetMax = new Vector2(-8f, 0f);
            SetRounded(capShine, 4, GoldLight);
            var capShade = NewImage("Shade", cap.transform, GoldDim);
            var cdt = capShade.rectTransform;
            cdt.anchorMin = new Vector2(0f, 0f); cdt.anchorMax = new Vector2(1f, 0.22f);
            cdt.offsetMin = new Vector2(6f, 4f); cdt.offsetMax = new Vector2(-6f, 0f);
            SetRounded(capShade, 4, GoldDim);
        }
    }

    // ---------------------------------------------------------------------------------
    // 아래 몸체: 방향키 + 소프트키/통화/종료 + 숫자 키
    // ---------------------------------------------------------------------------------
    private void BuildKeypad(Transform body)
    {
        float innerW = BodyWidth - Rim * 2f;
        const float side = 34f;
        float usable = innerW - side * 2f;

        // ----- 가운데 둥근 방향키 (금색 테 + 검은 원 + 가운데 확인 버튼) -----
        const float navTop = 28f, navSize = 130f;
        float navX = innerW / 2f - navSize / 2f;
        var navRim = NewImage("NavRing", body, Gold);
        PlaceTL(navRim.rectTransform, navX, navTop, navSize, navSize);
        SetRounded(navRim, (int)(navSize / 2f), Gold);
        var navFace = NewImage("NavFace", navRim.transform, KeyColor);
        Stretch(navFace.rectTransform, 3f);
        SetRounded(navFace, (int)(navSize / 2f) - 3, KeyColor);
        // 위/아래/왼/오른쪽 작은 점 (방향 표시)
        foreach (var (dx, dy) in new[] { (0f, 1f), (0f, -1f), (-1f, 0f), (1f, 0f) })
        {
            var dot = NewImage("Dir", navFace.transform, GoldDim);
            var drt0 = dot.rectTransform;
            drt0.anchorMin = drt0.anchorMax = drt0.pivot = new Vector2(0.5f, 0.5f);
            drt0.sizeDelta = new Vector2(8f, 8f);
            drt0.anchoredPosition = new Vector2(dx * 46f, dy * 46f);
            SetRounded(dot, 4, GoldDim);
        }
        // 가운데 확인 버튼: 메뉴에서는 하는 일이 없고, 전화 걸기에서는 통화와 같다.
        var ok = NewKey("Key_OK", navFace.transform, 0f, 0f, 52f, 52f, OnCallPressed);
        var okRt = ok.rectTransform;
        okRt.anchorMin = okRt.anchorMax = okRt.pivot = new Vector2(0.5f, 0.5f);
        okRt.anchoredPosition = Vector2.zero;
        SetRounded(ok, 26, Color.white);
        var okRing = NewImage("Ring", ok.transform, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        Stretch(okRing.rectTransform, 0f);
        SetRounded(okRing, 26, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        var okFace = NewImage("Face", okRing.transform, Color.white);
        Stretch(okFace.rectTransform, 2f);
        SetRounded(okFace, 24, Color.white);   // 흰색에 버튼 상태 색(KeyColor 등)이 곱해진다
        okFace.raycastTarget = false;
        ok.GetComponent<Button>().targetGraphic = okFace;
        ok.color = new Color(1f, 1f, 1f, 0f);  // 클릭만 받고 모양은 링/면이 그린다

        // ----- 방향키 양옆: 소프트키(위) / 통화·종료(아래) -----
        float sideKeyW = navX - side - 12f;
        const float sideKeyH = 50f;
        var leftSoft = NewKey("Key_SoftLeft", body, side, navTop + 4f, sideKeyW, sideKeyH, OnLeftSoftKey);
        AddSoftKeyMark(leftSoft);
        var rightSoft = NewKey("Key_SoftRight", body, innerW - side - sideKeyW, navTop + 4f, sideKeyW, sideKeyH, OnRightSoftKey);
        AddSoftKeyMark(rightSoft);
        var call = NewKey("Key_Call", body, side, navTop + navSize - sideKeyH - 4f, sideKeyW, sideKeyH, OnCallPressed);
        AddKeyIcon(call, "Key_Call", GoldLight);
        var end = NewKey("Key_End", body, innerW - side - sideKeyW, navTop + navSize - sideKeyH - 4f, sideKeyW, sideKeyH, OnEndPressed);
        AddKeyIcon(end, "Key_End", GoldDim);

        // ----- 숫자 키 4줄 -----
        // 눕혀서 세로로 납작해지므로 평면에서는 키를 넉넉히 높게 잡는다.
        const float gapX = 12f, gapY = 10f, keyH = 52f;
        float keyW = (usable - gapX * 2f) / 3f;
        float top = navTop + navSize + 18f;
        for (int i = 0; i < NumberKeys.Length; i++)
        {
            var (key, sub) = NumberKeys[i];
            float x = side + (i % 3) * (keyW + gapX);
            float y = top + (i / 3) * (keyH + gapY);
            string k = key;
            var btn = NewKey("Key_" + key, body, x, y, keyW, keyH, () => OnKeyPressed(k));

            // 폴더폰 키처럼 숫자와 글자를 한 줄에 나란히: "2 ABC" (키가 낮고 넓어서 위아래로 쌓으면 잘린다)
            string label = string.IsNullOrEmpty(sub)
                ? key
                : key + "<space=8><size=12><color=#" + ColorUtility.ToHtmlStringRGB(GoldDim) + "><b>" + sub + "</b></color></size>";
            var digit = NewText("Digit", btn.transform, label, 28, FontStyles.Bold, GoldLight, TextAlignmentOptions.Center);
            digit.richText = true;
            digit.overflowMode = TextOverflowModes.Overflow;
            Stretch(digit.rectTransform);
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

    // 소프트키 위의 짧은 금색 가로줄 (화면 아래 "선택"/"닫기" 글자와 짝이 되는 실제 버튼 표시)
    private static void AddSoftKeyMark(Image key)
    {
        var mark = NewImage("Mark", key.transform, GoldLight);
        var rt = mark.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(26f, 4f);
        rt.anchoredPosition = Vector2.zero;
        SetRounded(mark, 2, GoldLight);
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
    // 아래 몸체 눕히기 (원근)
    // ---------------------------------------------------------------------------------
    // uGUI(오버레이 캔버스)는 3D로 기울여도 원근이 생기지 않는다. 그래서 몸체를 평면으로 다 짠 뒤,
    // 그 안의 모든 그림(Image)과 글자(TMP)의 꼭짓점을 같은 공식(Tilt.Warp)으로 옮겨 그린다.
    //   - 경첩 쪽(위)은 좁고(HingeWidth), 아래로 올수록 넓어진다(BodyWidth).
    //   - 세로는 BaseTiltSquash만큼 줄고, 먼 쪽(위)일수록 더 많이 줄어든다.
    // 키의 클릭 영역(RectTransform)은 평면 자리에 그대로 있으므로, 몸체 위에 투명한 판(TiltRouter)을
    // 하나 깔아서 눌린 점을 거꾸로 펴서(Tilt.Unwarp) 그 자리의 키에 클릭/마우스 올림을 넘겨준다.
    private static void ApplyTilt(RectTransform plane)
    {
        foreach (var image in plane.GetComponentsInChildren<Image>(true))
        {
            image.gameObject.AddComponent<TiltMesh>().plane = plane;
            image.raycastTarget = false;
        }
        foreach (var text in plane.GetComponentsInChildren<TMP_Text>(true))
        {
            text.gameObject.AddComponent<TiltText>().plane = plane;
        }

        var router = NewImage("TiltRouter", plane, new Color(0f, 0f, 0f, 0f));
        Stretch(router.rectTransform);
        router.raycastTarget = true;
        router.gameObject.AddComponent<TiltRouter>().Init(plane);
    }

    private static class Tilt
    {
        private const float TopScale = HingeWidth / BodyWidth;          // 경첩 쪽 폭 비율
        private const float Norm = TopScale + (1f - TopScale) / 2f;

        // 평면 좌표 -> 눕힌 좌표 (plane의 로컬 좌표, 위쪽 가장자리 = 경첩)
        public static Vector3 Warp(Rect r, Vector3 p)
        {
            float t = (r.yMax - p.y) / r.height;                        // 0 = 경첩, 1 = 아래 끝
            float s = TopScale + (1f - TopScale) * t;                    // 그 줄의 가로 배율
            float g = (TopScale * t + (1f - TopScale) * t * t / 2f) / Norm;   // 먼 쪽일수록 세로가 더 줄어든다
            float cx = r.center.x;
            return new Vector3(cx + (p.x - cx) * s, r.yMax - r.height * BaseTiltSquash * g, p.z);
        }

        // 눕힌 좌표 -> 평면 좌표 (Warp의 역)
        public static Vector2 Unwarp(Rect r, Vector2 q)
        {
            float g = (r.yMax - q.y) / (r.height * BaseTiltSquash);
            float a = 1f - TopScale;
            float t = a < 1e-4f ? g : (-TopScale + Mathf.Sqrt(Mathf.Max(0f, TopScale * TopScale + 2f * a * g * Norm))) / a;
            float s = TopScale + a * t;
            float cx = r.center.x;
            return new Vector2(cx + (q.x - cx) / s, r.yMax - t * r.height);
        }
    }

    // 그림 꼭짓점 옮기기
    private class TiltMesh : BaseMeshEffect
    {
        public RectTransform plane;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || plane == null) return;
            Matrix4x4 toPlane = plane.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromPlane = toPlane.inverse;
            Rect r = plane.rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = fromPlane.MultiplyPoint3x4(Tilt.Warp(r, toPlane.MultiplyPoint3x4(v.position)));
                vh.SetUIVertex(v, i);
            }
        }
    }

    // 글자 꼭짓점 옮기기 (TMP는 BaseMeshEffect를 안 거치므로 그려지기 직전에 직접 옮긴다)
    private class TiltText : MonoBehaviour
    {
        public RectTransform plane;
        private TMP_Text text;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            if (text == null) return;
            text.OnPreRenderText += Warp;
            text.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (text != null) text.OnPreRenderText -= Warp;
        }

        private void Warp(TMP_TextInfo info)
        {
            if (plane == null) return;
            Matrix4x4 toPlane = plane.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromPlane = toPlane.inverse;
            Rect r = plane.rect;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var verts = info.meshInfo[m].vertices;
                if (verts == null) continue;
                int count = Mathf.Min(info.meshInfo[m].vertexCount, verts.Length);
                for (int i = 0; i < count; i++)
                {
                    verts[i] = fromPlane.MultiplyPoint3x4(Tilt.Warp(r, toPlane.MultiplyPoint3x4(verts[i])));
                }
            }
        }
    }

    // 눌린 점을 평면으로 펴서 그 자리의 키에 마우스 이벤트를 넘긴다.
    private class TiltRouter : MonoBehaviour, ICanvasRaycastFilter,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private RectTransform plane;
        private Button[] buttons;
        private Button hovered, pressed;

        public void Init(RectTransform target)
        {
            plane = target;
            buttons = plane.GetComponentsInChildren<Button>(true);
        }

        // 눕힌 몸체 바깥(평면 사각형 중 화면에 안 그려지는 아랫부분)은 클릭을 통과시킨다.
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!ToFlat(screenPoint, eventCamera, out Vector2 flat)) return false;
            Rect r = plane.rect;
            return flat.y >= r.yMin && flat.y <= r.yMax && flat.x >= r.xMin && flat.x <= r.xMax;
        }

        private bool ToFlat(Vector2 screenPoint, Camera cam, out Vector2 flat)
        {
            flat = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plane, screenPoint, cam, out Vector2 local)) return false;
            flat = Tilt.Unwarp(plane.rect, local);
            return true;
        }

        private Button Find(PointerEventData e)
        {
            if (!ToFlat(e.position, e.enterEventCamera != null ? e.enterEventCamera : e.pressEventCamera, out Vector2 flat)) return null;
            Button best = null;
            foreach (var b in buttons)
            {
                if (b == null || !b.isActiveAndEnabled || !b.interactable) continue;
                var rt = (RectTransform)b.transform;
                Matrix4x4 toPlane = plane.worldToLocalMatrix * rt.localToWorldMatrix;
                Rect br = rt.rect;
                Vector2 min = toPlane.MultiplyPoint3x4(br.min);
                Vector2 max = toPlane.MultiplyPoint3x4(br.max);
                if (flat.x >= Mathf.Min(min.x, max.x) && flat.x <= Mathf.Max(min.x, max.x) &&
                    flat.y >= Mathf.Min(min.y, max.y) && flat.y <= Mathf.Max(min.y, max.y))
                {
                    best = b;   // 겹치면 나중(안쪽, 예: 방향키 가운데 확인 버튼)이 이긴다
                }
            }
            return best;
        }

        private void SetHovered(Button b, PointerEventData e)
        {
            if (b == hovered) return;
            if (hovered != null) ExecuteEvents.Execute(hovered.gameObject, e, ExecuteEvents.pointerExitHandler);
            hovered = b;
            if (hovered != null) ExecuteEvents.Execute(hovered.gameObject, e, ExecuteEvents.pointerEnterHandler);
        }

        public void OnPointerMove(PointerEventData e) => SetHovered(Find(e), e);
        public void OnPointerExit(PointerEventData e) => SetHovered(null, e);

        public void OnPointerDown(PointerEventData e)
        {
            pressed = Find(e);
            if (pressed != null) ExecuteEvents.Execute(pressed.gameObject, e, ExecuteEvents.pointerDownHandler);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pressed != null) ExecuteEvents.Execute(pressed.gameObject, e, ExecuteEvents.pointerUpHandler);
        }

        public void OnPointerClick(PointerEventData e)
        {
            var b = Find(e);
            if (b != null && b == pressed) b.onClick.Invoke();
            pressed = null;
        }

        private void OnDisable()
        {
            hovered = null;
            pressed = null;
        }
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
