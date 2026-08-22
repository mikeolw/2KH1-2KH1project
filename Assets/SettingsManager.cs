using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    private const string PrefsKey = "GameSettings_v1";

    public GameSettings Current { get; private set; } = new GameSettings();

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

    public void Load()
    {
        Current = PlayerPrefs.HasKey(PrefsKey)
            ? JsonUtility.FromJson<GameSettings>(PlayerPrefs.GetString(PrefsKey))
            : new GameSettings();
        Apply();
    }

    public void SaveAndApply()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
        Apply();
    }

    // BGM/SFX 전용 오디오 소스나 믹서가 아직 없어서, 우선 마스터 볼륨만 실제로 반영한다.
    // 오디오 시스템이 추가되면 여기서 BGM/SFX 소스 volume에 연결하면 된다.
    private void Apply()
    {
        AudioListener.volume = Current.masterVolume;
    }

    public void SetMasterVolume(float v) { Current.masterVolume = v; SaveAndApply(); }
    public void SetSharpness(float v) { Current.sharpness = v; SaveAndApply(); }
    public void SetBgmVolume(float v) { Current.bgmVolume = v; SaveAndApply(); }
    public void SetSfxVolume(float v) { Current.sfxVolume = v; SaveAndApply(); }
}
