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
        // "시작하기"는 슬롯 선택 없이 곧바로 게임 씬으로 이동한다.
        // TODO: 이 경우 SaveManager.Instance.ActiveSave가 null인 채로 게임이 시작된다.
        // 즉 어느 슬롯에도 묶여있지 않은 상태로 진행되므로, 이후 세이브포인트에서
        // 어느 슬롯에 저장할지 정하는 로직이 필요하다 (SaveManager.cs의 ActiveSave 주석 참고).
        startButton.onClick.AddListener(() => SceneManager.LoadScene(gameplaySceneName));

        // "세이브데이터"는 저장된 세이브를 골라 이어하는 화면(SaveData.unity)으로 이동.
        saveDataButton.onClick.AddListener(() => SceneManager.LoadScene(saveDataSceneName));

        // "환경설정"은 별도의 설정 화면(Settings.unity)으로 이동.
        settingsButton.onClick.AddListener(() => SceneManager.LoadScene(settingsSceneName));
    }
}
