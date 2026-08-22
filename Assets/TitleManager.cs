using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 타이틀 화면은 버튼 3개로 각각 별도의 씬(페이지)으로 이동만 시킨다.
// 세이브 슬롯 UI와 환경설정 UI는 더 이상 이 씬 안에 패널로 존재하지 않고
// SaveData.unity / Settings.unity 씬에 따로 있다.
public class TitleManager : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    public Button startButton;
    public Button saveDataButton;
    public Button settingsButton;

    [Header("이동할 씬 이름")]
    public string gameplaySceneName = "SampleScene";
    public string saveDataSceneName = "SaveData";
    public string settingsSceneName = "Settings";

    private void Awake()
    {
        startButton.onClick.AddListener(() => SceneManager.LoadScene(gameplaySceneName));
        saveDataButton.onClick.AddListener(() => SceneManager.LoadScene(saveDataSceneName));
        settingsButton.onClick.AddListener(() => SceneManager.LoadScene(settingsSceneName));
    }
}
