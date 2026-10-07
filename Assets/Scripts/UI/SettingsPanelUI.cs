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
// ===== 화면 구성 (Figma "Screen / Settings" - 청회색 테마) =====
//   [화면 전체를 덮는 어두운 막 + 패널 그림(SettingsPanel.png)]
//     [가운데 패널 960x800]
//        ── SETTINGS / 환경설정
//        ── 탭:  사운드   화면   텍스트          (고른 탭은 밝은 굵은 글자 + 밑줄)
//        ── 항목: 왼쪽에 이름, 오른쪽 끝에 조작 칸 (슬라이더 / 꺼짐·켜짐 / ◀ 값 ▶)
//        ── 아래: [메인 화면으로] [종료하기]                  [돌아가기]
//
// 그림은 Assets/Resources/Illusts/UI/Settings/ 에 있다 (git 제외 - 드라이브로 공유).
// 그림이 없으면 같은 자리에 색 도형으로 대신 그린다.
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
    // 캔버스가 1440x1080이라는 전제로 잡은 값이다. 패널 그림(SettingsPanel.png)이
    // 1440x1080 한 장이고, 그 안의 패널이 가운데 960x800 자리에 그려져 있다.
    private const float BoxWidth = 960f;
    private const float BoxHeight = 800f;
    private const float Pad = 64f;              // 패널 좌우 여백
    private const float TabTop = 140f;          // 탭 줄 위쪽 (패널 위에서부터)
    private const float ContentTop = 212f;      // 항목 영역 위쪽 (탭 아래 구분선 + 여백)
    private const float FooterButtonBottom = 48f;
    private const float ContentBottom = 134f;   // 버튼 위 구분선 (패널 아래에서부터)
    private const float RowHeight = 70f;        // 항목 한 줄 높이 (아래 옅은 구분선 포함)
    private const float NoteHeight = 52f;       // 안내 문구 줄 높이

    // 조작 칸 크기 (그림 크기와 같다)
    private const float SliderTrackWidth = 300f;
    private const float SliderValueWidth = 80f;
    private const float ToggleSegWidth = 100f;
    private const float ToggleSegGap = 10f;
    private const float ControlHeight = 44f;
    private const float OptionValueWidth = 212f;
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 54f;

    // ===== 색 =====
    private static readonly Color Accent = new Color(0.561f, 0.643f, 0.718f);       // 8FA4B7 청회색 강조
    private static readonly Color TitleColor = new Color(0.894f, 0.918f, 0.937f);   // E4EAEF 제목
    private static readonly Color KickerColor = new Color(0.435f, 0.518f, 0.588f);  // 6F8496 SETTINGS
    private static readonly Color LabelColor = new Color(0.788f, 0.835f, 0.878f);   // C9D5E0 항목 이름/값
    private static readonly Color ActiveColor = new Color(0.851f, 0.89f, 0.922f);   // D9E3EB 고른 것
    private static readonly Color DimColor = new Color(0.424f, 0.486f, 0.545f);     // 6C7C8B 안 고른 것
    private static readonly Color HintColor = new Color(0.482f, 0.541f, 0.596f);    // 7B8A98 안내 문구
    private static readonly Color ButtonTextColor = new Color(0.663f, 0.729f, 0.788f);   // A9BAC9
    private static readonly Color PrimaryTextColor = new Color(0.059f, 0.082f, 0.106f);  // 0F151B
    private static readonly Color RowLineColor = new Color(0.165f, 0.208f, 0.251f, 0.6f);  // 2A3540
    // 그림이 없을 때만 쓰는 색
    private static readonly Color PanelColor = new Color(0.063f, 0.086f, 0.114f, 0.95f);   // 10161D
    private static readonly Color TrackColor = new Color(0.204f, 0.255f, 0.302f);          // 34414D
    private static readonly Color OnFillColor = new Color(0.141f, 0.192f, 0.251f);         // 243140

    // 그림 경로 (Resources 기준)
    private const string ArtFolder = "Illusts/UI/Settings/";

    private GameObject panel;
    private RectTransform contentArea;
    private readonly Dictionary<Category, GameObject> pages = new Dictionary<Category, GameObject>();
    private readonly Dictionary<Category, TMP_Text> tabLabels = new Dictionary<Category, TMP_Text>();
    private readonly Dictionary<Category, Image> tabUnderlines = new Dictionary<Category, Image>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private TMP_Text fontPreviewText;
    private Button mainMenuButton;
    private GameObject confirmDialog;   // "정말 나가시겠습니까?" 확인 창 (화면 가운데)

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

    // ===== Esc =====
    //   확인 창이 떠 있으면 -> 확인 창만 닫는다 ([취소]와 같음)
    //   아니면              -> 설정 화면을 닫는다 ([돌아가기]와 같음)
    // UIManager도 Esc로 퀵바 창들을 닫으므로, 같은 프레임에 두 번 처리되지 않게 이번 프레임에
    // Esc를 썼다는 표시(HandledEscapeThisFrame)를 남긴다.
    private int escapeHandledFrame = -1;
    public bool HandledEscapeThisFrame => escapeHandledFrame == Time.frameCount;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || !IsOpen) return;
        escapeHandledFrame = Time.frameCount;

        if (confirmDialog != null && confirmDialog.activeInHierarchy) confirmDialog.SetActive(false);
        else Close();
    }

    // ---------------------------------------------------------------------------------
    // 여닫기
    // ---------------------------------------------------------------------------------
    public void Open()
    {
        if (panel == null) return;

        RefreshAllControls();

        if (confirmDialog != null) confirmDialog.SetActive(false);

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
        // 설정 화면 뒤의 게임 화면(흰 선화)이 비쳐 보이면 글자가 읽기 어려워서 거의 가린다.
        dim.color = new Color(0f, 0f, 0f, 0.97f);   // 검은색
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 패널 그림 (1440x1080 한 장: 패널 + 그림자 + 위쪽 강조선 + 구분선) -----
        var panelArt = LoadSprite("SettingsPanel");
        if (panelArt != null)
        {
            var art = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(panel.transform, false);
            var artRt = art.GetComponent<RectTransform>();
            artRt.anchorMin = artRt.anchorMax = artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(1440f, 1080f);
            artRt.anchoredPosition = Vector2.zero;
            var artImg = art.GetComponent<Image>();
            artImg.sprite = panelArt;
            artImg.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning("[SettingsPanelUI] 설정 화면 그림을 찾을 수 없어 색 도형으로 그립니다: Assets/Resources/" + ArtFolder + "SettingsPanel.png");
        }

        // ----- 가운데 패널 (글자/조작 칸을 얹는 자리) -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        // 그림 속 패널은 화면 가운데보다 20px 아래(y140~940)에 있다.
        boxRt.anchoredPosition = new Vector2(0f, -(140f + BoxHeight / 2f - 540f));
        var boxImg = box.GetComponent<Image>();
        boxImg.color = panelArt != null ? new Color(0f, 0f, 0f, 0f) : PanelColor;
        boxImg.raycastTarget = false;

        if (panelArt == null)
        {
            // 그림에 들어 있는 선들을 대신 그린다.
            AddRect(box.transform, "TopAccent", Pad, 0f, 120f, 3f, Accent);
            AddRect(box.transform, "TabDivider", Pad, 188f, BoxWidth - Pad * 2f, 1f, RowLineColor);
            AddRect(box.transform, "FooterDivider", Pad, BoxHeight - ContentBottom, BoxWidth - Pad * 2f, 1f, RowLineColor);
        }

        // ----- 머리말 -----
        var kicker = CreateLabel(box.transform, "Kicker", "SETTINGS", 14, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(kicker.rectTransform, Pad, 44f, 300f, 20f);
        kicker.fontStyle = FontStyles.Bold;
        kicker.characterSpacing = 36f;
        kicker.color = KickerColor;

        var title = CreateLabel(box.transform, "Title", "환경설정", 36, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(title.rectTransform, Pad, 62f, 500f, 54f);
        title.fontStyle = FontStyles.Bold;
        title.color = TitleColor;

        // ----- 위쪽 탭 -----
        BuildTabBar(box.transform);

        // ----- 가운데: 조절 항목 영역 -----
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(box.transform, false);
        contentArea = content.GetComponent<RectTransform>();
        contentArea.anchorMin = new Vector2(0f, 0f);
        contentArea.anchorMax = new Vector2(1f, 1f);
        contentArea.offsetMin = new Vector2(Pad, ContentBottom + 12f);
        contentArea.offsetMax = new Vector2(-Pad, -ContentTop);

        BuildSoundPage();
        BuildDisplayPage();
        BuildTextPage();

        // ----- 아래쪽 버튼 -----
        BuildFooter(box.transform);

        // ----- "메인 화면으로" 확인 창 (맨 위에 겹쳐 뜬다) -----
        BuildConfirmDialog(panel.transform);

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨진다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    // ===== 위쪽 탭 =====
    // 글자 폭만큼의 탭 세 개를 왼쪽부터 40px 간격으로 놓는다.
    // 고른 탭은 밝은 굵은 글자 + 글자 폭만큼의 밑줄.
    private void BuildTabBar(Transform box)
    {
        float x = Pad;
        x = CreateTabButton(box, Category.Sound, "사운드", x) + 40f;
        x = CreateTabButton(box, Category.Display, "화면", x) + 40f;
        CreateTabButton(box, Category.Text, "텍스트", x);
    }

    // 탭 버튼 하나를 x에 놓고, 그 오른쪽 끝 x를 돌려준다.
    private float CreateTabButton(Transform parent, Category category, string label, float x)
    {
        var go = new GameObject($"Tab_{category}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var text = CreateLabel(go.transform, "Text", label, 22, TextAlignmentOptions.TopLeft);
        text.fontStyle = FontStyles.Bold;   // 폭을 굵은 글자 기준으로 잡아 고를 때 밀리지 않게
        UIFontHelper.Apply(text);
        float width = Mathf.Ceil(text.GetPreferredValues(label).x) + 2f;
        Stretch(text.rectTransform);

        PlaceTopLeft(go.GetComponent<RectTransform>(), x, TabTop, width, 48f);

        var bg = go.GetComponent<Image>();
        // 클릭만 받는 완전 투명. (Image는 알파 0이어도 클릭을 받는다)
        // 예전처럼 흰색 1%를 깔면 Linear 색 공간에서는 어두운 배경 위에 회색 네모로 보인다.
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;

        // 선택 표시용 밑줄 (선택된 탭만 켜진다)
        var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(go.transform, false);
        var uRt = underline.GetComponent<RectTransform>();
        uRt.anchorMin = new Vector2(0f, 0f);
        uRt.anchorMax = new Vector2(1f, 0f);
        uRt.pivot = new Vector2(0.5f, 0f);
        uRt.offsetMin = new Vector2(0f, 2f);
        uRt.offsetMax = new Vector2(0f, 4f);
        var uImg = underline.GetComponent<Image>();
        SetImage(uImg, "Tab_Underline", Accent);
        uImg.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => ShowCategory(category));

        tabLabels[category] = text;
        tabUnderlines[category] = uImg;
        return x + width;
    }

    // 고른 탭의 내용만 보여주고, 탭 모양도 그에 맞게 바꾼다.
    private void ShowCategory(Category category)
    {
        foreach (var pair in pages)
        {
            if (pair.Value != null) pair.Value.SetActive(pair.Key == category);
        }

        foreach (var pair in tabLabels)
        {
            bool selected = pair.Key == category;
            if (pair.Value != null)
            {
                pair.Value.color = selected ? ActiveColor : DimColor;
                pair.Value.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
            if (tabUnderlines.TryGetValue(pair.Key, out var underline) && underline != null)
            {
                underline.enabled = selected;
            }
        }
    }

    // ===== 아래쪽 버튼 =====
    //   왼쪽 : 메인 화면으로 (게임 도중에만 의미가 있다) / 종료하기
    //   오른쪽: 돌아가기 (주 버튼)
    private void BuildFooter(Transform box)
    {
        mainMenuButton = CreateFooterButton(box, "Btn_MainMenu", "메인 화면으로", Pad, primary: false, OnClickMainMenu);
        CreateFooterButton(box, "Btn_Quit", "종료하기", Pad + ButtonWidth + 16f, primary: false, OnClickQuit);
        CreateFooterButton(box, "Btn_Back", "돌아가기", BoxWidth - Pad - ButtonWidth, primary: true, Close);
    }

    private Button CreateFooterButton(Transform box, string name, string label, float x, bool primary,
                                      UnityEngine.Events.UnityAction onClick)
    {
        var btn = CreateButton(box, name, label, primary ? "Button_Primary" : "Button_Secondary",
                               primary ? Accent : new Color(0f, 0f, 0f, 0f), onClick);
        PlaceTopLeft(btn.GetComponent<RectTransform>(), x, BoxHeight - FooterButtonBottom - ButtonHeight, ButtonWidth, ButtonHeight);
        var text = btn.GetComponentInChildren<TMP_Text>();
        text.fontSize = 19;
        text.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
        text.color = primary ? PrimaryTextColor : ButtonTextColor;
        return btn;
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
        float y = 0f;

        y = CreateSliderRow(page, y, "전체 소리", 0f, 1f,
            () => Settings.masterVolume,
            v => SettingsManager.Instance.SetMasterVolume(v), percent: true);

        y = CreateSliderRow(page, y, "배경음악", 0f, 1f,
            () => Settings.bgmVolume,
            v => SettingsManager.Instance.SetBgmVolume(v), percent: true);

        y = CreateSliderRow(page, y, "효과음", 0f, 1f,
            () => Settings.sfxVolume,
            v => SettingsManager.Instance.SetSfxVolume(v), percent: true);

        CreateNoteRow(page, y, "전체 소리는 배경음악과 효과음 모두에 함께 적용됩니다.");
    }

    private void BuildDisplayPage()
    {
        var page = CreatePage(Category.Display);
        float y = 0f;

        y = CreateToggleRow(page, y, "창 모드",
            () => Settings.windowed,
            v => SettingsManager.Instance.SetWindowed(v));

        CreateNoteRow(page, y,
            "전체화면에서는 그림이 찌그러지지 않도록 좌우에 검은 여백이 생깁니다.\n"
            + "창 크기 변화는 빌드한 게임에서만 확인할 수 있습니다.");
    }

    private void BuildTextPage()
    {
        var page = CreatePage(Category.Text);
        float y = 0f;

        y = CreateOptionRow(page, y, "글꼴", FontManager.FontOptionNames,
            () => Settings.fontIndex,
            v => SettingsManager.Instance.SetFontIndex(v));

        y = CreateSliderRow(page, y, "글씨 크기", 0.7f, 1.6f,
            () => Settings.fontScale,
            v => { SettingsManager.Instance.SetFontScale(v); UpdateFontPreview(); },
            percent: false, suffix: "배");

        fontPreviewText = CreateNoteRow(page, y, "");
        y += NoteHeight;
        UpdateFontPreview();

        y = CreateOptionRow(page, y, "텍스트 속도", TextSpeedNames,
            () => Settings.textSpeedLevel,
            v => SettingsManager.Instance.SetTextSpeedLevel(v));

        y = CreateToggleRow(page, y, "자동 진행",
            () => Settings.autoAdvance,
            v => SettingsManager.Instance.SetAutoAdvance(v));

        CreateSliderRow(page, y, "자동 진행 대기시간", 0.2f, 5f,
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
    // 모든 항목은 "왼쪽에 이름, 오른쪽 끝에 조작 칸" 형태로 놓이고, 아래에 옅은 선이 그어진다.
    // 줄의 위쪽 y(항목 영역 위에서부터)를 받아서, 다음 줄이 시작할 y를 돌려준다.
    // 그래서 위에서부터 차곡차곡 쌓이고 겹치거나 잘릴 일이 없다.

    // controlWidth: 오른쪽 끝에 붙는 조작 칸의 폭.
    private RectTransform CreateRow(GameObject page, float y, string label, float controlWidth)
    {
        var go = new GameObject($"Row_{label}", typeof(RectTransform));
        go.transform.SetParent(page.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, RowHeight);
        rt.anchoredPosition = new Vector2(0f, -y);

        var text = CreateLabel(go.transform, "Label", label, 22, TextAlignmentOptions.Left);
        var lRt = text.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = new Vector2(-(controlWidth + 20f), 0f);
        text.color = LabelColor;

        // 줄 아래 옅은 구분선
        var line = new GameObject("RowLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(go.transform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 0f);
        lineRt.pivot = new Vector2(0.5f, 0f);
        lineRt.offsetMin = Vector2.zero;
        lineRt.offsetMax = new Vector2(0f, 1f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = RowLineColor;
        lineImg.raycastTarget = false;

        // 조작 칸 (오른쪽 끝, 세로 가운데)
        var ctrl = new GameObject("Control", typeof(RectTransform));
        ctrl.transform.SetParent(go.transform, false);
        var cRt = ctrl.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot = new Vector2(1f, 0.5f);
        cRt.sizeDelta = new Vector2(controlWidth, ControlHeight);
        cRt.anchoredPosition = Vector2.zero;
        return cRt;
    }

    // 슬라이더 한 줄: [────●─────] 100%
    private float CreateSliderRow(GameObject page, float y, string label, float min, float max,
                                  System.Func<float> getter, System.Action<float> setter,
                                  bool percent, string suffix = "")
    {
        var ctrl = CreateRow(page, y, label, SliderTrackWidth + 20f + SliderValueWidth);

        var valueLabel = CreateLabel(ctrl, "Value", "", 18, TextAlignmentOptions.Right);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(1f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(1f, 0.5f);
        vRt.sizeDelta = new Vector2(SliderValueWidth, 0f);
        vRt.anchoredPosition = Vector2.zero;
        valueLabel.color = LabelColor;

        var slider = CreateSlider(ctrl, min, max);
        var sRt = slider.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(0f, 1f);
        sRt.pivot = new Vector2(0f, 0.5f);
        sRt.sizeDelta = new Vector2(SliderTrackWidth, 0f);
        sRt.anchoredPosition = Vector2.zero;

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
        return y + RowHeight;
    }

    // 켜고 끄는 한 줄: [ 꺼짐 ][ 켜짐 ] 두 칸 중 지금 상태 쪽이 강조된다.
    private float CreateToggleRow(GameObject page, float y, string label,
                                  System.Func<bool> getter, System.Action<bool> setter)
    {
        var ctrl = CreateRow(page, y, label, ToggleSegWidth * 2f + ToggleSegGap);

        var off = CreateSegment(ctrl, "Off", "꺼짐", 0f, () => setter(false));
        var on = CreateSegment(ctrl, "On", "켜짐", ToggleSegWidth + ToggleSegGap, () => setter(true));

        System.Action refresh = () =>
        {
            bool value = getter();
            StyleSegment(off, !value);
            StyleSegment(on, value);
        };

        off.onClick.AddListener(() => refresh());
        on.onClick.AddListener(() => refresh());
        refreshActions.Add(refresh);
        return y + RowHeight;
    }

    private Button CreateSegment(RectTransform parent, string name, string label, float x, System.Action apply)
    {
        var btn = CreateButton(parent, name, label, "Toggle_Off", new Color(0f, 0f, 0f, 0f), () =>
        {
            if (SettingsManager.Instance == null) return;
            apply();
        });
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(ToggleSegWidth, 0f);
        rt.anchoredPosition = new Vector2(x, 0f);
        btn.GetComponentInChildren<TMP_Text>().fontSize = 18;
        return btn;
    }

    // 고른 칸: 채워진 칸 + 밝은 굵은 글자 / 안 고른 칸: 테두리만 + 흐린 글자
    private void StyleSegment(Button seg, bool selected)
    {
        var img = seg.GetComponent<Image>();
        SetImage(img, selected ? "Toggle_On" : "Toggle_Off", selected ? OnFillColor : new Color(0f, 0f, 0f, 0f));
        var text = seg.GetComponentInChildren<TMP_Text>();
        text.color = selected ? ActiveColor : DimColor;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
    }

    // 목록에서 고르는 한 줄: [◀]   보통   [▶]
    //
    // 드롭다운(TMP_Dropdown) 대신 화살표 방식을 쓰는 이유: 드롭다운은 펼침 목록을 코드로
    // 일일이 조립해야 하는데, 그 과정에서 목록이 화면 밖으로 삐져나가거나 잘리는 문제가
    // 잦았다. 항목이 서너 개뿐이라 화살표로 넘기는 편이 만들기도 간단하고 잘릴 일도 없다.
    private float CreateOptionRow(GameObject page, float y, string label, string[] options,
                                  System.Func<int> getter, System.Action<int> setter)
    {
        float arrowW = ControlHeight;
        var ctrl = CreateRow(page, y, label, arrowW * 2f + OptionValueWidth);

        var valueLabel = CreateLabel(ctrl, "Value", "", 20, TextAlignmentOptions.Center);
        var vRt = valueLabel.rectTransform;
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(0f, 1f);
        vRt.pivot = new Vector2(0f, 0.5f);
        vRt.sizeDelta = new Vector2(OptionValueWidth, 0f);
        vRt.anchoredPosition = new Vector2(arrowW, 0f);
        valueLabel.fontStyle = FontStyles.Bold;
        valueLabel.color = ActiveColor;

        System.Action refresh = () =>
        {
            int i = Mathf.Clamp(getter(), 0, options.Length - 1);
            valueLabel.text = options[i];
        };

        CreateArrowButton(ctrl, "Prev", "◀", 0f, () =>
        {
            if (SettingsManager.Instance == null) return;
            setter((getter() - 1 + options.Length) % options.Length);
            refresh();
        });
        CreateArrowButton(ctrl, "Next", "▶", arrowW + OptionValueWidth, () =>
        {
            if (SettingsManager.Instance == null) return;
            setter((getter() + 1) % options.Length);
            refresh();
        });

        refreshActions.Add(refresh);
        return y + RowHeight;
    }

    private void CreateArrowButton(RectTransform parent, string name, string glyph, float x, UnityEngine.Events.UnityAction onClick)
    {
        var btn = CreateButton(parent, name, glyph, "Option_ArrowBox", new Color(0f, 0f, 0f, 0f), onClick);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(ControlHeight, 0f);
        rt.anchoredPosition = new Vector2(x, 0f);
        var text = btn.GetComponentInChildren<TMP_Text>();
        text.fontSize = 14;
        text.color = ButtonTextColor;
    }

    // 안내 문구 (조작 요소 없음). 두 줄까지 들어간다.
    private TMP_Text CreateNoteRow(GameObject page, float y, string text)
    {
        var go = new GameObject("Note", typeof(RectTransform));
        go.transform.SetParent(page.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, NoteHeight);
        rt.anchoredPosition = new Vector2(0f, -(y + 12f));

        var note = go.AddComponent<TextMeshProUGUI>();
        note.text = text;
        note.fontSize = 16;
        note.alignment = TextAlignmentOptions.TopLeft;
        note.color = HintColor;
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
    // 저장하지 않은 진행 상황이 날아가므로 바로 나가지 않고, 화면 가운데에 확인 창을 띄운다.
    // (예전에는 버튼 글자를 "정말 나가시겠습니까?"로 바꾸는 방식이라 눈에 잘 띄지 않았다)
    private void OnClickMainMenu()
    {
        if (confirmDialog == null)
        {
            GoToMainMenu();
            return;
        }
        confirmDialog.SetActive(true);
        confirmDialog.transform.SetAsLastSibling();
    }

    private void GoToMainMenu()
    {
        if (confirmDialog != null) confirmDialog.SetActive(false);

        if (SavePointManager.Instance != null) SavePointManager.Instance.ResetForNewGame();
        if (SaveManager.Instance != null) SaveManager.Instance.SetActiveSave(null);
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.ResetForNewGame();

        HidePanel();
        UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
    }

    // ===== 확인 창 =====
    //   [설정 화면 위를 덮는 어두운 막]
    //     [가운데 상자 520x260]
    //        메인 화면으로 나가시겠습니까?
    //        저장하지 않은 진행 상황은 사라집니다.
    //                         [취소] [나가기]
    private const float ConfirmWidth = 520f;
    private const float ConfirmHeight = 260f;
    private const float ConfirmPad = 40f;
    private const float ConfirmButtonWidth = 180f;

    private void BuildConfirmDialog(Transform parent)
    {
        confirmDialog = new GameObject("ConfirmMainMenu", typeof(RectTransform), typeof(Image));
        confirmDialog.transform.SetParent(parent, false);
        Stretch(confirmDialog.GetComponent<RectTransform>());
        var dim = confirmDialog.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = true;   // 뒤의 설정 화면이 눌리지 않게 막는다

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline));
        box.transform.SetParent(confirmDialog.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(ConfirmWidth, ConfirmHeight);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f);
        boxImg.raycastTarget = true;
        // 1px 테두리 (패널 그림의 테두리 색)
        var outline = box.GetComponent<Outline>();
        outline.effectColor = new Color(0.227f, 0.282f, 0.333f);   // 3A4855
        outline.effectDistance = new Vector2(1f, -1f);

        AddRect(box.transform, "TopAccent", ConfirmPad, 0f, 120f, 3f, Accent);

        var title = CreateLabel(box.transform, "Title", "메인 화면으로 나가시겠습니까?", 24, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(title.rectTransform, ConfirmPad, 44f, ConfirmWidth - ConfirmPad * 2f, 36f);
        title.fontStyle = FontStyles.Bold;
        title.color = TitleColor;

        var body = CreateLabel(box.transform, "Body", "저장하지 않은 진행 상황은 사라집니다.", 17, TextAlignmentOptions.TopLeft);
        PlaceTopLeft(body.rectTransform, ConfirmPad, 92f, ConfirmWidth - ConfirmPad * 2f, 28f);
        body.color = HintColor;

        float by = ConfirmHeight - ConfirmPad + 8f - ButtonHeight;
        var cancel = CreateButton(box.transform, "Btn_Cancel", "취소", "Button_Secondary", new Color(0f, 0f, 0f, 0f),
                                  () => confirmDialog.SetActive(false));
        PlaceTopLeft(cancel.GetComponent<RectTransform>(), ConfirmWidth - ConfirmPad - ConfirmButtonWidth * 2f - 12f, by, ConfirmButtonWidth, ButtonHeight);

        var ok = CreateButton(box.transform, "Btn_Confirm", "나가기", "Button_Primary", Accent, GoToMainMenu);
        PlaceTopLeft(ok.GetComponent<RectTransform>(), ConfirmWidth - ConfirmPad - ConfirmButtonWidth, by, ConfirmButtonWidth, ButtonHeight);
        var okText = ok.GetComponentInChildren<TMP_Text>();
        okText.fontStyle = FontStyles.Bold;
        okText.color = PrimaryTextColor;

        confirmDialog.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 기본 부품 만들기
    // ---------------------------------------------------------------------------------
    private Sprite LoadSprite(string name)
    {
        if (spriteCache.TryGetValue(name, out var cached)) return cached;
        var sprite = Resources.Load<Sprite>(ArtFolder + name);
        spriteCache[name] = sprite;
        return sprite;
    }

    // 그림이 있으면 그림을, 없으면 fallbackColor 색 도형을 쓴다.
    // (그림이 없을 때 투명색이면 버튼 테두리가 안 보이므로 옅은 선 색으로 바꿔 준다)
    private void SetImage(Image img, string spriteName, Color fallbackColor)
    {
        var sprite = LoadSprite(spriteName);
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        if (sprite != null)
        {
            img.color = Color.white;
        }
        else
        {
            img.color = fallbackColor.a > 0f ? fallbackColor : new Color(TrackColor.r, TrackColor.g, TrackColor.b, 0.35f);
        }
    }

    private TMP_Text CreateLabel(Transform parent, string name, string text,
                                 float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = LabelColor;
        tmp.raycastTarget = false;
        return tmp;
    }

    // 그림 한 장짜리 버튼. 마우스를 올리면 살짝 밝아지고 누르면 살짝 어두워진다.
    private Button CreateButton(Transform parent, string name, string label, string spriteName, Color fallbackColor,
                                UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var bg = go.GetComponent<Image>();
        SetImage(bg, spriteName, fallbackColor);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 버튼이 눌리지 않는다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 19;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = ButtonTextColor;
        tmp.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.94f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.76f, 0.82f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    // 슬라이더: 가는 막대(Slider_Track) + 채워진 부분(Slider_Fill) + 동그란 손잡이(Slider_Knob).
    private Slider CreateSlider(Transform parent, float min, float max)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.direction = Slider.Direction.LeftToRight;

        // 막대
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(0f, 2f);
        bgRt.anchoredPosition = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        SetImage(bgImg, "Slider_Track", TrackColor);

        // 채워지는 부분
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.5f);
        faRt.anchorMax = new Vector2(1f, 0.5f);
        faRt.pivot = new Vector2(0.5f, 0.5f);
        faRt.sizeDelta = new Vector2(0f, 2f);
        faRt.anchoredPosition = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        SetImage(fillImg, "Slider_Fill", Accent);

        // 손잡이 (막대 양 끝에서 손잡이 반지름만큼 안쪽까지 움직인다)
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        // 높이 0인 가로 띠로 둔다. Slider가 손잡이를 이 영역의 위아래 끝까지 세로로 늘리기 때문에,
        // 영역 높이를 0으로 해야 손잡이가 sizeDelta(20x20) 그대로 동그랗게 나온다.
        haRt.anchorMin = new Vector2(0f, 0.5f);
        haRt.anchorMax = new Vector2(1f, 0.5f);
        haRt.pivot = new Vector2(0.5f, 0.5f);
        haRt.sizeDelta = Vector2.zero;
        haRt.anchoredPosition = Vector2.zero;

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.sizeDelta = new Vector2(20f, 20f);
        var hImg = handle.GetComponent<Image>();
        SetImage(hImg, "Slider_Knob", TitleColor);
        hImg.preserveAspect = true;

        slider.fillRect = fRt;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;

        return slider;
    }

    // 패널 왼쪽 위 기준 (x, y)에 w x h 크기로 놓는다.
    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void AddRect(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
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
