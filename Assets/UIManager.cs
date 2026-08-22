using UnityEngine;
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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
    public void TogglePhone() => TogglePanel(phonePanel);
    public void ToggleNote() => TogglePanel(notePanel);
    public void ToggleSettings() => TogglePanel(settingsPanel);

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