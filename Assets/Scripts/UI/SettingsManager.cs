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
    // 잡아야 그림이 찌그러지지 않는다.
    public const int ReferenceWidth = 1440;
    public const int ReferenceHeight = 1080;

    // 창 모드 크기를 모니터 세로 길이의 몇 배로 할지.
    //
    // ===== 왜 고정 크기가 아닌가 =====
    // 예전에는 1080x810으로 못박아 두었다. 1920x1080 모니터에서는 적당했지만, 2560x1440에서는
    // 화면 세로의 56%, 4K(3840x2160)에서는 37%밖에 안 차서 "창이 너무 작게 나온다"는 문제가 됐다.
    // 모니터 해상도에서 계산하면 어느 모니터에서도 같은 크기감으로 열린다.
    //
    // 0.85인 이유: 제목 표시줄(약 30px)과 윈도우 작업 표시줄이 차지하는 공간을 남겨둬야
    // 창 아래쪽이 화면 밖으로 잘리지 않는다.
    private const float WindowedScreenRatio = 0.85f;

    // 창 모드 최소 세로 크기. 이보다 작으면 대사창의 글씨가 읽기 힘들어진다.
    // (단, 모니터가 이보다 작으면 모니터 크기를 따른다. 아래 WindowedSize 참고)
    private const int MinWindowedHeight = 720;

    // 모니터 해상도를 알아낼 수 없을 때만 쓰는 비상용 값.
    private const int FallbackWidth = 1920;
    private const int FallbackHeight = 1080;

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

    // (제거됨) 예전에는 인게임 설정 화면을 별도 씬으로 겹쳐 띄우고, 뒤로가기에서 그 씬만
    // 내려야 하는지 판단하려고 ReturnAdditive라는 값을 들고 있었다. 지금은 인게임 설정이
    // 씬이 아니라 게임 씬 안의 패널(SettingsPanelUI)이라 씬을 오갈 일이 없어져 필요 없다.

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

        // 해상도/전체화면.
        ApplyScreenMode();

        // 나머지(BGM/SFX 볼륨, 글꼴, 글씨 크기, 화면 비율)는 각 담당 스크립트가
        // 이 이벤트를 듣고 알아서 반영한다.
        OnSettingsChanged?.Invoke();
    }

    // ---------------------------------------------------------------------------------
    // 창 모드 / 전체화면 적용
    // ---------------------------------------------------------------------------------

    // 마지막으로 적용한 창 모드. 아직 한 번도 적용하지 않았으면 null이다.
    //
    // ===== 왜 기억해 두는가 =====
    // Apply()는 볼륨 슬라이더를 움직일 때마다 불린다. 그때마다 Screen.SetResolution을
    // 다시 호출하면 (1) 창이 미세하게 깜빡이고, (2) 플레이어가 창 테두리를 끌어 키워둔
    // 크기를 매번 원래 크기로 되돌려 버린다. 모드가 실제로 바뀔 때만 호출한다.
    private bool? appliedWindowed;

    private void ApplyScreenMode()
    {
        // 에디터 Game 뷰에서는 Screen.SetResolution이 효과가 없다(빌드에서만 동작).
        // 실제 창 크기 변화는 빌드에서만 확인할 수 있다.
        if (appliedWindowed == Current.windowed) return;
        appliedWindowed = Current.windowed;

        if (Current.windowed)
        {
            Vector2Int size = WindowedSize();
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }
        else
        {
            // 전체화면은 모니터 해상도 그대로 쓴다. 예전처럼 1920x1080으로 못박으면
            // 1440p/4K 모니터에서 1080p로 렌더링한 화면을 OS가 늘려 보여주기 때문에
            // 선화가 한 겹 뭉개져 흐릿해진다.
            // 모니터가 16:9여도 AspectRatioKeeper가 화면 가운데 4:3 영역만 그리고
            // 좌우를 검게 남기므로 그림이 찌그러지지는 않는다.
            Vector2Int mon = DisplaySize();
            Screen.SetResolution(mon.x, mon.y, FullScreenMode.FullScreenWindow);
        }

        LogScreenMode();
    }

    // 창 크기 계산에 쓴 값들을 한 줄로 남긴다.
    //
    // 창 크기는 OS마다 화면 배율을 다루는 방식이 달라서 윈도우에서 맞아도 맥/리눅스에서
    // 어긋날 수 있다. 다른 OS에서 "창이 작다/크다"는 말이 나왔을 때 이 로그 한 줄만 보면
    // 어느 값이 틀어졌는지 바로 알 수 있다. (로그 파일 위치: 맥은
    // ~/Library/Logs/<회사명>/<게임명>/Player.log, 리눅스는 ~/.config/unity3d/.../Player.log)
    private void LogScreenMode()
    {
        DisplayInfo info = Screen.mainWindowDisplayInfo;
        Resolution res = Screen.currentResolution;

        Debug.Log($"[SettingsManager] 화면 적용: {(Current.windowed ? "창 모드" : "전체화면")} / " +
                  $"요청 크기 {(Current.windowed ? WindowedSize() : DisplaySize())} / " +
                  $"currentResolution {res.width}x{res.height} / " +
                  $"displayInfo {info.width}x{info.height} / " +
                  $"실제 Screen {Screen.width}x{Screen.height} (dpi {Screen.dpi:0})");
    }

    // 창 모드로 열 창 크기를 모니터 해상도에서 계산한다. 비율은 항상 4:3을 지킨다.
    private static Vector2Int WindowedSize()
    {
        Vector2Int mon = DisplaySize();

        // 1) 모니터 세로의 85%를 기준으로 잡고, 4:3이 되도록 가로를 맞춘다.
        int h = Mathf.RoundToInt(mon.y * WindowedScreenRatio);
        int w = Mathf.RoundToInt(h * (float)ReferenceWidth / ReferenceHeight);

        // 2) 세로로 긴 모니터(예: 세워놓은 모니터)에서는 가로가 먼저 넘친다.
        //    그때는 가로를 기준으로 다시 잡는다.
        int maxW = Mathf.RoundToInt(mon.x * WindowedScreenRatio);
        if (w > maxW)
        {
            w = maxW;
            h = Mathf.RoundToInt(w * (float)ReferenceHeight / ReferenceWidth);
        }

        // 3) 세로를 3의 배수로 내린다. 그래야 가로(= 세로/3*4)가 정수가 되어
        //    4:3이 1픽셀 오차 없이 딱 맞는다. (오차가 있으면 AspectRatioKeeper가
        //    1픽셀짜리 검은 띠를 만들고 그림이 살짝 뭉개진다)
        h = h / 3 * 3;

        // 4) 너무 작으면 최소 크기로 올리고, 그래도 모니터보다 크면 모니터에 맞춘다.
        //    (1366x768처럼 작은 노트북 화면에서 창이 화면 밖으로 나가는 걸 막는다)
        if (h < MinWindowedHeight) h = MinWindowedHeight;
        int capH = mon.y / 3 * 3;
        if (h > capH) h = capH;

        return new Vector2Int(h / 3 * 4, h);
    }

    // 모니터 해상도. Screen.SetResolution에 넘길 값을 계산하는 기준이 된다.
    //
    // ===== 왜 Screen.currentResolution을 먼저 보는가 (macOS Retina 때문) =====
    // Screen.mainWindowDisplayInfo는 모니터의 "물리 픽셀" 수를 준다. 그런데
    // Screen.SetResolution이 받는 값은 유니티의 화면 좌표계 기준이고, 이 둘은 OS에 따라
    // 단위가 다를 수 있다.
    //
    // 특히 맥은 Retina 지원(ProjectSettings의 macRetinaSupport)이 켜져 있어서, 화면 배율이
    // 2배인 모니터에서는 물리 픽셀 수가 유니티가 쓰는 좌표의 2배다. 그래서 물리 픽셀을
    // 기준으로 계산한 값을 SetResolution에 넘기면 창 크기가 2배 또는 1/2배로 어긋난다.
    // 1/2배로 어긋나면 지금 윈도우에서 고친 것과 똑같은 "창이 너무 작다" 증상이 맥에서 난다.
    //
    // Screen.currentResolution은 SetResolution과 같은 좌표계를 쓰므로, 어느 OS에서든
    // 단위가 어긋날 일이 없다. mainWindowDisplayInfo는 이 값을 못 얻을 때만 쓴다.
    //
    // (대신 모니터를 여러 개 쓰고 해상도가 서로 다르면, currentResolution은 게임이 올라가
    //  있는 모니터가 아닌 쪽 해상도를 줄 수 있다. 그래도 창이 모니터보다 커지면 OS가
    //  알아서 화면에 맞게 줄여주므로 창을 못 쓰게 되지는 않는다.)
    private static Vector2Int DisplaySize()
    {
        Resolution res = Screen.currentResolution;
        if (res.width > 0 && res.height > 0)
            return new Vector2Int(res.width, res.height);

        DisplayInfo info = Screen.mainWindowDisplayInfo;
        if (info.width > 0 && info.height > 0)
            return new Vector2Int(info.width, info.height);

        return new Vector2Int(FallbackWidth, FallbackHeight);
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
