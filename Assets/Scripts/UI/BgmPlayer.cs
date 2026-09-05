using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 화면(Title/SaveData/Settings 메뉴 씬들)에서 흘러나오는 배경음악 플레이어.
//
// Title.unity에 이 컴포넌트가 붙은 GameObject를 하나 두면, 최초 생성 시 DontDestroyOnLoad로
// 살아남아서 SaveData/Settings 씬을 오가도 음악이 끊기지 않고 이어진다.
// 실제 게임 씬(SampleScene)으로 넘어가면 메뉴 음악이 어울리지 않으므로 자동으로 정지하고
// 스스로를 파괴한다. 게임 자체의 BGM은 DialogueLine.bgmToPlay를 재생하는 별도 시스템이
// 맡아야 하는데, 그건 아직 구현되어 있지 않다 (TODO).
//
// 볼륨은 SettingsManager.Current.bgmVolume을 매 프레임 읽어와 반영한다. 이벤트 방식이
// 아니라 폴링인 이유는 지금 프로젝트에 오디오 재생 컴포넌트가 이거 하나뿐이라 별도
// 이벤트 시스템을 만들 정도는 아니라고 판단했기 때문. 오디오 소스가 여러 개로 늘어나면
// SettingsManager에 OnBgmVolumeChanged 같은 이벤트를 추가하는 걸 고려할 것.
[RequireComponent(typeof(AudioSource))]
public class BgmPlayer : MonoBehaviour
{
    public static BgmPlayer Instance;

    [Tooltip("Assets/Resources/Sounds/ 아래에 있는 오디오 파일 이름 (확장자 제외)")]
    public string clipResourceName = "title";

    // 이 씬이 로드되면 메뉴 음악을 멈추고 스스로 파괴한다.
    public string stopOnSceneName = "SampleScene";

    private AudioSource source;

    private void Awake()
    {
        if (Instance != null)
        {
            // 이미 재생 중인 BGM 플레이어가 있으면(예: Title 씬을 다시 거쳐온 경우) 새로
            // 생성된 쪽을 없애서 음악이 처음부터 다시 재생되는 걸 막는다.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.clip = Resources.Load<AudioClip>("Sounds/" + clipResourceName);

        if (source.clip == null)
        {
            Debug.LogWarning($"[BgmPlayer] Resources/Sounds/{clipResourceName} 오디오 클립을 찾지 못했습니다.");
            return;
        }

        source.Play();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 배경음악 볼륨 설정을 따르도록 등록한다.
        // (예전에는 Update()에서 매 프레임 source.volume을 덮어쓰고 있었다. 동작은 했지만
        //  1초에 60번씩 필요 없는 계산을 하는 셈이라, 설정이 "바뀔 때만" 반영하는
        //  AudioManager 방식으로 바꿨다. 볼륨을 다루는 곳이 한 군데로 모여서 관리도 쉬워진다.)
        AudioManager.RegisterSafe(source, AudioManager.Channel.Bgm);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == stopOnSceneName)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            source.Stop();
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
