using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 볼륨 설정(전체/배경음악/효과음)을 실제 소리에 반영하는 매니저
// =====================================================================================
// ===== 원래 있던 버그 =====
// 환경설정 화면의 BGM/효과음 슬라이더는 값이 저장되기만 하고 소리에는 전혀 영향을 주지
// 않았다. SettingsManager가 masterVolume만 AudioListener.volume에 넣고 있었기 때문이다.
// (AudioListener.volume은 게임의 모든 소리에 한꺼번에 곱해지는 값이라 "전체 볼륨"에는
//  맞지만, BGM만 줄이거나 효과음만 줄이는 데는 쓸 수 없다.)
//
// ===== 어떻게 고쳤나 =====
// 소리를 내는 AudioSource들이 게임 곳곳에 흩어져 있다 (DialogueSystem의 BGM/SFX,
// UIManager의 버튼 효과음, BgmPlayer의 타이틀 음악). 이 매니저에 "나는 BGM이다 / 나는
// 효과음이다"라고 등록해두면, 설정이 바뀔 때마다 각 AudioSource.volume을 알맞게 맞춰준다.
//
//   실제 들리는 크기 = AudioListener.volume(전체) x AudioSource.volume(종류별)
//
// 전체 볼륨은 SettingsManager가 AudioListener.volume에 직접 넣으므로, 여기서는 종류별
// 볼륨만 담당하면 된다.
//
// ===== 효과음은 여기서 바로 낸다 (PlaySfx) =====
// 예전에는 효과음이 필요한 스크립트마다 AudioSource를 하나씩 만들고(UIManager, 핸드폰,
// 조사 오브젝트 마우스 올림...), 그걸 여기에 등록하고, 그림 파일도 각자 Resources.Load 했다.
// 이제 짧은 효과음은 한 줄이면 된다:
//   AudioManager.PlaySfx(AudioManager.Sfx.Click);                 // 파일 경로로
//   AudioManager.PlaySfx(someClip);                               // 인스펙터에 연결한 소리로
//   AudioManager.PlaySfx(AudioManager.Sfx.HotspotHover, 0.15f);   // 0.15초 안에 또 부르면 무시
// 효과음 볼륨 설정은 자동으로 따른다. 소리 파일이 없으면(드라이브에서 아직 안 받음) 경고를 한 번만
// 띄우고 소리 없이 넘어간다.
//
// 배경음악(BGM)과 대사 줄마다 바뀌는 효과음은 "같은 곡이면 끊지 않고 이어 틀기" 같은 규칙이
// 있어서 지금처럼 DialogueSystem/BgmPlayer가 자기 AudioSource로 틀고, 여기에는 등록만 한다.
//
// ===== 씬 배치 =====
// 따로 둘 필요 없다. GameBootstrap이 만들고, 그 전에 누가 부르면 그때 알아서 만든다(Ensure).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    // 자주 쓰는 효과음 파일 경로 (Assets/Resources/ 아래, 확장자 없이)
    public static class Sfx
    {
        public const string Click = "Sounds/SFX/Click_SFX";
        public const string HotspotHover = "Sounds/SFX/HotspotHover_SFX";
    }

    // 소리의 종류. 어떤 볼륨 설정을 따를지 결정한다.
    public enum Channel
    {
        Bgm,  // 배경음악 - bgmVolume 설정을 따른다
        Sfx   // 효과음   - sfxVolume 설정을 따른다
    }

    // 등록된 AudioSource들. 씬이 바뀌면서 파괴된 것(null)이 섞일 수 있으므로
    // 볼륨을 적용할 때마다 걸러낸다.
    private readonly List<AudioSource> bgmSources = new List<AudioSource>();
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();

    // PlaySfx가 쓰는 효과음 전용 AudioSource (PlayOneShot이라 여러 소리가 겹쳐도 된다)
    private AudioSource oneShotSource;
    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;   // 2D 소리 (위치에 따라 작아지지 않게)
        Register(oneShotSource, Channel.Sfx);
    }

    // 없으면 만들어서 돌려준다. Title 씬을 거치지 않고 게임 씬을 바로 실행해도 소리/볼륨이 동작하게.
    public static AudioManager Ensure()
    {
        if (Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
        return Instance;
    }

    // ---------------------------------------------------------------------------------
    // 효과음 재생
    // ---------------------------------------------------------------------------------
    public static void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        Ensure().oneShotSource.PlayOneShot(clip);
    }

    // resourcePath: Assets/Resources/ 아래 경로(확장자 없이). 예: AudioManager.Sfx.Click
    // minInterval: 같은 소리를 이 시간(초) 안에 다시 부르면 무시한다 - 마우스로 빠르게 훑을 때
    //              소리가 연달아 겹쳐 거슬리는 걸 막는다. 0이면 제한 없음.
    public static void PlaySfx(string resourcePath, float minInterval = 0f)
    {
        if (string.IsNullOrEmpty(resourcePath)) return;
        var m = Ensure();

        if (minInterval > 0f)
        {
            if (m.lastPlayedAt.TryGetValue(resourcePath, out float last) && Time.unscaledTime - last < minInterval) return;
            m.lastPlayedAt[resourcePath] = Time.unscaledTime;
        }

        PlaySfx(m.LoadClip(resourcePath));
    }

    // 한 번 읽은 소리는 기억해 둔다. 없는 파일도 "없음"으로 기억해서 경고는 한 번만 띄운다.
    private AudioClip LoadClip(string resourcePath)
    {
        if (clipCache.TryGetValue(resourcePath, out var cached)) return cached;
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
            Debug.LogWarning($"[AudioManager] 효과음 파일이 없어 소리 없이 넘어갑니다: Assets/Resources/{resourcePath} (드라이브에서 받았는지 확인)");
        clipCache[resourcePath] = clip;
        return clip;
    }

    private void OnEnable()
    {
        // 설정이 바뀔 때마다 볼륨을 다시 적용하도록 구독해둔다.
        // SettingsManager가 아직 안 만들어졌을 수도 있으므로(씬 실행 순서에 따라)
        // Start에서 한 번 더 확인한다.
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += ApplyVolumes;
        }
    }

    private void Start()
    {
        // Awake 시점에는 SettingsManager가 없었을 수도 있으므로 여기서 다시 시도한다.
        if (SettingsManager.Instance != null)
        {
            // 중복 구독을 막기 위해 한 번 빼고 다시 넣는다(같은 함수를 두 번 빼도 오류가 아니다).
            SettingsManager.Instance.OnSettingsChanged -= ApplyVolumes;
            SettingsManager.Instance.OnSettingsChanged += ApplyVolumes;
        }
        ApplyVolumes();
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= ApplyVolumes;
        }
    }

    // ---------------------------------------------------------------------------------
    // 등록 / 해제
    // ---------------------------------------------------------------------------------

    // 소리를 내는 쪽에서 자기 AudioSource를 등록한다.
    // 등록하는 즉시 현재 설정값이 적용되므로, 등록 후에 따로 볼륨을 맞출 필요가 없다.
    public void Register(AudioSource source, Channel channel)
    {
        if (source == null) return;

        var list = channel == Channel.Bgm ? bgmSources : sfxSources;
        if (!list.Contains(source)) list.Add(source);

        ApplyTo(source, channel);
    }

    public void Unregister(AudioSource source)
    {
        if (source == null) return;
        bgmSources.Remove(source);
        sfxSources.Remove(source);
    }

    // ---------------------------------------------------------------------------------
    // 볼륨 적용
    // ---------------------------------------------------------------------------------

    // 등록된 모든 AudioSource에 현재 설정값을 적용한다.
    public void ApplyVolumes()
    {
        CleanupDestroyed(bgmSources);
        CleanupDestroyed(sfxSources);

        foreach (var s in bgmSources) ApplyTo(s, Channel.Bgm);
        foreach (var s in sfxSources) ApplyTo(s, Channel.Sfx);
    }

    private void ApplyTo(AudioSource source, Channel channel)
    {
        if (source == null) return;

        float volume = 1f;
        if (SettingsManager.Instance != null)
        {
            var s = SettingsManager.Instance.Current;
            volume = channel == Channel.Bgm ? s.bgmVolume : s.sfxVolume;
        }
        source.volume = Mathf.Clamp01(volume);
    }

    // 씬이 바뀌면서 파괴된 AudioSource(null)를 목록에서 지운다.
    // 유니티에서 파괴된 오브젝트는 == null 비교가 true가 되므로 이렇게 걸러낼 수 있다.
    private void CleanupDestroyed(List<AudioSource> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null) list.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------------------------
    // 편의 함수
    // ---------------------------------------------------------------------------------

    // 아직 AudioManager가 없어도 안전하게 등록할 수 있게 해주는 도우미.
    // (Title 씬을 거치지 않고 SampleScene을 바로 실행하는 테스트 상황 대응)
    // 예전에는 AudioManager가 없으면 지금 볼륨을 한 번만 넣어 두고 끝이라, 그 뒤 환경설정에서
    // 볼륨을 바꿔도 그 소리에는 반영되지 않았다. 이제는 없으면 만들어서 정식으로 등록한다.
    public static void RegisterSafe(AudioSource source, Channel channel)
    {
        if (source == null) return;
        Ensure().Register(source, channel);
    }
}
