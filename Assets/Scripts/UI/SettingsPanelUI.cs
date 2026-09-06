using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 환경설정 화면 - 코드로 직접 만드는 하나짜리 설정 UI
// =====================================================================================
// ===== 왜 코드로 만드나 =====
// 원래는 Settings.unity라는 별도 씬을 인게임 위에 "겹쳐 띄우는(additive)" 방식이었는데,
// 그 방식에서 문제가 계속 나왔다:
//   - 뒤로가기를 눌러도 씬이 내려가기까지 몇 프레임이 걸려, 그 사이 옵션 창이
//     게임 화면 뒤에 깔린 것처럼 보였다.
//   - 두 씬의 캔버스가 서로 다른 기준으로 그려져 앞뒤 순서가 뒤엉켰다.
//   - 씬에 이미 짜여 있던 UI에 새 항목을 끼워 넣으려다 보니 버튼이 잘리거나 겹쳤다.
// 그래서 씬을 오가지 않고, 이 스크립트가 설정 화면 전체를 코드로 만들어 켜고 끈다.
// 배치를 전부 코드가 정하므로 항목을 추가해도 잘리거나 겹치지 않는다.
//
// ===== 화면 구성 =====
//   [화면 전체를 덮는 어두운 막]
//     [가운데 상자]
//        ── 제목: 환경설정
//        ── 위쪽 탭:  [ 사운드 ] [ 화면 ] [ 텍스트 ]      (고른 탭에 밑줄 + 강조색)
//        ── 가운데:   고른 탭의 조절 항목들
//        ── 아래쪽:   [메인 화면으로]        [돌아가기] [종료하기]
//
// ===== 쓰는 법 =====
// SettingsPanelUI.Instance.Open() / Close() 로 여닫는다.
// 씬에 미리 만들어둘 것은 없다. UIManager나 GameBootstrap이 필요할 때 알아서 만든다.
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance;

    // 설정 분류(위쪽 탭).
    private enum Category { Sound, Display, Text }

    // ===== 화면 크기 기준값 =====
    // 캔버스가 1440x1080이라는 전제로 잡은 값이다.
    private const float BoxWidth = 1000f;
    private const float BoxHeight = 780f;
    private const float TitleHeight = 72f;     // 제목 영역
    private const float TabHeight = 58f;       // 위쪽 탭 영역
    private const float FooterHeight = 92f;    // 아래쪽 버튼 영역
    private const float ContentPadding = 44f;  // 조절 항목 좌우 여백
    private const float RowHeight = 54f;       // 항목 한 줄 높이
    private const float RowGap = 16f;          // 항목 사이 간격
    private const float LabelWidth = 300f;     // 항목 이름 칸 너비

    // ===== 색 =====
    private static readonly Color Accent = new Color(1f, 0.84f, 0.42f);        // 강조(금색)
    private static readonly Color TextMain = new Color(0.95f, 0.95f, 0.93f);   // 본문 글자
    private static readonly Color TextDim = new Color(0.66f, 0.66f, 0.64f);    // 설명 글자
    private static readonly Color BoxBg = new Color(0f, 0f, 0f, 0.97f);        // 상자 배경
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.16f);    // 구분선

    private GameObject panel;
    private RectTransform contentArea;
    private readonly Dictionary<Category, GameObject> pages = new Dictionary<Category, GameObject>();
    private readonly Dictionary<Category, Button> tabButtons = new Dictionary<Category, Button>();
    private readonly Dictionary<Category, Image> tabUnderlines = new Dictionary<Category, Image>();

    private TMP_Text fontPreviewText;
    private Button mainMenuButton;
    private bool mainMenuConfirming;

    // 열 때마다 모든 항목을 현재 설정값으로 다시 맞추기 위한 갱신 함수 목록.
    private readonly List<System.Action> refreshActions = new List<System.Action>();

    // 다른 스크립트가 "지금 설정 창이 열려 있나?"를 확인할 때 쓴다.
    public bool IsOpen => panel != null && panel.activeSelf;

    // 닫기(돌아가기) 버튼을 눌렀을 때 대신 실행할 동작.
    // 타이틀에서 들어온 독립 설정 화면처럼 "닫으면 이전 화면으로 돌아가야" 하는 경우에 쓴다.
    public System.Action onClose;

    [Header("메인 화면 씬 이름")]
    public string titleSceneName = "Title";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        Build();
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 여닫기
    // ---------------------------------------------------------------------------------
    public void Open()
    {
        if (panel == null) return;

        RefreshAllControls();

        mainMenuConfirming = false;
        SetMainMenuLabel("메인 화면으로");

        ShowCategory(Category.Sound);

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();   // 다른 UI보다 항상 위에
    }

    public void Close()
    {
        if (onClose != null)
        {
            onClose.Invoke();
            return;
        }
        HidePanel();
    }

    // 콜백을 거치지 않고 무조건 화면에서 감춘다.
    public void HidePanel()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // 타이틀에서 들어온 독립 설정 화면에서는 이미 타이틀에 있는 셈이므로
    // "메인 화면으로" 버튼이 필요 없다. 그럴 때 숨긴다.
    public void SetMainMenuButtonVisible(bool visible)
    {
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(visible);
    }

    // ---------------------------------------------------------------------------------
    // 화면 만들기
    // ---------------------------------------------------------------------------------
    private void Build()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SettingsPanelUI] 씬에 Canvas가 없어 설정 화면을 만들 수 없습니다.");
            return;
        }

        // ----- 화면 전체를 덮는 어두운 막 -----
        panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        var dim = panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 가운데 상자 -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = BoxBg;

        // 상자 테두리(위쪽 강조선) - 밋밋한 검은 판에 포인트를 준다.
        var topAccent = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
        topAccent.transform.SetParent(box.transform, false);
        var taRt = topAccent.GetComponent<RectTransform>();
        taRt.anchorMin = new Vector2(0f, 1f);
        taRt.anchorMax = new Vector2(1f, 1f);
        taRt.pivot = new Vector2(0.5f, 1f);
        taRt.offsetMin = Vector2.zero;
        taRt.offsetMax = Vector2.zero;
        taRt.sizeDelta = new Vector2(0f, 3f);
        taRt.anchoredPosition = Vector2.zero;
        var taImg = topAccent.GetComponent<Image>();
        taImg.color = Accent;
        taImg.raycastTarget = false;

        // ----- 제목 -----
        var title = CreateLabel(box.transform, "Title", "환경설정", 34, TextAlignmentOptions.Center);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(ContentPadding, 0f);
        titleRt.offsetMax = new Vector2(-ContentPadding, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, TitleHeight);
        titleRt.anchoredPosition = new Vector2(0f, -6f);
        title.fontStyle = FontStyles.Bold;
        title.color = Accent;

        // ----- 위쪽 탭 -----
        BuildTabBar(box.transform);

        // 탭 아래 구분선
        CreateDivider(box.transform, -(TitleHeight + TabHeight));

        // ----- 가운데: 조절 항목 영역 -----
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(box.transform, false);
        contentArea = content.GetComponent<RectTransform>();
        contentArea.anchorMin = new Vector2(0f, 0f);
        contentArea.anchorMax = new Vector2(1f, 1f);
        contentArea.offsetMin = new Vector2(ContentPadding, FooterHeight);
        contentArea.offsetMax = new Vector2(-ContentPadding, -(TitleHeight + TabHeight + 24f));

        BuildSoundPage();
        BuildDisplayPage();
        BuildTextPage();

        // ----- 아래쪽 버튼 -----
        CreateDivider(box.transform, -(BoxHeight - FooterHeight));
        BuildFooter(box.transform);

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨진다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    // ===== 위쪽 탭 =====
    // 탭 세 개를 가로로 나란히 놓는다. 고른 탭은 글자가 강조색이 되고 아래에 밑줄이 켜진다.
    private void BuildTabBar(Transform box)
    {
        var bar = new GameObject("TabBar", typeof(RectTransform));
        bar.transform.SetParent(box, false);
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.offsetMin = new Vector2(ContentPadding, 0f);
        barRt.offsetMax = new Vector2(-ContentPadding, 0f);
        barRt.sizeDelta = new Vector2(barRt.sizeDelta.x, TabHeight);
        barRt.anchoredPosition = new Vector2(0f, -TitleHeight);

        CreateTabButton(bar.transform, Category.Sound, "사운드", 0, 3);
        CreateTabButton(bar.transform, Category.Display, "화면", 1, 3);
        CreateTabButton(bar.transform, Category.Text, "텍스트", 2, 3);
    }

    // 탭 버튼 하나. index/total로 가로를 균등하게 나눠 앉힌다.
    private void CreateTabButton(Transform parent, Category category, string label, int index, int total)
    {
        var go = new GameObject($"Tab_{category}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2((float)index / total, 0f);
        rt.anchorMax = new Vector2((float)(index + 1) / total, 1f);
        rt.offsetMin = new Vector2(4f, 6f);
        rt.offsetMax = new Vector2(-4f, 0f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.05f);
        bg.raycastTarget = true;   // 꺼져 있으면 탭이 눌리지 않는다

        var text = CreateLabel(go.transform, "Text", label, 24, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyles.Bold;

        // 선택 표시용 밑줄 (선택된 탭만 켜진다)
        var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(go.transform, false);
        var uRt = underline.GetComponent<RectTransform>();
        uRt.anchorMin = new Vector2(0f, 0f);
        uRt.anchorMax = new Vector2(1f, 0f);
        uRt.pivot = new Vector2(0.5f, 0f);
        uRt.offsetMin = Vector2.zero;
        uRt.offsetMax = Vector2.zero;
        uRt.sizeDelta = new Vector2(0f, 3f);
        uRt.anchoredPosition = Vector2.zero;
        var uImg = underline.GetComponent<Image>();
        uImg.color = Accent;
        uImg.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.45f);
        btn.colors = colors;
        btn.onClick.AddListener(() => ShowCategory(category));

        tabButtons[category] = btn;
        tabUnderlines[category] = uImg;
    }

    // 고른 탭의 내용만 보여주고, 탭 모양도 그에 맞게 바꾼다.
    private void ShowCategory(Category category)
    {
        foreach (var pair in pages)
        {
            if (pair.Value != null) pair.Value.SetActive(pair.Key == category);
        }

        foreach (var pair in tabButtons)
        {
            bool selected = pair.Key == category;

            var text = pair.Value != null ? pair.Value.GetComponentInChildren<TMP_Text>() : null;
            if (text != null) text.color = selected ? Accent : TextDim;

            if (tabUnderlines.TryGetValue(pair.Key, out var underline) && underline != null)
            {
                underline.enabled = selected;
            }
        }
    }

    // ===== 아래쪽 버튼 =====
    //   왼쪽 : 메인 화면으로 (게임 도중에만 의미가 있다)
    //   오른쪽: 돌아가기 / 종료하기
    private void BuildFooter(Transform box)
    {
        mainMenuButton = CreateButton(box, "Btn_MainMenu", "메인 화면으로",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(ContentPadding, 22f), new Vector2(220f, 50f),
            OnClickMainMenu, subtle: true);

        // 종료하기 (맨 오른쪽)
        CreateButton(box, "Btn_Quit", "종료하기",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-ContentPadding, 22f), new Vector2(180f, 50f),
            OnClickQuit, subtle: true);

        // 돌아가기 (종료하기 왼쪽)
        CreateButton(box, "Btn_Back", "돌아가기",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-(ContentPadding + 180f + 12f), 22f), new Vector2(180f, 50f),
            Close, subtle: false);
    }

    private void OnClickQuit()
    {
        // 에디터에서는 플레이 모드를 끄고, 실제 빌드에서는 애플리케이션을 종료한다.
        // (Application.Quit()은 에디터 플레이 모드에서 아무 효과가 없어서 분기한다)
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------------------------
    // 탭별 조절 항목
    // ---------------------------------------------------------------------------------
    private void BuildSoundPage()
    {
        var page = CreatePage(Category.Sound);
        int row = 0;

        CreateSliderRow(page, row++, "전체 소리", 0f, 1f,
            () => Settings.masterVolume,
            v => SettingsManager.Instance.SetMasterVolume(v), percent: true);

        CreateSliderRow(page, row++, "배경음악", 0f, 1f,
            () => Settings.bgmVolume,
            v => SettingsManager.Instance.SetBgmVolume(v), percent: true);

        CreateSliderRow(page, row++, "효과음", 0f, 1f,
            () => Settings.sfxVolume,
            v => SettingsManager.Instance.SetSfxVolume(v), percent: true);

        CreateNoteRow(page, row, "전체 소리는 배경음악과 효과음 모두에 함께 적용됩니다.");
    }

    private void BuildDisplayPage()
    {
        var page = CreatePage(Category.Display);
        int row = 0;

        CreateToggleRow(page, row++, "창 모드",
            () => Settings.windowed,
            v => SettingsManager.Instance.SetWindowed(v));

        CreateNoteRow(page, row,
            "전체화면에서는 그림이 찌그러지지 않도록 좌우에 검은 여백이 생깁니다.\n"
            + "창 크기 변화는 빌드한 게임에서만 확인할 수 있습니다.");
    }

    private void BuildTextPage()
    {
        var page = CreatePage(Category.Text);
        int row = 0;

        CreateOptionRow(page, row++, "글꼴", FontManager.FontOptionNames,
            () => Settings.fontIndex,
            v => SettingsManager.Instance.SetFontIndex(v));

        CreateSliderRow(page, row++, "글씨 크기", 0.7f, 1.6f,
            () => Settings.fontScale,
            v => { SettingsManager.Instance.SetFontScale(v); UpdateFontPreview(); },
            percent: false, suffix: "배");

        fontPreviewText = CreateNoteRow(page, row++, "");
        UpdateFontPreview();

        CreateOptionRow(page, row++, "텍스트 속도", TextSpeedNames,
            () => Settings.textSpeedLevel,
            v => SettingsManager.Instance.SetTextSpeedLevel(v));

        CreateToggleRow(page, row++, "자동 진행",
            () => Settings.autoAdvance,
            v => SettingsManager.Instance.SetAutoAdvance(v));

        CreateSliderRow(page, row++, "자동 진행 대기시간", 0.2f, 5f,
            () => Settings.autoAdvanceDelay,
            v => SettingsManager.Instance.SetAutoAdvanceDelay(v),
            percent: false, suffix: "초");
    }

    private static readonly string[] TextSpeedNames = { "느리게", "보통", "빠르게", "즉시" };

    // 현재 설정값 묶음에 짧게 접근하기 위한 도우미.
    private GameSettings Settings =>
        SettingsManager.Instance != null ? SettingsManager.Instance.Current : new GameSettings();

    private GameObject CreatePage(Category category)
    {
        var page = new GameObject($"Page_{category}", typeof(RectTransform));
        page.transform.SetParent(contentArea, false);
        Stretch(page.GetComponent<RectTransform>());
        page.SetActive(false);
        pages[category] = page;
        return page;
    }

    // ---------------------------------------------------------------------------------
    // 항목 한 줄 만들기
    // ---------------------------------------------------------------------------------
    // 모든 항목은 "왼쪽에 이름, 오른쪽에 조작 요소" 형태로 같은 자리에 놓인다.
    // 줄 번호(row)만 주면 위에서부터 차곡차곡 쌓이므로 겹치거나 잘릴 일이 없다.

    private RectTransform CreateRow(GameObject page, int row, out RectTransform controlArea)
    {
        var go = new GameObject($"Row_{row}", typeof(RectTransform));
        go.transform.SetParent(page.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, RowHeight);
        rt.anchoredPosition = new Vector2(0f, -row * (RowHeight + RowGap));

        var ctrl = new GameObject("Control", typeof(RectTransform));
        ctrl.transform.SetParent(go.transform, false);
        controlArea = ctrl.GetComponent<RectTransform>();
        controlArea.anchorMin = new Vector2(0f, 0f);
        controlArea.anchorMax = new Vector2(1f, 1f);
        controlArea.offsetMin = new Vector2(LabelWidth, 0f);
        controlArea.offsetMax = Vector2.zero;

        return rt;
    }

    private void CreateRowLabel(RectTransform row, string text)
    {
        var label = CreateLabel(row, "Label", text, 24, TextAlignmentOptions.Left);
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(LabelWidth - 20f, 0f);
        rt.anchoredPosition = Vector2.zero;
    }

    // 슬라이더 한 줄 (오른쪽 끝에 현재 값을 숫자로 보여준다)
    private void CreateSliderRow(GameObject page, int row, string label, float min, float max,
                                 System.Func<float> getter, System.Action<float> setter,
                                 bool percent, string suffix = "")
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        // 값 표시 (오른쪽 끝)
        var valueLabel = CreateLabel(ctrl, "Value", "", 22, TextAlignmentOptions.Right);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(1f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(1f, 0.5f);
        vRt.sizeDelta = new Vector2(100f, 0f);
        vRt.anchoredPosition = Vector2.zero;
        valueLabel.color = Accent;

        // 슬라이더 (값 표시 왼쪽까지)
        var slider = CreateSlider(ctrl, min, max);
        var sRt = slider.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.5f);
        sRt.anchorMax = new Vector2(1f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.offsetMin = new Vector2(0f, -14f);
        sRt.offsetMax = new Vector2(-112f, 14f);

        System.Action<float> updateLabel = v =>
            valueLabel.text = percent ? $"{Mathf.RoundToInt(v * 100f)}%" : $"{v:0.0}{suffix}";

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(v =>
        {
            if (SettingsManager.Instance == null) return;
            setter(v);
            updateLabel(v);
        });

        refreshActions.Add(() =>
        {
            float v = getter();
            slider.SetValueWithoutNotify(v);
            updateLabel(v);
        });
    }

    // 켜고 끄는 한 줄. 상태가 글씨로 바로 보이도록 체크박스 대신 버튼을 쓴다.
    private void CreateToggleRow(GameObject page, int row, string label,
                                 System.Func<bool> getter, System.Action<bool> setter)
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        var btn = CreateButton(ctrl, "Toggle", "",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(170f, 42f), null, subtle: true);

        var btnLabel = btn.GetComponentInChildren<TMP_Text>();
        var btnBg = btn.GetComponent<Image>();

        System.Action refresh = () =>
        {
            bool on = getter();
            btnLabel.text = on ? "켜짐" : "꺼짐";
            btnLabel.color = on ? new Color(0.1f, 0.08f, 0.03f) : TextDim;
            btnBg.color = on ? Accent : new Color(1f, 1f, 1f, 0.10f);
        };

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (SettingsManager.Instance == null) return;
            setter(!getter());
            refresh();
        });

        refreshActions.Add(refresh);
    }

    // 목록에서 고르는 한 줄. 좌우 화살표로 넘긴다.
    //
    // 드롭다운(TMP_Dropdown) 대신 화살표 방식을 쓰는 이유: 드롭다운은 펼침 목록을 코드로
    // 일일이 조립해야 하는데, 그 과정에서 목록이 화면 밖으로 삐져나가거나 잘리는 문제가
    // 잦았다. 항목이 서너 개뿐이라 화살표로 넘기는 편이 만들기도 간단하고 잘릴 일도 없다.
    private void CreateOptionRow(GameObject page, int row, string label, string[] options,
                                 System.Func<int> getter, System.Action<int> setter)
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        const float arrowW = 46f;
        const float valueW = 200f;

        var valueLabel = CreateLabel(ctrl, "Value", "", 24, TextAlignmentOptions.Center);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(0f, 0.5f);
        vRt.anchorMax = new Vector2(0f, 0.5f);
        vRt.pivot = new Vector2(0f, 0.5f);
        vRt.sizeDelta = new Vector2(valueW, 42f);
        vRt.anchoredPosition = new Vector2(arrowW + 8f, 0f);
        valueLabel.color = Accent;

        System.Action refresh = () =>
        {
            int i = Mathf.Clamp(getter(), 0, options.Length - 1);
            valueLabel.text = options[i];
        };

        CreateButton(ctrl, "Prev", "◀",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(arrowW, 42f), () =>
            {
                if (SettingsManager.Instance == null) return;
                setter((getter() - 1 + options.Length) % options.Length);
                refresh();
            }, subtle: true);

        CreateButton(ctrl, "Next", "▶",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(arrowW + 8f + valueW + 8f, 0f), new Vector2(arrowW, 42f), () =>
            {
                if (SettingsManager.Instance == null) return;
                setter((getter() + 1) % options.Length);
                refresh();
            }, subtle: true);

        refreshActions.Add(refresh);
    }

    // 설명 문구 (조작 요소 없음). 두 줄까지 들어가도록 높이를 넉넉히 잡는다.
    private TMP_Text CreateNoteRow(GameObject page, int row, string text)
    {
        var go = new GameObject($"Note_{row}", typeof(RectTransform));
        go.transform.SetParent(page.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, RowHeight + 12f);
        rt.anchoredPosition = new Vector2(0f, -row * (RowHeight + RowGap) - 4f);

        var note = go.AddComponent<TextMeshProUGUI>();
        note.text = text;
        note.fontSize = 19;
        note.alignment = TextAlignmentOptions.TopLeft;
        note.color = TextDim;
        note.raycastTarget = false;
        return note;
    }

    private void UpdateFontPreview()
    {
        if (fontPreviewText == null) return;
        float scale = Settings.fontScale;
        fontPreviewText.text = $"미리보기 ({scale:0.0}배) — 다시 만난 그 애는 여전히 정의의 용사를 말했다.";
    }

    private void RefreshAllControls()
    {
        foreach (var action in refreshActions) action?.Invoke();
        UpdateFontPreview();
    }

    // ---------------------------------------------------------------------------------
    // 메인 화면으로
    // ---------------------------------------------------------------------------------
    private void OnClickMainMenu()
    {
        // 저장하지 않은 진행 상황이 날아가므로 한 번 더 확인받는다.
        if (!mainMenuConfirming)
        {
            mainMenuConfirming = true;
            SetMainMenuLabel("정말 나가시겠습니까?");
            return;
        }

        if (SavePointManager.Instance != null) SavePointManager.Instance.ResetForNewGame();
        if (SaveManager.Instance != null) SaveManager.Instance.SetActiveSave(null);
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.ResetForNewGame();

        HidePanel();
        UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
    }

    private void SetMainMenuLabel(string text)
    {
        if (mainMenuButton == null) return;

        var label = mainMenuButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = text;
            // 확인 단계에서는 붉은빛으로 바꿔 경고임을 알린다.
            label.color = mainMenuConfirming ? new Color(1f, 0.55f, 0.45f) : TextMain;
        }
    }

    // ---------------------------------------------------------------------------------
    // 기본 부품 만들기
    // ---------------------------------------------------------------------------------
    private TMP_Text CreateLabel(Transform parent, string name, string text,
                                 float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = TextMain;
        tmp.raycastTarget = false;
        return tmp;
    }

    // subtle=true면 배경이 옅은 보조 버튼, false면 조금 더 눈에 띄는 기본 버튼.
    private Button CreateButton(Transform parent, string name, string label,
                                Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                Vector2 anchoredPosition, Vector2 size,
                                UnityEngine.Events.UnityAction onClick, bool subtle = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var bg = go.GetComponent<Image>();
        bg.color = subtle ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.22f);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 버튼이 눌리지 않는다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = TextMain;
        tmp.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.78f);
        colors.pressedColor = new Color(0.85f, 0.72f, 0.35f);
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    private Slider CreateSlider(Transform parent, float min, float max)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.direction = Slider.Direction.LeftToRight;

        // 홈(배경)
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(0f, 8f);
        bgRt.anchoredPosition = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

        // 채워지는 부분
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.5f);
        faRt.anchorMax = new Vector2(1f, 0.5f);
        faRt.pivot = new Vector2(0.5f, 0.5f);
        faRt.sizeDelta = new Vector2(-20f, 8f);
        faRt.anchoredPosition = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = Accent;

        // 손잡이
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 28f);
        handle.GetComponent<Image>().color = Color.white;

        slider.fillRect = fRt;
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();

        return slider;
    }

    // 가로 구분선. y는 상자 위쪽 기준 오프셋(음수).
    private void CreateDivider(Transform parent, float y)
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(ContentPadding, 0f);
        rt.offsetMax = new Vector2(-ContentPadding, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1.5f);
        rt.anchoredPosition = new Vector2(0f, y);

        var img = go.GetComponent<Image>();
        img.color = LineColor;
        img.raycastTarget = false;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
