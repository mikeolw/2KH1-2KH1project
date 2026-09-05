using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

// 환경설정 화면(탭 + 슬라이더/토글/드롭다운)의 UI 동작을 담당.
//
// 두 가지 방식으로 재사용할 수 있게 설계되어 있다:
//   1) 독립된 씬으로 쓰는 경우 (지금의 Settings.unity) - onBack을 아무도 설정하지 않으면
//      뒤로가기 버튼이 자동으로 이전 씬으로 돌아간다.
//   2) 인게임 팝업 패널로 쓰는 경우 - 패널을 만든 쪽에서 onBack에 "패널 닫기" 콜백을
//      대입해주면 그 콜백이 대신 호출된다.
//
// 실제 값 저장/적용은 이 스크립트가 하지 않고 SettingsManager에 위임한다.
// 이 스크립트는 "화면에 뭘 보여줄지"(탭 전환)와 "UI 이벤트를 SettingsManager로 연결하는 것"만 맡는다.
//
// ===== 탭 구성 =====
//   볼륨 탭 : 전체 / 배경음악 / 효과음 크기
//   화면 탭 : 창 모드 (전체화면일 때 남는 공간은 AspectRatioKeeper가 검게 처리)
//   텍스트 탭(새로 추가) : 글꼴, 글씨 크기, 텍스트 출력 속도, 자동 진행
//
// ===== 텍스트 탭이 씬에 없어도 동작하는 이유 =====
// Settings.unity는 볼륨/화면 탭만 있는 상태로 만들어져 있다. 텍스트 탭 UI를 씬에 직접
// 만들려면 유니티 에디터에서 손으로 배치해야 하는데, 그러면 씬 파일이 커지고 팀원끼리
// 충돌하기도 쉽다. 그래서 텍스트 탭 관련 필드가 인스펙터에서 비어 있으면 이 스크립트가
// 게임 시작 시 스스로 만들어준다. 나중에 디자인을 다듬고 싶으면 씬에 직접 만들어서
// 아래 필드에 연결하면 되고, 그때는 자동 생성이 건너뛰어진다.
public class SettingsPanelController : MonoBehaviour
{
    [Header("탭 버튼")]
    public Button tabVolumeButton;
    public Button tabDisplayButton;
    [Tooltip("텍스트 탭 버튼. 비워두면 자동 생성된다.")]
    public Button tabTextButton;

    [Header("탭별 콘텐츠 패널 (한 번에 하나만 활성화됨)")]
    public GameObject volumeContent;
    public GameObject displayContent;
    [Tooltip("텍스트 탭 내용. 비워두면 자동 생성된다.")]
    public GameObject textContent;

    [Header("볼륨 탭 슬라이더 (min 0 ~ max 1)")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("화면 탭")]
    public Toggle windowedToggle;

    [Header("텍스트 탭 (비워두면 자동 생성)")]
    [Tooltip("글꼴 선택. 기본 / 맑은 고딕 / 바탕 / 굴림")]
    public TMP_Dropdown fontDropdown;
    [Tooltip("글씨 크기 배율. 0.7 ~ 1.6")]
    public Slider fontScaleSlider;
    [Tooltip("텍스트 출력 속도. 느리게 / 보통 / 빠르게 / 즉시")]
    public TMP_Dropdown textSpeedDropdown;
    [Tooltip("자동 진행 켜기/끄기")]
    public Toggle autoAdvanceToggle;
    [Tooltip("자동 진행 시 다음 줄로 넘어가기까지의 대기 시간(초). 0.2 ~ 5")]
    public Slider autoAdvanceDelaySlider;
    [Tooltip("설정한 글씨 크기를 미리 볼 수 있는 예시 문장")]
    public TMP_Text fontPreviewText;

    [Header("공용 버튼")]
    public Button quitButton;
    public Button backButton;

    // 패널로 쓰일 때만 채워지는 콜백. 독립 씬으로 쓰일 땐 null로 두면 됨.
    public System.Action onBack;

    // 인게임에서 이 씬이 additive로 겹쳐 떠 있는 동안 true.
    // UIManager.IsAnyPanelOpen이 이 플래그를 확인해서, 설정 화면이 열려 있는 동안
    // 뒤에 깔린 대사가 몰래 진행되지 않도록 막는다.
    public static bool IsOpen { get; private set; }

    // 텍스트 속도 드롭다운에 표시할 이름들. GameSettings.textSpeedLevel의 번호와 순서가 같다.
    private static readonly string[] TextSpeedNames = { "느리게", "보통", "빠르게", "즉시" };

    private void Awake()
    {
        // additive로 겹쳐 뜬 경우 이 씬에 딸려온 EventSystem이 기존 씬 것과 중복돼서
        // "There are 2 event systems" 경고와 입력 충돌이 생긴다. 이 씬 쪽만 제거한다.
        // FindObjectsByType은 유니티 6에서 예전 FindObjectsOfType을 대체한 함수다.
        // EventSystem은 켜져 있는 것만 문제가 되므로 비활성은 찾지 않고,
        // 순서도 상관없으므로 정렬하지 않는다(그게 더 빠르다).
        var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            foreach (var es in eventSystems)
            {
                if (es.gameObject.scene == gameObject.scene) Destroy(es.gameObject);
            }
        }

        EnsureTextTabUI();

        if (tabVolumeButton != null) tabVolumeButton.onClick.AddListener(() => ShowTab(volumeContent));
        if (tabDisplayButton != null) tabDisplayButton.onClick.AddListener(() => ShowTab(displayContent));
        if (tabTextButton != null) tabTextButton.onClick.AddListener(() => ShowTab(textContent));

        // 에디터에서는 플레이 모드를 끄고, 실제 빌드에서는 애플리케이션을 종료한다.
        if (quitButton != null) quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        // 뒤로가기: 인게임에서 additive로 열렸으면 이 씬만 언로드해서 진행 상태를 보존하고,
        // 타이틀에서 진입한 경우엔 원래 씬으로 이동한다.
        if (backButton != null) backButton.onClick.AddListener(() =>
        {
            if (onBack != null)
            {
                onBack.Invoke();
            }
            else if (SettingsManager.Instance != null && SettingsManager.Instance.ReturnAdditive)
            {
                SettingsManager.Instance.ReturnAdditive = false;
                SceneManager.UnloadSceneAsync("Settings");
            }
            else
            {
                SceneManager.LoadScene(SettingsManager.Instance != null ? SettingsManager.Instance.ReturnSceneName : "Title");
            }
        });
    }

    // 화면이 켜질 때마다 모든 UI를 현재 설정값으로 맞추고 이벤트를 다시 연결한다.
    private void OnEnable()
    {
        IsOpen = true;

        if (SettingsManager.Instance == null) return;

        var s = SettingsManager.Instance.Current;
        var manager = SettingsManager.Instance;

        // ===== 볼륨 =====
        // SetValueWithoutNotify: 초기값을 넣을 때 onValueChanged가 같이 터져서
        // "읽어온 값을 다시 저장"하는 불필요한 호출이 일어나지 않도록 막는다.
        BindSlider(masterVolumeSlider, s.masterVolume, manager.SetMasterVolume);
        BindSlider(bgmVolumeSlider, s.bgmVolume, manager.SetBgmVolume);
        BindSlider(sfxVolumeSlider, s.sfxVolume, manager.SetSfxVolume);

        // ===== 화면 =====
        BindToggle(windowedToggle, s.windowed, manager.SetWindowed);

        // ===== 텍스트 =====
        BindDropdown(fontDropdown, FontManager.FontOptionNames, s.fontIndex, manager.SetFontIndex);
        BindSlider(fontScaleSlider, s.fontScale, v =>
        {
            manager.SetFontScale(v);
            UpdateFontPreview();
        }, 0.7f, 1.6f);

        BindDropdown(textSpeedDropdown, TextSpeedNames, s.textSpeedLevel, manager.SetTextSpeedLevel);
        BindToggle(autoAdvanceToggle, s.autoAdvance, manager.SetAutoAdvance);
        BindSlider(autoAdvanceDelaySlider, s.autoAdvanceDelay, manager.SetAutoAdvanceDelay, 0.2f, 5f);

        UpdateFontPreview();

        // 화면을 열 때마다 항상 볼륨 탭부터 보여준다.
        ShowTab(volumeContent);
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    // ---------------------------------------------------------------------------------
    // UI 연결 도우미
    // ---------------------------------------------------------------------------------
    // 아래 세 함수는 "초기값 넣기 -> 기존 리스너 제거 -> 새 리스너 등록"이라는 똑같은 절차를
    // 반복해서 쓰기 때문에 하나로 묶어둔 것이다. 리스너를 매번 제거하는 이유는 OnEnable이
    // 여러 번 호출될 수 있어서(씬 재로드 등) 중복 등록되면 한 번 움직일 때 여러 번 저장되기 때문.

    private void BindSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> onChanged,
                            float? min = null, float? max = null)
    {
        if (slider == null) return;

        if (min.HasValue) slider.minValue = min.Value;
        if (max.HasValue) slider.maxValue = max.Value;

        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(onChanged);
    }

    private void BindToggle(Toggle toggle, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        if (toggle == null) return;

        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(onChanged);
    }

    private void BindDropdown(TMP_Dropdown dropdown, string[] options, int value,
                              UnityEngine.Events.UnityAction<int> onChanged)
    {
        if (dropdown == null) return;

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Length - 1));
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(onChanged);
    }

    // 글씨 크기 미리보기 문장을 갱신한다.
    // 실제 크기 반영은 FontManager가 화면 전체에 적용하므로, 여기서는 안내 문구만 바꾼다.
    private void UpdateFontPreview()
    {
        if (fontPreviewText == null) return;

        float scale = SettingsManager.Instance != null ? SettingsManager.Instance.Current.fontScale : 1f;
        fontPreviewText.text = $"글씨 크기 미리보기 ({scale:0.0}배) - 다시 만난 그 애는 여전히 정의의 용사를 말했다.";
    }

    // 탭 하나만 켜고 나머지는 끈다 (라디오 버튼 방식).
    private void ShowTab(GameObject target)
    {
        if (volumeContent != null) volumeContent.SetActive(target == volumeContent);
        if (displayContent != null) displayContent.SetActive(target == displayContent);
        if (textContent != null) textContent.SetActive(target == textContent);
    }

    // ---------------------------------------------------------------------------------
    // 텍스트 탭 UI 자동 생성
    // ---------------------------------------------------------------------------------
    // 씬에 텍스트 탭이 아직 없을 때, 볼륨 탭 패널을 기준 삼아 같은 자리에 새 패널을 만든다.
    private void EnsureTextTabUI()
    {
        if (textContent != null) return;           // 이미 씬에 있으면 그대로 쓴다
        if (volumeContent == null) return;         // 기준으로 삼을 패널이 없으면 포기

        Transform parent = volumeContent.transform.parent;

        // 볼륨 탭과 같은 크기/위치의 빈 패널을 만든다.
        textContent = new GameObject("Content_Text", typeof(RectTransform));
        textContent.transform.SetParent(parent, false);
        CopyRectTransform(volumeContent.GetComponent<RectTransform>(), textContent.GetComponent<RectTransform>());
        textContent.SetActive(false);

        // 세로로 차곡차곡 쌓이도록 레이아웃을 붙인다.
        var layout = textContent.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 20, 20);
        layout.spacing = 14f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        // 항목들
        fontDropdown = CreateLabeledDropdown(textContent.transform, "글꼴");
        fontScaleSlider = CreateLabeledSlider(textContent.transform, "글씨 크기", 0.7f, 1.6f);
        fontPreviewText = CreateRow(textContent.transform, "", 20);
        textSpeedDropdown = CreateLabeledDropdown(textContent.transform, "텍스트 속도");
        autoAdvanceToggle = CreateLabeledToggle(textContent.transform, "자동 진행");
        autoAdvanceDelaySlider = CreateLabeledSlider(textContent.transform, "자동 진행 대기시간", 0.2f, 5f);

        // 탭 버튼도 없으면 화면 탭 버튼을 복제해서 만든다.
        if (tabTextButton == null && tabDisplayButton != null)
        {
            var clone = Instantiate(tabDisplayButton.gameObject, tabDisplayButton.transform.parent);
            clone.name = "Tab_Text";

            // 복제본에는 원본의 onClick이 그대로 따라오므로 지우고 새로 연결한다.
            tabTextButton = clone.GetComponent<Button>();
            tabTextButton.onClick.RemoveAllListeners();

            var label = clone.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "텍스트";
        }
    }

    private void CopyRectTransform(RectTransform from, RectTransform to)
    {
        if (from == null || to == null) return;
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
    }

    // 한 줄짜리 텍스트를 만든다.
    private TMP_Text CreateRow(Transform parent, string label, float fontSize)
    {
        var go = new GameObject(string.IsNullOrEmpty(label) ? "Preview" : $"Label_{label}",
                                typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 34f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TMP_Dropdown CreateLabeledDropdown(Transform parent, string label)
    {
        CreateRow(parent, label, 22);

        var go = new GameObject($"Dropdown_{label}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 40f;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var dropdown = go.AddComponent<TMP_Dropdown>();

        // TMP_Dropdown은 "닫혀 있을 때 보이는 글자" 오브젝트가 반드시 필요하다.
        var captionGo = new GameObject("Label", typeof(RectTransform));
        captionGo.transform.SetParent(go.transform, false);
        var captionRt = captionGo.GetComponent<RectTransform>();
        captionRt.anchorMin = Vector2.zero;
        captionRt.anchorMax = Vector2.one;
        captionRt.offsetMin = new Vector2(12f, 0f);
        captionRt.offsetMax = new Vector2(-12f, 0f);
        var caption = captionGo.AddComponent<TextMeshProUGUI>();
        caption.fontSize = 20;
        caption.alignment = TextAlignmentOptions.Left;
        caption.color = Color.white;
        caption.raycastTarget = false;
        dropdown.captionText = caption;

        // 펼쳤을 때 나오는 목록(Template)도 만들어야 한다.
        dropdown.template = CreateDropdownTemplate(go.transform, dropdown);

        return dropdown;
    }

    // TMP_Dropdown이 펼쳐질 때 쓰는 목록 틀을 만든다.
    // (유니티가 기본 제공하는 프리팹이 없으므로 최소 구성으로 직접 만든다.)
    private RectTransform CreateDropdownTemplate(Transform parent, TMP_Dropdown dropdown)
    {
        var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(parent, false);
        var templateRt = template.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.anchoredPosition = new Vector2(0f, 2f);
        templateRt.sizeDelta = new Vector2(0f, 150f);
        template.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.98f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 36f);

        // 목록의 항목 하나(Item). TMP_Dropdown이 이걸 복제해서 항목 수만큼 만든다.
        var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        var itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0f, 0.5f);
        itemRt.anchorMax = new Vector2(1f, 0.5f);
        itemRt.sizeDelta = new Vector2(0f, 36f);

        var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBg.transform.SetParent(item.transform, false);
        var itemBgRt = itemBg.GetComponent<RectTransform>();
        itemBgRt.anchorMin = Vector2.zero;
        itemBgRt.anchorMax = Vector2.one;
        itemBgRt.offsetMin = Vector2.zero;
        itemBgRt.offsetMax = Vector2.zero;
        itemBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        var itemLabelGo = new GameObject("Item Label", typeof(RectTransform));
        itemLabelGo.transform.SetParent(item.transform, false);
        var itemLabelRt = itemLabelGo.GetComponent<RectTransform>();
        itemLabelRt.anchorMin = Vector2.zero;
        itemLabelRt.anchorMax = Vector2.one;
        itemLabelRt.offsetMin = new Vector2(12f, 0f);
        itemLabelRt.offsetMax = new Vector2(-12f, 0f);
        var itemLabel = itemLabelGo.AddComponent<TextMeshProUGUI>();
        itemLabel.fontSize = 20;
        itemLabel.alignment = TextAlignmentOptions.Left;
        itemLabel.color = Color.white;

        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBg.GetComponent<Image>();

        var scroll = template.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportRt;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        dropdown.itemText = itemLabel;

        template.SetActive(false);
        return templateRt;
    }

    private Slider CreateLabeledSlider(Transform parent, string label, float min, float max)
    {
        CreateRow(parent, label, 22);

        var go = new GameObject($"Slider_{label}", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 30f;

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        // 슬라이더 배경(홈)
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.35f);
        bgRt.anchorMax = new Vector2(1f, 0.65f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

        // 채워지는 부분
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.9f, 0.8f, 0.4f, 0.9f);

        // 손잡이
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = Vector2.zero;
        handleAreaRt.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 26f);
        handle.GetComponent<Image>().color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private Toggle CreateLabeledToggle(Transform parent, string label)
    {
        var go = new GameObject($"Toggle_{label}", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 34f;

        var toggle = go.AddComponent<Toggle>();

        // 체크박스
        var box = new GameObject("Background", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(go.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 0.5f);
        boxRt.anchorMax = new Vector2(0f, 0.5f);
        boxRt.pivot = new Vector2(0f, 0.5f);
        boxRt.sizeDelta = new Vector2(26f, 26f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

        var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        var checkRt = check.GetComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(5f, 5f);
        checkRt.offsetMax = new Vector2(-5f, -5f);
        check.GetComponent<Image>().color = new Color(0.9f, 0.8f, 0.4f);

        // 라벨
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(36f, 0f);
        labelRt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();

        return toggle;
    }
}
