using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 세이브데이터 화면(SaveData.unity) 전용 컨트롤러.
//
// 타이틀의 "세이브데이터" 버튼을 누르면 이 씬으로 이동해온다. 슬롯 3개를 보여주고,
// 저장된 슬롯을 고르면 그 세이브를 SaveManager에 활성화(SetActiveSave)한 뒤 게임 씬으로
// 이동한다. 빈 슬롯은 이 화면에서 고를 수 없다 (조회/이어하기 전용 화면이라서 - 새 게임은
// 타이틀의 "시작하기" 버튼으로 시작하고 이 화면을 거치지 않는다).
//
// TODO(미비점):
//  - 슬롯 삭제 기능이 없다. SaveManager.DeleteSlot()은 이미 구현되어 있으니, 슬롯 버튼에
//    길게 누르기/우클릭 등으로 삭제 확인 UI를 붙이면 된다.
//  - 슬롯을 눌러도 되돌릴 수 없다(확인 창 없이 바로 로드). 오조작 방지용 확인 팝업을
//    추가하면 좋을 것 같다.
//  - SaveManager.Instance가 없는 상태(=Title 씬을 거치지 않고 이 씬에 바로 진입한 경우)에
//    대한 방어 코드가 없다. 테스트 목적으로 이 씬에서 바로 플레이를 시작하면 NullReferenceException이
//    난다 — 반드시 Title 씬부터 플레이할 것.
public class SaveDataSceneController : MonoBehaviour
{
    [Header("세이브 슬롯 (3개, SaveManager.SlotCount와 맞출 것)")]
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
            int slotIndex = i; // 람다가 반복 변수를 그대로 캡처하면 전부 마지막 i값을 참조하게
                                // 되는 클로저 함정이 있어서, 로컬 변수로 복사해 캡처한다.
            slotButtons[i].onClick.AddListener(() => OnClickSlot(slotIndex));
        }
    }

    private void Start()
    {
        RefreshSlots();
    }

    // 슬롯 3개를 각각 SaveManager에서 읽어와 라벨을 갱신하고, 세이브가 없는 슬롯은
    // 버튼을 비활성화(interactable = false)해서 고를 수 없게 만든다.
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

    // 슬롯을 클릭했을 때: 해당 세이브를 불러와 "지금 이어하는 세이브"로 지정하고 게임 씬으로 이동.
    // interactable=false인 빈 슬롯은 애초에 클릭 이벤트가 발생하지 않지만, 혹시 모를 상황을
    // 대비해 data == null이면 아무 것도 하지 않고 리턴하는 방어 코드를 넣어뒀다.
    private void OnClickSlot(int slotIndex)
    {
        var data = SaveManager.Instance.Load(slotIndex);
        if (data == null) return;

        SaveManager.Instance.SetActiveSave(data);
        SceneManager.LoadScene(gameplaySceneName);
    }
}
