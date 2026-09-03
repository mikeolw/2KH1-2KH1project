using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

// 환경설정 화면(탭 2개 + 슬라이더)의 UI 동작을 담당.
//
// 두 가지 방식으로 재사용할 수 있게 설계되어 있다:
//   1) 독립된 씬으로 쓰는 경우 (지금의 Settings.unity) - onBack을 아무도 설정하지 않으면
//      뒤로가기 버튼이 자동으로 SceneManager.LoadScene("Title")을 호출한다.
//   2) 인게임 팝업 패널로 쓰는 경우 (예: 나중에 SampleScene의 UIManager.settingsPanel에
//      이 컴포넌트가 붙는 경우) - 이때는 패널을 만든 쪽에서 onBack에 "패널 닫기" 같은
//      콜백을 대입해주면 그 콜백이 대신 호출된다. TitleManager가 예전에 이 방식으로 썼었고,
//      지금은 씬 분리로 안 쓰이지만 구조는 남겨뒀다.
//
// 실제 슬라이더 값 저장/적용은 이 스크립트가 하지 않고 SettingsManager에 위임한다.
// 이 스크립트는 "화면에 뭘 보여줄지"(탭 전환)와 "슬라이더 이벤트를 SettingsManager로
// 연결하는 것"만 담당한다.
public class SettingsPanelController : MonoBehaviour
{
    [Header("탭 버튼 (상단 2개 아이콘)")]
    public Button tabVolumeButton;
    public Button tabDisplayButton;   // "화면" 탭 - 창 모드 토글 추가됨 (밝기 등 나머지는 아직 미정)

    [Header("탭별 콘텐츠 패널 (한 번에 하나만 활성화됨)")]
    public GameObject volumeContent;
    public GameObject displayContent;

    [Header("볼륨 탭 슬라이더 (min 0 ~ max 1)")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;   // TODO: 실제 BGM 소스가 없어 값 저장만 되고 소리엔 영향 없음
    public Slider sfxVolumeSlider;   // TODO: 실제 SFX 소스가 없어 값 저장만 되고 소리엔 영향 없음

    [Header("화면 탭 (Content_Display)")]
    public Toggle windowedToggle;

    [Header("공용 버튼")]
    public Button quitButton;
    public Button backButton;

    // 패널로 쓰일 때만 채워지는 콜백. 독립 씬으로 쓰일 땐 null로 두면 됨 (Awake의 backButton 참고).
    public System.Action onBack;

    // 인게임(SampleScene)에서 UIManager.OpenSettingsScene()으로 이 씬이 additive로 겹쳐
    // 떠 있는 동안 true. UIManager.IsAnyPanelOpen이 이 플래그를 확인해서, 설정 화면이 열려
    // 있는 동안 뒤에 깔린 대사(스페이스바 등)가 몰래 진행되지 않도록 막는다.
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        // additive로 겹쳐 뜬 경우 이 씬에 딸려온 EventSystem이 기존 씬의 EventSystem과
        // 중복돼서 "There are 2 event systems" 경고와 입력 충돌이 생긴다. 이 씬(Settings)
        // 쪽 EventSystem만 제거하고 원래 씬 것 하나만 남긴다. 독립 씬으로 쓰일 땐(Single 로드)
        // EventSystem이 원래 하나뿐이라 아무 효과 없다.
        var eventSystems = FindObjectsOfType<EventSystem>();
        if (eventSystems.Length > 1)
        {
            foreach (var es in eventSystems)
            {
                if (es.gameObject.scene == gameObject.scene) Destroy(es.gameObject);
            }
        }

        tabVolumeButton.onClick.AddListener(() => ShowTab(volumeContent));
        tabDisplayButton.onClick.AddListener(() => ShowTab(displayContent));

        // 에디터에서는 플레이 모드를 끄고, 실제 빌드에서는 애플리케이션을 종료한다.
        // (Application.Quit()은 에디터 플레이 모드에서는 아무 효과가 없기 때문에 분기 처리)
        quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        // 패널로 쓰일 땐 onBack이 지정되고, Settings.unity처럼 독립된 씬으로 쓰일 땐
        // onBack이 비어있다. 이 경우 UIManager.OpenSettingsScene()이 additive로 겹쳐 띄운
        // 것이면(ReturnAdditive) 원래 씬은 손대지 않고 이 씬만 언로드해서 대사/시나리오
        // 진행 상태를 그대로 유지하고, 그게 아니면(Title에서 진입한 경우) 원래 하던 대로
        // ReturnSceneName(기본 타이틀)으로 이동한다.
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

    // 이 오브젝트(또는 이 컴포넌트가 붙은 패널)가 활성화될 때마다 슬라이더 초기값을
    // SettingsManager의 현재 값으로 맞추고, 슬라이더 이벤트를 다시 연결한다.
    // 독립 씬으로 쓰일 땐 씬이 로드될 때 자동으로 한 번 호출된다.
    private void OnEnable()
    {
        IsOpen = true;

        // SettingsManager는 Title.unity에서 생성되어 DontDestroyOnLoad로 유지된다.
        // 혹시라도 Title을 거치지 않고 이 씬에 바로 진입한 경우(테스트 등) Instance가
        // 없을 수 있으니 방어적으로 체크한다.
        if (SettingsManager.Instance == null) return;

        var s = SettingsManager.Instance.Current;
        // SetValueWithoutNotify: 초기값을 세팅할 때 onValueChanged가 같이 발동해서
        // "읽어온 값을 다시 저장"하는 불필요한 호출이 일어나지 않도록 함.
        masterVolumeSlider.SetValueWithoutNotify(s.masterVolume);
        bgmVolumeSlider.SetValueWithoutNotify(s.bgmVolume);
        sfxVolumeSlider.SetValueWithoutNotify(s.sfxVolume);
        windowedToggle.SetIsOnWithoutNotify(s.windowed);

        // OnEnable이 여러 번 호출될 수 있으므로(예: 탭을 왔다갔다 하는 경우는 아니지만
        // 씬을 다시 로드하는 경우 등) 리스너가 중복 등록되지 않도록 먼저 전부 제거한다.
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        bgmVolumeSlider.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        windowedToggle.onValueChanged.RemoveAllListeners();

        masterVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetMasterVolume);
        bgmVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetBgmVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetSfxVolume);
        windowedToggle.onValueChanged.AddListener(SettingsManager.Instance.SetWindowed);

        // 화면을 열 때마다 항상 볼륨 탭부터 보여준다.
        ShowTab(volumeContent);
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    // 2개 탭 콘텐츠 중 target 하나만 켜고 나머지는 끈다 (라디오 버튼 방식).
    private void ShowTab(GameObject target)
    {
        volumeContent.SetActive(target == volumeContent);
        displayContent.SetActive(target == displayContent);
    }
}
