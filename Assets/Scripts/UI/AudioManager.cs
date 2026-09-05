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
// ===== 씬 배치 =====
// Title.unity에 빈 GameObject를 만들고 이 스크립트를 붙여두면 된다(DontDestroyOnLoad).
// 등록은 각 스크립트가 알아서 하므로 인스펙터에서 연결할 것은 없다.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

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
    public static void RegisterSafe(AudioSource source, Channel channel)
    {
        if (Instance != null)
        {
            Instance.Register(source, channel);
            return;
        }

        // AudioManager가 없으면 설정값만 직접 한 번 적용해준다.
        if (source != null && SettingsManager.Instance != null)
        {
            var s = SettingsManager.Instance.Current;
            source.volume = channel == Channel.Bgm ? s.bgmVolume : s.sfxVolume;
        }
    }
}
