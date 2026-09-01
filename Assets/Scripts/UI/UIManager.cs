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

    // 환경설정은 인게임 팝업이 아니라 별도로 만들어둔 Settings.unity 씬으로 이동한다
    // (Title 화면과 동일한 환경설정 화면을 재사용). 뒤로가기를 누르면 이 씬(SampleScene)으로
    // 돌아와야 하므로, 씬 전환 직전에 SettingsManager에 복귀할 씬 이름을 알려준다.
    public void OpenSettingsScene()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ReturnSceneName = SceneManager.GetActiveScene().name;
        }
        SceneManager.LoadScene("Settings");
    }

    // 조사기록/인벤토리/사진첩/핸드폰/설정 팝업 중 하나라도 열려있는지 여부.
    // DialogueSystem이 대사 넘기기 클릭을 처리하기 전에 이 값을 확인해서, 모달 바깥을
    // 클릭했을 때 그 클릭이 뒤에 있는 대사창 등 다른 스크립트로 새어나가지 않게 막는다.
    public bool IsAnyPanelOpen =>
        (inventoryPanel && inventoryPanel.activeSelf) ||
        (photoPanel && photoPanel.activeSelf) ||
        (phonePanel && phonePanel.activeSelf) ||
        (notePanel && notePanel.activeSelf) ||
        (settingsPanel && settingsPanel.activeSelf);

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