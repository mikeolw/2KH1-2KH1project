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

    // 환경설정은 인게임 팝업이 아니라 별도로 만들어둔 Settings.unity 씬을 "덮어씌우는" 형태로
    // additive 로드한다 (Title 화면과 동일한 환경설정 화면을 재사용). Single 모드로 씬을
    // 통째로 바꿔버리면 되돌아올 때 이 씬(SampleScene)이 처음부터 다시 로드되면서 진행 중이던
    // 대사/시나리오 진행 상태가 전부 초기화돼버리므로, 기존 씬은 그대로 살려두고 그 위에
    // Settings 씬만 겹쳐 띄운 뒤 뒤로가기를 누르면 그 씬만 언로드한다
    // (SettingsPanelController의 backButton 핸들러 참고).
    public void OpenSettingsScene()
    {
        // 정상적으로는 Title 씬을 거쳐야 SettingsManager가 생성되어 있지만, 에디터에서
        // SampleScene을 바로 Play해서 테스트하는 경우 등 Title을 거치지 않은 경우엔
        // Instance가 아직 없을 수 있다. 이때 그냥 넘어가면 ReturnSceneName이 기본값인
        // "Title"로 남아서 뒤로가기를 눌렀을 때 이 씬이 아니라 타이틀로 튕기는 버그가
        // 생기므로, 없으면 여기서 만들어준다.
        if (SettingsManager.Instance == null)
        {
            new GameObject("SettingsManager").AddComponent<SettingsManager>();
        }
        SettingsManager.Instance.ReturnSceneName = SceneManager.GetActiveScene().name;
        SettingsManager.Instance.ReturnAdditive = true;
        SceneManager.LoadScene("Settings", LoadSceneMode.Additive);
    }

    // 조사기록/인벤토리/사진첩/핸드폰/설정 팝업 중 하나라도 열려있는지 여부.
    // DialogueSystem이 대사 넘기기 클릭을 처리하기 전에 이 값을 확인해서, 모달 바깥을
    // 클릭했을 때 그 클릭이 뒤에 있는 대사창 등 다른 스크립트로 새어나가지 않게 막는다.
    // 설정 화면은 이제 별도 씬(Additive)으로 겹쳐 뜨기 때문에 settingsPanel GameObject로는
    // 감지가 안 되고, 대신 SettingsPanelController.IsOpen 정적 플래그로 확인한다.
    public bool IsAnyPanelOpen =>
        (inventoryPanel && inventoryPanel.activeSelf) ||
        (photoPanel && photoPanel.activeSelf) ||
        (phonePanel && phonePanel.activeSelf) ||
        (notePanel && notePanel.activeSelf) ||
        (settingsPanel && settingsPanel.activeSelf) ||
        SettingsPanelController.IsOpen;

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