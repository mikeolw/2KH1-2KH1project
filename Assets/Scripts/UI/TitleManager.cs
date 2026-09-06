using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 타이틀 화면(Title.unity) 전용 컨트롤러.
//
// 이 스크립트가 하는 일은 딱 하나, 버튼 3개를 각각 다른 씬으로 보내는 것뿐이다.
// 세이브 슬롯 UI는 SaveData.unity + SaveDataSceneController, 환경설정 UI는
// Settings.unity + SettingsPanelController가 따로 담당한다 (원래는 이 셋을 전부
// Title.unity 안에 패널로 넣었었지만, "각 버튼이 완전히 별도의 페이지로 이동해야 한다"는
// 요청에 따라 씬 단위로 분리했다).
//
// Title.unity는 SaveManager/SettingsManager를 생성해서 DontDestroyOnLoad로 유지시키는
// "부트스트랩 씬" 역할도 겸한다. 그래서 게임을 처음 실행하면 반드시 Title 씬을 거쳐야
// 저 두 매니저가 만들어진다 (Build Settings에서도 Title이 0번 씬으로 등록되어 있음).
public class TitleManager : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    public Button startButton;     // 시작하기
    public Button saveDataButton;  // 세이브데이터
    public Button settingsButton;  // 환경설정

    [Header("이동할 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    public string gameplaySceneName = "SampleScene";
    public string saveDataSceneName = "SaveData";
    public string settingsSceneName = "Settings";

    private void Awake()
    {
        // "시작하기"는 슬롯 선택 없이 곧바로 게임 씬으로 이동해 새 게임을 시작한다.
        // 이전에 이어하던 세이브가 남아 있으면 그 지점부터 시작해 버리므로 여기서 비워준다
        // (DialogueSystem.Start()가 SaveManager.ActiveSave를 보고 이어할지 판단한다).
        startButton.onClick.AddListener(() =>
        {
            if (SaveManager.Instance != null) SaveManager.Instance.SetActiveSave(null);
            SceneManager.LoadScene(gameplaySceneName);
        });

        // "세이브데이터"는 저장된 세이브를 골라 이어하는 화면(SaveData.unity)으로 이동.
        saveDataButton.onClick.AddListener(() =>
        {
            SaveDataSceneController.OpenMode = SaveDataSceneController.Mode.Load;
            SceneManager.LoadScene(saveDataSceneName);
        });

        // "환경설정"은 별도의 설정 화면(Settings.unity)으로 이동한다.
        // 그 씬은 SettingsPanelController가 통합 설정 화면(SettingsPanelUI)을 띄워준다.
        settingsButton.onClick.AddListener(() =>
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ReturnSceneName = SceneManager.GetActiveScene().name;
            }
            SceneManager.LoadScene(settingsSceneName);
        });
    }
}
