using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// SaveData.unity 전용: 세이브 슬롯 3개를 보여주고, 저장된 슬롯을 고르면 그 세이브를
// SaveManager에 활성화한 뒤 게임 씬으로 이동한다. 빈 슬롯은 고를 수 없다(조회/이어하기 전용).
public class SaveDataSceneController : MonoBehaviour
{
    [Header("세이브 슬롯 (3개)")]
    public Button[] slotButtons = new Button[3];
    public TMP_Text[] slotLabels = new TMP_Text[3];
    public Button backButton;

    [Header("이동할 씬 이름")]
    public string gameplaySceneName = "SampleScene";
    public string titleSceneName = "Title";

    private void Awake()
    {
        backButton.onClick.AddListener(() => SceneManager.LoadScene(titleSceneName));

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i; // 클로저 캡처용 로컬 변수
            slotButtons[i].onClick.AddListener(() => OnClickSlot(slotIndex));
        }
    }

    private void Start()
    {
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            SaveData data = SaveManager.Instance.Load(i);
            bool hasSave = data != null;
            slotLabels[i].text = hasSave ? $"{data.chapterId}\n{data.timestamp}" : "빈 슬롯";
            slotButtons[i].interactable = hasSave;
        }
    }

    private void OnClickSlot(int slotIndex)
    {
        var data = SaveManager.Instance.Load(slotIndex);
        if (data == null) return;

        SaveManager.Instance.SetActiveSave(data);
        SceneManager.LoadScene(gameplaySceneName);
    }
}
