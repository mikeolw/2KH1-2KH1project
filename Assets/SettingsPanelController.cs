using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsPanelController : MonoBehaviour
{
    [Header("탭 버튼")]
    public Button tabVolumeButton;
    public Button tabDisplayButton;
    public Button tabKeypadButton;
    public Button tabListButton;

    [Header("탭 콘텐츠")]
    public GameObject volumeContent;
    public GameObject displayContent;
    public GameObject keypadContent;
    public GameObject listContent;

    [Header("볼륨 탭 슬라이더")]
    public Slider masterVolumeSlider;
    public Slider sharpnessSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("공용 버튼")]
    public Button quitButton;
    public Button backButton;

    public System.Action onBack;

    private void Awake()
    {
        tabVolumeButton.onClick.AddListener(() => ShowTab(volumeContent));
        tabDisplayButton.onClick.AddListener(() => ShowTab(displayContent));
        tabKeypadButton.onClick.AddListener(() => ShowTab(keypadContent));
        tabListButton.onClick.AddListener(() => ShowTab(listContent));

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

    private void OnEnable()
    {
        if (SettingsManager.Instance == null) return;

        var s = SettingsManager.Instance.Current;
        masterVolumeSlider.SetValueWithoutNotify(s.masterVolume);
        sharpnessSlider.SetValueWithoutNotify(s.sharpness);
        bgmVolumeSlider.SetValueWithoutNotify(s.bgmVolume);
        sfxVolumeSlider.SetValueWithoutNotify(s.sfxVolume);

        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        sharpnessSlider.onValueChanged.RemoveAllListeners();
        bgmVolumeSlider.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider.onValueChanged.RemoveAllListeners();

        masterVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetMasterVolume);
        sharpnessSlider.onValueChanged.AddListener(SettingsManager.Instance.SetSharpness);
        bgmVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetBgmVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetSfxVolume);

        ShowTab(volumeContent);
    }

    private void ShowTab(GameObject target)
    {
        volumeContent.SetActive(target == volumeContent);
        displayContent.SetActive(target == displayContent);
        keypadContent.SetActive(target == keypadContent);
        listContent.SetActive(target == listContent);
    }
}
