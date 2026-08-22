using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 환경설정 화면(탭 4개 + 슬라이더)의 UI 동작을 담당.
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
    [Header("탭 버튼 (상단 4개 아이콘)")]
    public Button tabVolumeButton;
    public Button tabDisplayButton;   // TODO: "화면" 탭 - 아직 내용(밝기/해상도 등)이 정해지지 않아 Content_Display는 "준비 중" placeholder 텍스트만 있음
    public Button tabKeypadButton;    // TODO: "키패드" 탭 - 조작키 설정으로 추정되나 확정 전, Content_Keypad도 placeholder
    public Button tabListButton;      // TODO: "목록" 탭 - 용도 자체가 아직 불명확, Content_List도 placeholder

    [Header("탭별 콘텐츠 패널 (한 번에 하나만 활성화됨)")]
    public GameObject volumeContent;
    public GameObject displayContent;
    public GameObject keypadContent;
    public GameObject listContent;

    [Header("볼륨 탭 슬라이더 (min 0 ~ max 1)")]
    public Slider masterVolumeSlider;
    public Slider sharpnessSlider;   // "선명도" - 화면 탭으로 옮길지 미정이라 우선 여기 있음 (GameSettings.sharpness 주석 참고)
    public Slider bgmVolumeSlider;   // TODO: 실제 BGM 소스가 없어 값 저장만 되고 소리엔 영향 없음
    public Slider sfxVolumeSlider;   // TODO: 실제 SFX 소스가 없어 값 저장만 되고 소리엔 영향 없음

    [Header("공용 버튼")]
    public Button quitButton;
    public Button backButton;

    // 패널로 쓰일 때만 채워지는 콜백. 독립 씬으로 쓰일 땐 null로 두면 됨 (Awake의 backButton 참고).
    public System.Action onBack;

    private void Awake()
    {
        tabVolumeButton.onClick.AddListener(() => ShowTab(volumeContent));
        tabDisplayButton.onClick.AddListener(() => ShowTab(displayContent));
        tabKeypadButton.onClick.AddListener(() => ShowTab(keypadContent));
        tabListButton.onClick.AddListener(() => ShowTab(listContent));

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
        // onBack이 비어있으므로 타이틀 씬으로 돌아간다.
        if (backButton != null) backButton.onClick.AddListener(() =>
        {
            if (onBack != null) onBack.Invoke();
            else SceneManager.LoadScene("Title");
        });
    }

    // 이 오브젝트(또는 이 컴포넌트가 붙은 패널)가 활성화될 때마다 슬라이더 초기값을
    // SettingsManager의 현재 값으로 맞추고, 슬라이더 이벤트를 다시 연결한다.
    // 독립 씬으로 쓰일 땐 씬이 로드될 때 자동으로 한 번 호출된다.
    private void OnEnable()
    {
        // SettingsManager는 Title.unity에서 생성되어 DontDestroyOnLoad로 유지된다.
        // 혹시라도 Title을 거치지 않고 이 씬에 바로 진입한 경우(테스트 등) Instance가
        // 없을 수 있으니 방어적으로 체크한다.
        if (SettingsManager.Instance == null) return;

        var s = SettingsManager.Instance.Current;
        // SetValueWithoutNotify: 초기값을 세팅할 때 onValueChanged가 같이 발동해서
        // "읽어온 값을 다시 저장"하는 불필요한 호출이 일어나지 않도록 함.
        masterVolumeSlider.SetValueWithoutNotify(s.masterVolume);
        sharpnessSlider.SetValueWithoutNotify(s.sharpness);
        bgmVolumeSlider.SetValueWithoutNotify(s.bgmVolume);
        sfxVolumeSlider.SetValueWithoutNotify(s.sfxVolume);

        // OnEnable이 여러 번 호출될 수 있으므로(예: 탭을 왔다갔다 하는 경우는 아니지만
        // 씬을 다시 로드하는 경우 등) 리스너가 중복 등록되지 않도록 먼저 전부 제거한다.
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        sharpnessSlider.onValueChanged.RemoveAllListeners();
        bgmVolumeSlider.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider.onValueChanged.RemoveAllListeners();

        masterVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetMasterVolume);
        sharpnessSlider.onValueChanged.AddListener(SettingsManager.Instance.SetSharpness);
        bgmVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetBgmVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetSfxVolume);

        // 화면을 열 때마다 항상 볼륨 탭부터 보여준다.
        ShowTab(volumeContent);
    }

    // 4개 탭 콘텐츠 중 target 하나만 켜고 나머지는 끈다 (라디오 버튼 방식).
    private void ShowTab(GameObject target)
    {
        volumeContent.SetActive(target == volumeContent);
        displayContent.SetActive(target == displayContent);
        keypadContent.SetActive(target == keypadContent);
        listContent.SetActive(target == listContent);
    }
}
