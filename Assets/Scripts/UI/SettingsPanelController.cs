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
    [Tooltip("메인(타이틀) 화면으로 돌아가는 버튼. 비워두면 자동 생성된다.")]
    public Button mainMenuButton;

    [Header("메인 화면 씬 이름")]
    public string titleSceneName = "Title";

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
        // EventSystem은 켜져 있는 것만 문제가 되므로 비활성은 찾지 않는다.
        var eventSystems = FindObjectsByType<EventSystem>();
        if (eventSystems.Length > 1)
        {
            foreach (var es in eventSystems)
            {
                if (es.gameObject.scene == gameObject.scene) Destroy(es.gameObject);
            }
        }

        BringSettingsCanvasToFront();
        EnsureTextTabUI();

        if (tabVolumeButton != null) tabVolumeButton.onClick.AddListener(() => ShowTab(volumeContent));
        if (tabDisplayButton != null) tabDisplayButton.onClick.AddListener(() => ShowTab(displayContent));

        // 텍스트 설정은 탭이 아니라 화면을 덮는 별도 창이다(EnsureTextTabUI 주석 참고).
        // 그래서 ShowTab을 쓰면 안 된다 - 그러면 볼륨/화면 탭이 꺼진 채로 남아서,
        // 텍스트 창을 닫았을 때 옵션 화면이 텅 비어 보인다. 창만 켜고 끈다.
        if (tabTextButton != null) tabTextButton.onClick.AddListener(() =>
        {
            if (textContent != null) textContent.SetActive(true);
        });

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
        if (backButton != null) backButton.onClick.AddListener(OnClickBack);

        EnsureMainMenuButton();
    }

    // ===== 메인 화면으로 돌아가기 =====
    // 인게임 도중 옵션을 열었을 때 타이틀로 나갈 방법이 없었다.
    // 저장하지 않은 진행 상황이 날아가므로 한 번 더 확인하는 절차를 둔다.
    private bool mainMenuConfirming;

    private void OnClickMainMenu()
    {
        // 첫 번째 클릭: 정말 나갈 건지 확인 문구로 바꾼다.
        if (!mainMenuConfirming)
        {
            mainMenuConfirming = true;
            SetMainMenuLabel("정말 나가시겠습니까?");
            return;
        }

        // 두 번째 클릭: 실제로 타이틀로 나간다.
        //
        // 겹쳐 띄운 상태(additive)에서 그냥 씬을 바꾸면 옵션 씬이 남아 떠다닐 수 있으므로,
        // 플래그를 정리하고 Single 모드로 타이틀을 불러온다(그러면 모든 씬이 정리된다).
        if (SettingsManager.Instance != null) SettingsManager.Instance.ReturnAdditive = false;

        // 이어서 하던 세이브 정보도 비운다. 안 그러면 타이틀에서 새 게임을 눌러도
        // 직전에 하던 지점부터 시작해 버린다.
        if (SavePointManager.Instance != null) SavePointManager.Instance.ResetForNewGame();

        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }

    private void SetMainMenuLabel(string text)
    {
        if (mainMenuButton == null) return;
        var label = mainMenuButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
    }

    // 씬에 메인 화면 버튼이 없으면 만들어 붙인다.
    //
    // ===== 기존 버튼을 복제하지 않는 이유 =====
    // 처음에는 뒤로가기 버튼을 복제해서 그 위에 놓으려고 했다. 그런데 씬의 버튼이 어떤
    // 앵커/좌표계로 배치돼 있는지 코드가 알 수 없어서, 복제본이 엉뚱한 자리에 앉거나
    // 다른 UI를 덮으면서 옵션 화면이 깨져 보였다.
    // 그래서 복제 대신 화면 왼쪽 위 구석이라는 고정된 자리에 새로 만든다.
    // 그 자리는 대부분의 옵션 화면에서 비어 있어 기존 UI와 겹칠 위험이 적다.
    private void EnsureMainMenuButton()
    {
        if (mainMenuButton == null)
        {
            mainMenuButton = CreateOverlayButton(transform, "Btn_MainMenu", "메인 화면으로",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(140f, -28f), new Vector2(240f, 52f),
                null);
        }

        SetMainMenuLabel("메인 화면으로");
        mainMenuButton.onClick.RemoveAllListeners();
        mainMenuButton.onClick.AddListener(OnClickMainMenu);
    }

    // ===== 뒤로가기 =====
    // 인게임에서 겹쳐 띄운 경우(additive)에는 이 씬만 내리고, 타이틀에서 진입한 경우에는
    // 원래 씬으로 이동한다.
    //
    // ===== 고친 문제: 옵션 화면이 인게임 화면 "뒤로" 넘어가 보이던 현상 =====
    // 씬을 내리는 UnloadSceneAsync는 이름 그대로 비동기라서, 요청한 뒤 실제로 사라지기까지
    // 몇 프레임이 걸린다. 그 사이에도 옵션 화면은 그대로 살아 있는데, 인게임 캔버스가
    // 화면 비율 고정(AspectRatioKeeper) 때문에 Screen Space - Camera로 바뀌어 있어서
    // 그리는 순서가 뒤바뀌어 옵션 화면이 인게임 뒤에 깔린 것처럼 보였다.
    // 그래서 내리기를 요청하는 즉시 이 씬의 화면을 꺼버려서, 사라지는 동안 어중간하게
    // 보이는 일이 없게 했다.
    private void OnClickBack()
    {
        if (onBack != null)
        {
            onBack.Invoke();
            return;
        }

        bool additive = SettingsManager.Instance != null && SettingsManager.Instance.ReturnAdditive;

        if (additive)
        {
            SettingsManager.Instance.ReturnAdditive = false;

            // 실제로 씬이 사라지기 전에 먼저 눈에서 치운다.
            HideImmediately();

            var op = SceneManager.UnloadSceneAsync("Settings");
            if (op == null)
            {
                // 어떤 이유로든 내리기에 실패하면(씬 이름이 다르거나 이미 내려간 경우)
                // 화면만이라도 확실히 꺼둔 상태로 남긴다.
                Debug.LogWarning("[SettingsPanelController] Settings 씬을 내리지 못했습니다. 화면만 껐습니다.");
            }
            return;
        }

        SceneManager.LoadScene(SettingsManager.Instance != null ? SettingsManager.Instance.ReturnSceneName : "Title");
    }

    // 이 씬에 속한 최상위 오브젝트를 전부 꺼서 즉시 화면에서 사라지게 한다.
    private void HideImmediately()
    {
        IsOpen = false;

        var scene = gameObject.scene;
        if (!scene.IsValid()) { gameObject.SetActive(false); return; }

        foreach (var root in scene.GetRootGameObjects())
        {
            root.SetActive(false);
        }
    }

    // ===== 옵션 화면이 항상 인게임 위에 그려지게 한다 =====
    // 인게임 캔버스는 화면 비율 고정(AspectRatioKeeper) 때문에 Screen Space - Camera로
    // 바뀌어 있다. 겹쳐 띄운 옵션 화면의 캔버스가 그대로 두면 그리는 순서가 뒤엉켜
    // 인게임 뒤로 밀려 보일 수 있다. 그래서 옵션 캔버스는 확실히 맨 위에 오도록,
    // 인게임과 같은 방식(카메라 기준)으로 맞추고 정렬 순서를 크게 올려둔다.
    private void BringSettingsCanvasToFront()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                // 다른 캔버스 안에 들어있는 캔버스는 부모를 따라가므로 건드리지 않는다.
                if (canvas.transform.parent != null &&
                    canvas.transform.parent.GetComponentInParent<Canvas>() != null) continue;

                // ===== 그리는 방식(renderMode)은 절대 건드리지 않는다 =====
                // 예전에는 여기서 Screen Space - Camera로 바꿨는데, 그러면 이 씬에 맞춰
                // 설정해둔 캔버스 스케일러 기준이 어긋나면서 옵션 화면 레이아웃이 통째로
                // 무너졌다. Screen Space - Overlay 캔버스는 원래 카메라 기준 캔버스보다
                // 항상 나중에(= 위에) 그려지므로, 방식을 바꿀 필요 자체가 없다.
                // 정렬 순서만 올려두면 확실히 맨 위에 온다.
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
            }
        }
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

        // 화면을 닫았다 다시 열면 "정말 나가시겠습니까?" 확인 상태는 초기화한다.
        mainMenuConfirming = false;
        SetMainMenuLabel("메인 화면으로");

        // 텍스트 설정 창도 닫아둔 상태로 시작한다.
        if (textContent != null) textContent.SetActive(false);

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
    // 텍스트 설정은 탭이 아니라 별도 창이므로 여기서 다루지 않는다.
    private void ShowTab(GameObject target)
    {
        if (volumeContent != null) volumeContent.SetActive(target == volumeContent);
        if (displayContent != null) displayContent.SetActive(target == displayContent);
    }

    // ---------------------------------------------------------------------------------
    // 텍스트 탭 UI 자동 생성
    // ---------------------------------------------------------------------------------
    // 씬에 텍스트 탭이 아직 없을 때, 볼륨 탭 패널을 기준 삼아 같은 자리에 새 패널을 만든다.
    // ---------------------------------------------------------------------------------
    // 텍스트 설정 화면 만들기 (글꼴 / 글씨 크기 / 출력 속도 / 자동 진행)
    // ---------------------------------------------------------------------------------
    // ===== 왜 "탭"이 아니라 독립 오버레이인가 =====
    // 처음에는 씬에 있던 볼륨/화면 탭 옆에 같은 모양의 탭을 하나 더 만들려고, 기존 탭 버튼을
    // 복제해서 옆으로 밀어 붙였다. 그런데 씬의 옵션 화면이 어떤 좌표계로 짜여 있는지 코드가
    // 알 수 없다 보니, 복제한 버튼과 새 패널이 기존 UI 위에 겹쳐 앉으면서 옵션 화면 전체가
    // 깨져 보였다.
    //
    // 그래서 방식을 바꿨다. 기존 UI는 손끝 하나 대지 않고,
    //   - 화면 위쪽 가운데에 "텍스트 설정" 버튼 하나만 새로 놓고,
    //   - 그 버튼을 누르면 화면 전체를 덮는 별도 창이 열리게
    // 했다. 창이 열리면 기존 UI를 완전히 가리므로 겹쳐서 깨져 보일 일이 없고,
    // 닫으면 원래 옵션 화면이 그대로 돌아온다.
    private void EnsureTextTabUI()
    {
        if (textContent != null) return;   // 이미 씬에 만들어둔 것이 있으면 그대로 쓴다

        // ===== 화면 전체를 덮는 창 =====
        textContent = new GameObject("TextSettingsOverlay", typeof(RectTransform), typeof(Image));
        textContent.transform.SetParent(transform, false);

        var overlayRect = textContent.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var overlayBg = textContent.GetComponent<Image>();
        overlayBg.color = new Color(0f, 0f, 0f, 0.97f);
        overlayBg.raycastTarget = true;   // 뒤쪽 UI가 눌리지 않게 막는다

        // 항상 기존 UI보다 앞에 그려지도록 계층 맨 끝으로 보낸다.
        textContent.transform.SetAsLastSibling();
        textContent.SetActive(false);

        // ===== 창 제목 =====
        var header = CreateOverlayLabel(textContent.transform, "Header", "텍스트 설정",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(60f, -96f), new Vector2(-60f, -36f), 34, TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;
        header.color = new Color(1f, 0.86f, 0.45f);

        // ===== 항목들이 세로로 쌓이는 영역 =====
        var list = new GameObject("Items", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(textContent.transform, false);

        var listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(60f, 110f);   // 아래는 닫기 버튼 자리를 비워둔다
        listRect.offsetMax = new Vector2(-60f, -110f); // 위는 제목 자리를 비워둔다

        var layout = list.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperLeft;

        // ===== 항목 =====
        fontDropdown = CreateLabeledDropdown(list.transform, "글꼴");
        fontScaleSlider = CreateLabeledSlider(list.transform, "글씨 크기", 0.7f, 1.6f);
        fontPreviewText = CreateRow(list.transform, "", 20);
        textSpeedDropdown = CreateLabeledDropdown(list.transform, "텍스트 속도");
        autoAdvanceToggle = CreateLabeledToggle(list.transform, "자동 진행 (대사가 저절로 넘어갑니다)");
        autoAdvanceDelaySlider = CreateLabeledSlider(list.transform, "자동 진행 대기시간(초)", 0.2f, 5f);

        // ===== 닫기 버튼 =====
        CreateOverlayButton(textContent.transform, "Btn_CloseTextSettings", "닫기",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(240f, 56f),
            () => textContent.SetActive(false));

        // ===== 이 창을 여는 버튼 =====
        // 화면 위쪽 가운데는 대부분의 옵션 화면에서 비어 있는 자리라 기존 UI와 겹칠 위험이 적다.
        if (tabTextButton == null)
        {
            tabTextButton = CreateOverlayButton(transform, "Btn_OpenTextSettings", "텍스트 설정",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(240f, 52f),
                null);
        }
    }

    // 오버레이용 글자 한 줄.
    private TMP_Text CreateOverlayLabel(Transform parent, string name, string text,
                                        Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 offsetMin, Vector2 offsetMax,
                                        float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    // 오버레이용 버튼. 위치를 앵커 + 오프셋 + 크기로 직접 지정한다.
    private Button CreateOverlayButton(Transform parent, string name, string label,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 anchoredPosition, Vector2 size,
                                       UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, anchorMin.y >= 1f ? 1f : (anchorMin.y <= 0f ? 0f : 0.5f));
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var bg = go.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.20f);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 버튼이 눌리지 않는다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
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
        colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.35f);
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
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
        template.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.98f);

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

    // ===== 켜고 끌 수 있는 토글(체크박스) 만들기 =====
    // 예전에는 글씨만 보이고 눌러도 아무 반응이 없었다. 원인:
    //   - 라벨 영역이 체크박스 위까지 덮고 있는데 라벨의 raycastTarget이 꺼져 있어서,
    //     체크박스 아주 작은 부분 말고는 클릭이 잡히지 않았다.
    //   - 클릭 판정을 받아줄 투명한 배경이 없었다.
    // 그래서 줄 전체를 덮는 투명한 클릭 영역을 깔고, 그 위에 체크박스와 글씨를 올렸다.
    // 이제 줄 아무 데나 눌러도 켜고 끌 수 있다.
    private Toggle CreateLabeledToggle(Transform parent, string label)
    {
        var go = new GameObject($"Toggle_{label}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 40f;

        // 줄 전체를 덮는 클릭 영역. 거의 투명하지만 raycastTarget이 켜져 있어야 클릭이 잡힌다.
        var rowBg = go.GetComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.06f);
        rowBg.raycastTarget = true;

        var toggle = go.AddComponent<Toggle>();

        // 체크박스 테두리
        var box = new GameObject("Background", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(go.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 0.5f);
        boxRt.anchorMax = new Vector2(0f, 0.5f);
        boxRt.pivot = new Vector2(0f, 0.5f);
        boxRt.sizeDelta = new Vector2(28f, 28f);
        boxRt.anchoredPosition = new Vector2(8f, 0f);
        var boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(1f, 1f, 1f, 0.25f);
        boxImg.raycastTarget = false;   // 클릭은 줄 전체(rowBg)가 받는다

        // 체크 표시 (켜졌을 때만 보인다)
        var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        var checkRt = check.GetComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(5f, 5f);
        checkRt.offsetMax = new Vector2(-5f, -5f);
        var checkImg = check.GetComponent<Image>();
        checkImg.color = new Color(1f, 0.82f, 0.35f);
        checkImg.raycastTarget = false;

        // 라벨
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(46f, 0f);
        labelRt.offsetMax = new Vector2(-8f, 0f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // targetGraphic : 클릭 판정과 강조 색의 기준
        // graphic       : 켜졌을 때 보여줄 체크 표시
        toggle.targetGraphic = rowBg;
        toggle.graphic = checkImg;
        toggle.transition = Selectable.Transition.ColorTint;

        var colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.4f);
        toggle.colors = colors;

        return toggle;
    }
}
