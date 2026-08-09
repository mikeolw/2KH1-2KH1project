using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("글로벌 상단 UI")]
    public TMP_Text timeStampText; // 예: "2002/08/08 13:00"

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