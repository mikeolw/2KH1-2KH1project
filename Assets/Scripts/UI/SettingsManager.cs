using UnityEngine;

// 환경설정 값을 PlayerPrefs에 저장/로드하는 매니저.
//
// SaveManager와 마찬가지로 Title.unity에서 생성되어 DontDestroyOnLoad로 씬 전환 간 유지되며,
// 다른 스크립트는 SettingsManager.Instance로 접근한다.
//
// 세이브 슬롯(SaveData)과 다르게 슬롯 구분이 없는 "기기당 설정값 1벌"이라서 JSON 파일이
// 아니라 PlayerPrefs(문자열 1개)로 저장한다. Settings.unity의 SettingsPanelController가
// 슬라이더 값이 바뀔 때마다 이 매니저의 SetXxx() 메서드를 호출해서 즉시 저장 + 적용한다.
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    // PlayerPrefs에 저장할 때 쓰는 키. 필드 구조를 크게 바꾸면 버전을 올려서(_v2 등)
    // 기존 유저의 예전 형식 데이터와 충돌하지 않게 하는 걸 권장.
    private const string PrefsKey = "GameSettings_v1";

    // 창 모드 해상도. 레퍼런스 해상도 1440x1080(4:3)에서 너비를 1080으로 줄인 배율(0.75)을
    // 그대로 높이에 적용: 1080 * 0.75 = 810.
    private const int WindowedWidth = 1080;
    private const int WindowedHeight = 810;

    // 창 모드가 아닐 때(기본값) 되돌아갈 해상도/모드. ProjectSettings.asset의
    // defaultScreenWidth/Height(1920x1080)와 fullscreenMode(1 = FullScreenWindow)를 그대로 따름.
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;

    // 현재 적용 중인 설정값. UI(슬라이더)는 이 값을 읽어서 초기 상태를 표시한다.
    public GameSettings Current { get; private set; } = new GameSettings();

    // Settings.unity의 뒤로가기 버튼이 돌아갈 씬 이름. 기본값은 Title에서 진입한 경우를
    // 위한 "Title". SampleScene 등 인게임에서 설정 화면을 열 때는 진입 직전에 이 값을
    // 그 씬 이름으로 바꿔준다 (UIManager.OpenSettingsScene 참고).
    public string ReturnSceneName = "Title";

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

    // PlayerPrefs에서 설정값을 읽어온다. 저장된 값이 없으면(최초 실행) 기본값(GameSettings의
    // 필드 기본값)을 사용한다. 읽어온 뒤 바로 Apply()해서 마스터 볼륨 등이 즉시 반영되게 한다.
    public void Load()
    {
        Current = PlayerPrefs.HasKey(PrefsKey)
            ? JsonUtility.FromJson<GameSettings>(PlayerPrefs.GetString(PrefsKey))
            : new GameSettings();
        Apply();
    }

    // 현재 값을 PlayerPrefs에 저장하고 즉시 적용한다. SetXxx() 메서드들이 값 변경 후
    // 항상 이 메서드를 호출하므로, 슬라이더를 움직이는 즉시 저장까지 이뤄진다
    // (별도의 "적용" 버튼이 없는 구조).
    public void SaveAndApply()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
        Apply();
    }

    // 실제 게임에 설정값을 반영하는 부분.
    // TODO: masterVolume -> AudioListener.volume, windowed -> Screen.SetResolution만 연결되어 있다.
    // bgmVolume/sfxVolume은 저장은 되지만 적용할 대상(BGM/SFX 전용 AudioSource나
    // AudioMixer)이 프로젝트에 아직 없어서 소리에 영향을 주지 않는다.
    // 오디오 시스템이 추가되면 이 메서드 안에서 각 값을 연결해줄 것.
    private void Apply()
    {
        AudioListener.volume = Current.masterVolume;

        if (Current.windowed)
            Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
        else
            Screen.SetResolution(DefaultWidth, DefaultHeight, FullScreenMode.FullScreenWindow);
    }

    // 아래 4개는 SettingsPanelController의 슬라이더/토글 onValueChanged에 직접 연결되는
    // 콜백들이다. 값이 바뀔 때마다 해당 값만 바꾸고 바로 SaveAndApply()를 호출한다.
    public void SetMasterVolume(float v) { Current.masterVolume = v; SaveAndApply(); }
    public void SetBgmVolume(float v) { Current.bgmVolume = v; SaveAndApply(); }
    public void SetSfxVolume(float v) { Current.sfxVolume = v; SaveAndApply(); }
    public void SetWindowed(bool v) { Current.windowed = v; SaveAndApply(); }
}
