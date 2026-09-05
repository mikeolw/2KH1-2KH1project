using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("글로벌 상단 UI")]
    public TMP_Text timeStampText; // 예: "2002/08/08 13:00"

    // 이 필드들은 QuickBarPanel의 버튼(Btn_Inventory 등)이 아니라, 그 버튼을 눌렀을 때
    // 열리는 실제 팝업 콘텐츠(아이템 리스트, 사진첩 등)를 가리켜야 한다.
    // 버튼은 항상 화면에 떠있는 QuickBarPanel 쪽에 있고, 여기 연결된 패널은 평소엔 꺼져
    // 있다가 토글로만 켜지는 콘텐츠 화면이다. 예전엔 버튼이 이 패널들 안에 잘못 들어가
    // 있었어서 (QuickBarPanel로 분리하며 수정됨), 헷갈리지 않도록 남겨두는 주석.
    [Header("팝업 UI 패널들")]
    public GameObject inventoryPanel;   // 가방 UI
    public GameObject photoPanel;       // 사진 앨범 UI
    public GameObject phonePanel;       // 핸드폰 UI
    public GameObject notePanel;        // 조사기록 메모장 UI
    public GameObject settingsPanel;    // 설정 UI

    [Header("효과음")]
    public AudioClip noteOpenSfx;       // 노트(조사기록) 패널을 열 때 재생되는 효과음 (Sounds/SFX/ 폴더 참고)
    public AudioClip phoneOpenSfx;      // 핸드폰 패널을 열 때 재생되는 효과음
    private AudioSource sfxSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // 환경설정의 효과음 볼륨이 UI 버튼 소리에도 적용되도록 등록한다 (AudioManager.cs 참고).
        AudioManager.RegisterSafe(sfxSource, AudioManager.Channel.Sfx);
    }

    void Start()
    {
        // 시작할 때 모든 팝업 닫기
        CloseAllPanels();
        SetTimeStamp("2002/08/08 13:00");
    }

    public void SetTimeStamp(string timeStr)
    {
        if (timeStampText != null) timeStampText.text = timeStr;
    }

    // 하단 퀵바 버튼에서 호출할 함수들
    public void ToggleInventory() => TogglePanel(inventoryPanel);
    public void TogglePhoto() => TogglePanel(photoPanel);
    public void TogglePhone()
    {
        bool willOpen = !phonePanel.activeSelf;
        TogglePanel(phonePanel);
        if (willOpen && phoneOpenSfx != null) sfxSource.PlayOneShot(phoneOpenSfx);
    }
    public void ToggleNote()
    {
        bool willOpen = !notePanel.activeSelf;
        TogglePanel(notePanel);
        if (willOpen && noteOpenSfx != null) sfxSource.PlayOneShot(noteOpenSfx);
    }
    public void ToggleSettings() => TogglePanel(settingsPanel);

    // ===== 인게임 환경설정 =====
    // 예전에는 Settings.unity 씬을 게임 화면 위에 "겹쳐 띄우는(additive)" 방식이었다.
    // 그런데 이 방식에서 문제가 반복해서 나왔다:
    //   - 뒤로가기를 눌러도 씬이 내려가기까지 몇 프레임이 걸려, 그 사이 옵션 창이
    //     게임 화면 뒤에 깔린 것처럼 다시 보였다.
    //   - 두 씬의 캔버스가 서로 다른 기준으로 그려져 앞뒤 순서가 뒤엉켰다.
    //   - 씬마다 EventSystem이 하나씩 있어 입력이 충돌했다.
    //
    // 그래서 씬을 오가는 방식을 버리고, 이 게임 씬 안에서 패널 하나를 켜고 끄는 방식으로
    // 바꿨다. 씬 전환이 아예 없으므로 위 문제가 구조적으로 생길 수 없고, 진행 중이던
    // 대사 상태도 당연히 그대로 유지된다. (SettingsPanelUI.cs 참고)
    public void OpenSettingsScene()
    {
        // Title 씬을 거치지 않고 게임 씬을 바로 실행한 경우를 대비해 설정 매니저를 확보한다.
        if (SettingsManager.Instance == null)
        {
            new GameObject("SettingsManager").AddComponent<SettingsManager>();
        }

        // 설정 패널이 아직 없으면 만든다(GameBootstrap이 미리 만들어두지만 안전장치).
        if (SettingsPanelUI.Instance == null)
        {
            new GameObject("SettingsPanel").AddComponent<SettingsPanelUI>();
        }

        // 다른 팝업(가방/수첩 등)이 열려 있으면 닫고 설정만 보여준다.
        CloseAllPanels();
        SettingsPanelUI.Instance.Open();
    }

    // 조사기록/인벤토리/사진첩/핸드폰/설정 팝업 중 하나라도 열려있는지 여부.
    // DialogueSystem이 대사 넘기기 클릭을 처리하기 전에 이 값을 확인해서, 모달 바깥을
    // 클릭했을 때 그 클릭이 뒤에 있는 대사창 등 다른 스크립트로 새어나가지 않게 막는다.
    public bool IsAnyPanelOpen =>
        (inventoryPanel && inventoryPanel.activeSelf) ||
        (photoPanel && photoPanel.activeSelf) ||
        (phonePanel && phonePanel.activeSelf) ||
        (notePanel && notePanel.activeSelf) ||
        (settingsPanel && settingsPanel.activeSelf) ||
        (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsOpen);

    private void TogglePanel(GameObject targetPanel)
    {
        bool isActive = targetPanel.activeSelf;
        CloseAllPanels();
        targetPanel.SetActive(!isActive);
    }

    public void CloseAllPanels()
    {
        if (inventoryPanel) inventoryPanel.SetActive(false);
        if (photoPanel) photoPanel.SetActive(false);
        if (phonePanel) phonePanel.SetActive(false);
        if (notePanel) notePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }
}