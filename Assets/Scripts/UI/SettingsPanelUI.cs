using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 환경설정 화면 - 코드로 직접 만드는 하나짜리 설정 UI
// =====================================================================================
// ===== 왜 이렇게 다시 만들었나 =====
// 원래는 Settings.unity라는 별도 씬을 인게임 위에 "겹쳐 띄우는(additive)" 방식이었다.
// 그런데 이 방식에서 문제가 계속 나왔다:
//   - 뒤로가기를 눌러도 씬이 내려가기까지 몇 프레임이 걸려, 그 사이 옵션 창이
//     게임 화면 뒤에 깔린 것처럼 보였다.
//   - 두 씬의 캔버스가 서로 다른 기준으로 그려져 앞뒤 순서가 뒤엉켰다.
//   - 씬마다 EventSystem이 하나씩 있어 입력이 충돌했다.
//   - 씬에 이미 짜여 있던 UI에 새 항목을 끼워 넣으려다 보니 버튼이 잘리거나 겹쳤다.
//
// 그래서 씬을 오가는 방식을 버리고, 이 스크립트가 설정 화면 전체를 코드로 직접 만든다.
// 게임 화면 위에 패널 하나를 켜고 끄는 것뿐이라 씬 전환이 아예 없고, 위에서 말한 문제가
// 구조적으로 생길 수 없다. 배치도 전부 코드가 정하므로 잘리거나 겹칠 일이 없다.
//
// ===== 화면 구성 =====
//   [화면 전체를 덮는 어두운 막]
//     [가운데 상자]
//        제목: 환경설정
//        왼쪽 : 분류 버튼 (사운드 / 화면 / 텍스트)
//        오른쪽: 고른 분류의 설정 항목들
//        아래 : [메인 화면으로]            [닫기]
//
// ===== 쓰는 법 =====
// SettingsPanelUI.Instance.Open() / Close() 로 여닫는다.
// 씬에 미리 만들어둘 것은 없다. UIManager가 필요할 때 알아서 만든다.
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance;

    // 설정 분류.
    private enum Category { Sound, Display, Text }

    // ===== 화면 크기 기준값 =====
    // 캔버스가 1440x1080이라는 전제로 잡은 값이다. 전부 코드에서 정하므로 잘릴 일이 없다.
    private const float BoxWidth = 1040f;
    private const float BoxHeight = 780f;
    private const float SideBarWidth = 240f;   // 왼쪽 분류 버튼 영역
    private const float HeaderHeight = 80f;    // 위쪽 제목 영역
    private const float FooterHeight = 84f;    // 아래쪽 버튼 영역
    private const float RowHeight = 52f;       // 설정 항목 한 줄 높이
    private const float RowGap = 14f;          // 항목 사이 간격
    private const float LabelWidth = 260f;     // 항목 이름 칸 너비

    private GameObject panel;
    private RectTransform contentArea;         // 오른쪽 설정 항목이 들어가는 자리
    private readonly Dictionary<Category, GameObject> pages = new Dictionary<Category, GameObject>();
    private readonly Dictionary<Category, Button> categoryButtons = new Dictionary<Category, Button>();

    private TMP_Text fontPreviewText;
    private Button mainMenuButton;
    private bool mainMenuConfirming;

    // 다른 스크립트가 "지금 설정 창이 열려 있나?"를 확인할 때 쓴다.
    public bool IsOpen => panel != null && panel.activeSelf;

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

        // 열 때마다 현재 설정값을 UI에 다시 반영한다.
        RefreshAllControls();

        mainMenuConfirming = false;
        SetMainMenuLabel("메인 화면으로");

        ShowCategory(Category.Sound);

        panel.SetActive(true);
        // 다른 UI보다 항상 위에 오도록 계층 맨 끝으로.
        panel.transform.SetAsLastSibling();
    }

    // 닫기 버튼을 눌렀을 때 대신 실행할 동작.
    // 타이틀에서 들어온 독립 설정 화면(Settings.unity)처럼 "닫으면 이전 화면으로 돌아가야"
    // 하는 경우에 여기에 그 동작을 넣어둔다. 비어 있으면 그냥 패널만 닫는다.
    public System.Action onClose;

    public void Close()
    {
        if (onClose != null)
        {
            onClose.Invoke();
            return;
        }

        if (panel != null) panel.SetActive(false);
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
        box.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.97f);

        // ----- 제목 -----
        var title = CreateLabel(box.transform, "Title", "환경설정", 36, TextAlignmentOptions.Left);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(32f, 0f);
        titleRt.offsetMax = new Vector2(-32f, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, HeaderHeight);
        titleRt.anchoredPosition = new Vector2(0f, 0f);
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 0.86f, 0.45f);

        // 제목 아래 구분선
        CreateDivider(box.transform, -HeaderHeight);

        // ----- 왼쪽: 분류 버튼 -----
        var sidebar = new GameObject("Sidebar", typeof(RectTransform));
        sidebar.transform.SetParent(box.transform, false);
        var sideRt = sidebar.GetComponent<RectTransform>();
        sideRt.anchorMin = new Vector2(0f, 0f);
        sideRt.anchorMax = new Vector2(0f, 1f);
        sideRt.pivot = new Vector2(0f, 1f);
        sideRt.offsetMin = new Vector2(24f, FooterHeight);
        sideRt.offsetMax = new Vector2(24f + SideBarWidth, -(HeaderHeight + 12f));

        CreateCategoryButton(sidebar.transform, Category.Sound, "사운드", 0);
        CreateCategoryButton(sidebar.transform, Category.Display, "화면", 1);
        CreateCategoryButton(sidebar.transform, Category.Text, "텍스트", 2);

        // ----- 오른쪽: 설정 항목 영역 -----
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(box.transform, false);
        contentArea = content.GetComponent<RectTransform>();
        contentArea.anchorMin = new Vector2(0f, 0f);
        contentArea.anchorMax = new Vector2(1f, 1f);
        contentArea.offsetMin = new Vector2(24f + SideBarWidth + 24f, FooterHeight);
        contentArea.offsetMax = new Vector2(-32f, -(HeaderHeight + 12f));

        BuildSoundPage();
        BuildDisplayPage();
        BuildTextPage();

        // ----- 아래: 공용 버튼 -----
        CreateDivider(box.transform, -(BoxHeight - FooterHeight));

        mainMenuButton = CreateButton(box.transform, "Btn_MainMenu", "메인 화면으로",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(32f, 18f), new Vector2(240f, 52f), OnClickMainMenu);

        CreateButton(box.transform, "Btn_Close", "닫기",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-32f, 18f), new Vector2(180f, 52f), Close);
    }

    // 왼쪽 분류 버튼 하나.
    private void CreateCategoryButton(Transform parent, Category category, string label, int index)
    {
        var btn = CreateButton(parent, $"Cat_{category}", label,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -index * (RowHeight + 10f)), new Vector2(0f, RowHeight),
            () => ShowCategory(category));

        // 가로는 부모 너비에 맞춘다.
        var rt = btn.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        rt.sizeDelta = new Vector2(0f, RowHeight);

        categoryButtons[category] = btn;
    }

    // 고른 분류만 보여주고, 그 분류 버튼을 강조한다.
    private void ShowCategory(Category category)
    {
        foreach (var pair in pages)
        {
            if (pair.Value != null) pair.Value.SetActive(pair.Key == category);
        }

        // 지금 보고 있는 분류가 어느 것인지 색으로 알 수 있게 한다.
        foreach (var pair in categoryButtons)
        {
            var img = pair.Value != null ? pair.Value.GetComponent<Image>() : null;
            if (img == null) continue;

            img.color = pair.Key == category
                ? new Color(1f, 0.82f, 0.35f, 0.45f)   // 선택됨
                : new Color(1f, 1f, 1f, 0.14f);        // 선택 안 됨
        }
    }

    // ---------------------------------------------------------------------------------
    // 분류별 설정 항목
    // ---------------------------------------------------------------------------------
    private void BuildSoundPage()
    {
        var page = CreatePage(Category.Sound);
        int row = 0;

        CreateSliderRow(page, row++, "전체 소리", 0f, 1f,
            () => Settings.masterVolume,
            v => SettingsManager.Instance.SetMasterVolume(v),
            percent: true);

        CreateSliderRow(page, row++, "배경음악", 0f, 1f,
            () => Settings.bgmVolume,
            v => SettingsManager.Instance.SetBgmVolume(v),
            percent: true);

        CreateSliderRow(page, row++, "효과음", 0f, 1f,
            () => Settings.sfxVolume,
            v => SettingsManager.Instance.SetSfxVolume(v),
            percent: true);
    }

    private void BuildDisplayPage()
    {
        var page = CreatePage(Category.Display);
        int row = 0;

        CreateToggleRow(page, row++, "창 모드",
            () => Settings.windowed,
            v => SettingsManager.Instance.SetWindowed(v));

        CreateNoteRow(page, row++,
            "전체화면에서는 그림이 찌그러지지 않도록 좌우에 검은 여백이 생깁니다.");
    }

    private void BuildTextPage()
    {
        var page = CreatePage(Category.Text);
        int row = 0;

        CreateDropdownRow(page, row++, "글꼴", FontManager.FontOptionNames,
            () => Settings.fontIndex,
            v => SettingsManager.Instance.SetFontIndex(v));

        CreateSliderRow(page, row++, "글씨 크기", 0.7f, 1.6f,
            () => Settings.fontScale,
            v => { SettingsManager.Instance.SetFontScale(v); UpdateFontPreview(); },
            percent: false);

        fontPreviewText = CreateNoteRow(page, row++, "");
        UpdateFontPreview();

        CreateDropdownRow(page, row++, "텍스트 속도", TextSpeedNames,
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
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(0f, RowHeight);
        rt.anchoredPosition = new Vector2(0f, -row * (RowHeight + RowGap));

        // 오른쪽 조작 영역 (이름 칸 다음부터 끝까지)
        var ctrl = new GameObject("Control", typeof(RectTransform));
        ctrl.transform.SetParent(go.transform, false);
        controlArea = ctrl.GetComponent<RectTransform>();
        controlArea.anchorMin = new Vector2(0f, 0f);
        controlArea.anchorMax = new Vector2(1f, 1f);
        controlArea.offsetMin = new Vector2(LabelWidth, 0f);
        controlArea.offsetMax = new Vector2(0f, 0f);

        return rt;
    }

    private void CreateRowLabel(RectTransform row, string text)
    {
        var label = CreateLabel(row, "Label", text, 24, TextAlignmentOptions.Left);
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(LabelWidth - 16f, 0f);
        rt.anchoredPosition = Vector2.zero;
    }

    // 슬라이더 한 줄 (오른쪽 끝에 현재 값을 숫자로 보여준다)
    private void CreateSliderRow(GameObject page, int row, string label, float min, float max,
                                 System.Func<float> getter, System.Action<float> setter,
                                 bool percent, string suffix = "")
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        // 값 표시 (오른쪽 끝 90px)
        var valueLabel = CreateLabel(ctrl, "Value", "", 22, TextAlignmentOptions.Right);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(1f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(1f, 0.5f);
        vRt.sizeDelta = new Vector2(96f, 0f);
        vRt.anchoredPosition = Vector2.zero;
        valueLabel.color = new Color(1f, 0.86f, 0.45f);

        // 슬라이더 (값 표시 왼쪽까지)
        var slider = CreateSlider(ctrl, min, max);
        var sRt = slider.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.5f);
        sRt.anchorMax = new Vector2(1f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.offsetMin = new Vector2(0f, -12f);
        sRt.offsetMax = new Vector2(-108f, 12f);

        // 값 갱신 함수
        System.Action refresh = () =>
        {
            float v = getter();
            slider.SetValueWithoutNotify(v);
            valueLabel.text = percent ? $"{Mathf.RoundToInt(v * 100f)}%" : $"{v:0.0}{suffix}";
        };

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(v =>
        {
            if (SettingsManager.Instance == null) return;
            setter(v);
            valueLabel.text = percent ? $"{Mathf.RoundToInt(v * 100f)}%" : $"{v:0.0}{suffix}";
        });

        refreshActions.Add(refresh);
    }

    // 켜고 끄는 한 줄. 줄 전체가 클릭 영역이라 어디를 눌러도 바뀐다.
    private void CreateToggleRow(GameObject page, int row, string label,
                                 System.Func<bool> getter, System.Action<bool> setter)
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        // 켜기/끄기 버튼 (토글 대신 버튼으로 만들면 상태가 글씨로 바로 보여 더 직관적이다)
        var btn = CreateButton(ctrl, "Toggle", "",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(160f, 40f), null);

        var btnLabel = btn.GetComponentInChildren<TMP_Text>();

        System.Action refresh = () =>
        {
            bool on = getter();
            btnLabel.text = on ? "켜짐" : "꺼짐";
            var img = btn.GetComponent<Image>();
            img.color = on ? new Color(1f, 0.82f, 0.35f, 0.5f) : new Color(1f, 1f, 1f, 0.14f);
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
    // 드롭다운(TMP_Dropdown) 대신 화살표 방식을 쓴 이유: 드롭다운은 펼침 목록을 만들려면
    // 템플릿 오브젝트를 코드로 일일이 조립해야 하는데, 그 과정에서 목록이 화면 밖으로
    // 삐져나가거나 잘리는 문제가 잦았다. 항목이 서너 개뿐이라 화살표로 넘기는 편이
    // 만들기도 간단하고 잘릴 일도 없다.
    private void CreateDropdownRow(GameObject page, int row, string label, string[] options,
                                   System.Func<int> getter, System.Action<int> setter)
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);
        CreateRowLabel(rowRt, label);

        var valueLabel = CreateLabel(ctrl, "Value", "", 24, TextAlignmentOptions.Center);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(0f, 0.5f);
        vRt.anchorMax = new Vector2(0f, 0.5f);
        vRt.pivot = new Vector2(0f, 0.5f);
        vRt.sizeDelta = new Vector2(220f, 40f);
        vRt.anchoredPosition = new Vector2(56f, 0f);
        valueLabel.color = new Color(1f, 0.86f, 0.45f);

        System.Action refresh = () =>
        {
            int i = Mathf.Clamp(getter(), 0, options.Length - 1);
            valueLabel.text = options[i];
        };

        // 왼쪽 화살표
        CreateButton(ctrl, "Prev", "◀",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(48f, 40f), () =>
            {
                if (SettingsManager.Instance == null) return;
                int i = (getter() - 1 + options.Length) % options.Length;
                setter(i);
                refresh();
            });

        // 오른쪽 화살표
        CreateButton(ctrl, "Next", "▶",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(284f, 0f), new Vector2(48f, 40f), () =>
            {
                if (SettingsManager.Instance == null) return;
                int i = (getter() + 1) % options.Length;
                setter(i);
                refresh();
            });

        refreshActions.Add(refresh);
    }

    // 설명 문구 한 줄 (조작 요소 없음)
    private TMP_Text CreateNoteRow(GameObject page, int row, string text)
    {
        var rowRt = CreateRow(page, row, out RectTransform ctrl);

        var note = CreateLabel(rowRt, "Note", text, 19, TextAlignmentOptions.Left);
        var rt = note.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        note.color = new Color(0.75f, 0.75f, 0.72f);
        return note;
    }

    private void UpdateFontPreview()
    {
        if (fontPreviewText == null) return;
        float scale = Settings.fontScale;
        fontPreviewText.text = $"({scale:0.0}배) 다시 만난 그 애는 여전히 정의의 용사를 말했다.";
    }

    // 열 때마다 모든 항목을 현재 설정값으로 다시 맞춘다.
    private readonly List<System.Action> refreshActions = new List<System.Action>();

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

    // 타이틀에서 들어온 독립 설정 화면에서는 이미 타이틀에 있는 셈이므로
    // "메인 화면으로" 버튼이 필요 없다. 그럴 때 숨긴다.
    public void SetMainMenuButtonVisible(bool visible)
    {
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(visible);
    }

    private void SetMainMenuLabel(string text)
    {
        if (mainMenuButton == null) return;
        var label = mainMenuButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
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
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label,
                                Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                Vector2 anchoredPosition, Vector2 size,
                                UnityEngine.Events.UnityAction onClick)
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
        bg.color = new Color(1f, 1f, 1f, 0.14f);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 버튼이 눌리지 않는다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
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
        bgRt.sizeDelta = new Vector2(0f, 10f);
        bgRt.anchoredPosition = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);

        // 채워지는 부분
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.5f);
        faRt.anchorMax = new Vector2(1f, 0.5f);
        faRt.pivot = new Vector2(0.5f, 0.5f);
        faRt.sizeDelta = new Vector2(-20f, 10f);
        faRt.anchoredPosition = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(1f, 0.82f, 0.35f, 0.9f);

        // 손잡이
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = new Vector2(0f, 0f);
        haRt.anchorMax = new Vector2(1f, 1f);
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 30f);
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
        rt.offsetMin = new Vector2(24f, 0f);
        rt.offsetMax = new Vector2(-24f, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 2f);
        rt.anchoredPosition = new Vector2(0f, y);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.20f);
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
