using UnityEngine;

// 환경설정 값을 PlayerPrefs에 저장/로드하는 매니저.
//
// SaveManager와 마찬가지로 Title.unity에서 생성되어 DontDestroyOnLoad로 씬 전환 간 유지되며,
// 다른 스크립트는 SettingsManager.Instance로 접근한다.
//
// 세이브 슬롯(SaveData)과 다르게 슬롯 구분이 없는 "기기당 설정값 1벌"이라서 JSON 파일이
// 아니라 PlayerPrefs(문자열 1개)로 저장한다. Settings.unity의 SettingsPanelController가
// 슬라이더/토글 값이 바뀔 때마다 이 매니저의 SetXxx() 메서드를 호출해서 즉시 저장 + 적용한다.
//
// ===== 값이 실제로 반영되는 경로 =====
//   masterVolume  -> AudioListener.volume (여기 Apply에서 직접)
//   bgmVolume     -> AudioManager가 BGM AudioSource에 반영
//   sfxVolume     -> AudioManager가 SFX AudioSource들에 반영
//   windowed      -> Screen.SetResolution + AspectRatioKeeper(검은 여백 처리)
//   fontIndex     -> FontManager가 화면의 모든 TMP 텍스트 글꼴 교체
//   fontScale     -> FontManager가 화면의 모든 TMP 텍스트 크기 배율 적용
//   textSpeedLevel/autoAdvance -> DialogueSystem이 대사 출력할 때 직접 읽어감
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    // PlayerPrefs에 저장할 때 쓰는 키. 필드 구조를 크게 바꾸면 버전을 올려서(_v3 등)
    // 기존 유저의 예전 형식 데이터와 충돌하지 않게 하는 걸 권장.
    // (v1 -> v2: 글꼴/글씨크기/텍스트속도/자동진행 설정이 추가됨. 기존 값은 기본값으로 채워진다.)
    private const string PrefsKey = "GameSettings_v2";

    // ===== 해상도 =====
    // 이 게임의 그림은 전부 1440x1080(4:3)으로 그려져 있다. 창 모드 크기도 같은 4:3 비율로
    // 잡아야 그림이 찌그러지지 않는다. 1440x1080의 75% 크기.
    public const int ReferenceWidth = 1440;
    public const int ReferenceHeight = 1080;
    private const int WindowedWidth = 1080;
    private const int WindowedHeight = 810;

    // 전체화면일 때 쓰는 해상도. 모니터가 16:9여도 AspectRatioKeeper가 화면 가운데에
    // 4:3 영역만 그리고 좌우를 검게 남기므로 그림은 찌그러지지 않는다.
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;

    // 현재 적용 중인 설정값. UI(슬라이더)는 이 값을 읽어서 초기 상태를 표시한다.
    public GameSettings Current { get; private set; } = new GameSettings();

    // 설정이 바뀔 때마다 발생하는 이벤트. AudioManager/FontManager/AspectRatioKeeper처럼
    // "설정을 실제로 화면·소리에 반영하는" 쪽에서 구독해두면, 슬라이더를 움직이는 즉시
    // 반영된다. (SettingsManager가 그 클래스들을 직접 찾아 부르지 않고 이벤트로 알리는 이유:
    // 어떤 씬에는 AudioManager가 없을 수도 있는데, 그때마다 null 체크를 하기보다
    // "있으면 알아서 듣는" 구조가 씬 구성에 훨씬 자유롭기 때문이다.)
    public event System.Action OnSettingsChanged;

    // Settings.unity의 뒤로가기 버튼이 돌아갈 씬 이름. 기본값은 Title에서 진입한 경우를
    // 위한 "Title". SampleScene 등 인게임에서 설정 화면을 열 때는 진입 직전에 이 값을
    // 그 씬 이름으로 바꿔준다 (UIManager.OpenSettingsScene 참고).
    public string ReturnSceneName = "Title";

    // OpenSettingsScene()에서 인게임(SampleScene 등)에 설정 화면을 "덮어씌우는" 형태로
    // additive 로드했는지 여부. true면 뒤로가기를 눌렀을 때 원래 씬을 다시 로드하는 게 아니라
    // Settings 씬만 언로드해서 원래 씬(대사 진행 상태 등)을 그대로 유지해야 한다.
    public bool ReturnAdditive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // PlayerPrefs에서 설정값을 읽어온다. 저장된 값이 없으면(최초 실행) 기본값을 사용한다.
    public void Load()
    {
        Current = PlayerPrefs.HasKey(PrefsKey)
            ? JsonUtility.FromJson<GameSettings>(PlayerPrefs.GetString(PrefsKey))
            : new GameSettings();

        // JsonUtility는 저장된 문자열이 깨져 있으면 null을 반환할 수 있다. 방어 코드.
        if (Current == null) Current = new GameSettings();

        Apply();
    }

    // 현재 값을 PlayerPrefs에 저장하고 즉시 적용한다. SetXxx() 메서드들이 값 변경 후
    // 항상 이 메서드를 호출하므로, 슬라이더를 움직이는 즉시 저장까지 이뤄진다.
    public void SaveAndApply()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
        Apply();
    }

    // 실제 게임에 설정값을 반영한다.
    private void Apply()
    {
        // 마스터 볼륨은 게임 전체 소리에 곱해지는 값이라 여기서 바로 적용한다.
        AudioListener.volume = Current.masterVolume;

        // 해상도/전체화면. 에디터 Game 뷰에서는 Screen.SetResolution이 효과가 없으므로
        // (빌드에서만 동작) 실제 창 크기 변화는 빌드에서만 확인 가능하다.
        if (Current.windowed)
            Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
        else
            Screen.SetResolution(DefaultWidth, DefaultHeight, FullScreenMode.FullScreenWindow);

        // 나머지(BGM/SFX 볼륨, 글꼴, 글씨 크기, 화면 비율)는 각 담당 스크립트가
        // 이 이벤트를 듣고 알아서 반영한다.
        OnSettingsChanged?.Invoke();
    }

    // ---------------------------------------------------------------------------------
    // UI(슬라이더/토글/드롭다운)에서 직접 연결하는 콜백들
    // ---------------------------------------------------------------------------------
    public void SetMasterVolume(float v) { Current.masterVolume = Mathf.Clamp01(v); SaveAndApply(); }
    public void SetBgmVolume(float v) { Current.bgmVolume = Mathf.Clamp01(v); SaveAndApply(); }
    public void SetSfxVolume(float v) { Current.sfxVolume = Mathf.Clamp01(v); SaveAndApply(); }
    public void SetWindowed(bool v) { Current.windowed = v; SaveAndApply(); }

    public void SetFontIndex(int v) { Current.fontIndex = Mathf.Max(0, v); SaveAndApply(); }
    public void SetFontScale(float v) { Current.fontScale = Mathf.Clamp(v, 0.7f, 1.6f); SaveAndApply(); }

    public void SetTextSpeedLevel(int v) { Current.textSpeedLevel = Mathf.Clamp(v, 0, 3); SaveAndApply(); }
    public void SetAutoAdvance(bool v) { Current.autoAdvance = v; SaveAndApply(); }
    public void SetAutoAdvanceDelay(float v) { Current.autoAdvanceDelay = Mathf.Clamp(v, 0.2f, 5f); SaveAndApply(); }

    // ---------------------------------------------------------------------------------
    // 텍스트 속도 환산
    // ---------------------------------------------------------------------------------

    // textSpeedLevel(0~3)을 "초당 몇 글자를 찍을지"로 바꿔준다.
    // 숫자는 기존 미연시(비주얼 노벨) 게임들의 체감 속도를 참고해서 잡았다:
    //   느리게 20자/초  - 소리 내어 읽는 정도
    //   보통   40자/초  - 대부분의 상용 게임 기본값
    //   빠르게 80자/초  - 이미 읽은 대사를 다시 볼 때 쓰는 속도
    //   즉시            - 타이핑 없이 한 번에 표시 (아래 IsInstantText로 따로 판정)
    public float TextSpeedCharsPerSecond
    {
        get
        {
            switch (Current.textSpeedLevel)
            {
                case 0: return 20f;
                case 2: return 80f;
                case 3: return float.MaxValue; // 사실상 즉시
                default: return 40f;           // 1 = 보통
            }
        }
    }

    // "즉시" 단계인지. DialogueSystem이 타이핑 코루틴을 아예 건너뛸지 판단할 때 쓴다.
    public bool IsInstantText => Current.textSpeedLevel >= 3;
}
