using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 세이브데이터 화면(SaveData.unity) 전용 컨트롤러.
//
// 이 화면은 두 가지 용도로 쓸 수 있다:
//   1) 불러오기 모드 (기본) - 타이틀에서 들어왔을 때. 저장된 슬롯을 고르면 그 지점부터 이어한다.
//   2) 저장하기 모드        - 게임 중 저장할 때. 슬롯을 고르면 그 슬롯에 덮어쓴다.
// 어느 모드로 열지는 SaveDataSceneController.OpenMode 정적 필드로 정한다
// (씬을 넘어가며 값을 전달해야 해서 static을 썼다. 씬이 바뀌어도 값이 유지된다).
//
// ===== 저장은 세이브포인트에서만 =====
// 시나리오 문서에 {세이브포인트}라고 표시된 지점을 지나야만 저장할 수 있다.
// 아직 하나도 지나지 않았다면 저장하기 모드로 들어와도 슬롯이 전부 잠긴다
// (SavePointManager.CanSave 참고). 저장되는 것은 "지금 이 순간"이 아니라
// "마지막으로 지나온 세이브포인트"이므로, 불러오면 항상 안전한 지점에서 다시 시작한다.
public class SaveDataSceneController : MonoBehaviour
{
    // 이 화면이 어떤 용도로 열렸는지.
    public enum Mode
    {
        Load,  // 불러오기 (타이틀에서 진입)
        Save   // 저장하기 (게임 중 진입)
    }

    // 다음에 이 씬을 열 때 쓸 모드. 씬을 이동하며 값을 넘겨야 해서 static으로 둔다.
    // 기본값은 불러오기 - 타이틀에서 그냥 들어오면 지금까지와 똑같이 동작한다.
    public static Mode OpenMode = Mode.Load;

    [Header("세이브 슬롯 (3개, SaveManager.SlotCount와 맞출 것)")]
    public Button[] slotButtons = new Button[3];
    public TMP_Text[] slotLabels = new TMP_Text[3];
    public Button backButton;

    [Header("화면 상단 안내 문구 (없어도 동작함)")]
    public TMP_Text headerText;

    [Header("이동할 씬 이름")]
    public string gameplaySceneName = "SampleScene";
    public string titleSceneName = "Title";

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(OnClickBack);

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;

            int slotIndex = i; // 람다가 반복 변수를 그대로 캡처하면 전부 마지막 i값을 참조하게
                               // 되는 클로저 함정이 있어서, 로컬 변수로 복사해 캡처한다.
            slotButtons[i].onClick.AddListener(() => OnClickSlot(slotIndex));
        }
    }

    private void Start()
    {
        RefreshSlots();
    }

    // 슬롯 3개의 표시 내용과 누를 수 있는지 여부를 갱신한다.
    private void RefreshSlots()
    {
        // Title 씬을 거치지 않고 이 씬을 바로 실행한 경우에 대한 방어.
        // (예전에는 여기서 NullReferenceException이 났다)
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[SaveDataSceneController] SaveManager가 없습니다. Title 씬부터 실행해 주세요.");
            foreach (var b in slotButtons) { if (b != null) b.interactable = false; }
            if (headerText != null) headerText.text = "세이브 시스템을 불러올 수 없습니다. 타이틀부터 실행해 주세요.";
            return;
        }

        bool saveMode = OpenMode == Mode.Save;

        // 저장하기 모드인데 아직 세이브포인트를 지나지 않았다면 저장할 수 없다.
        bool canSave = SavePointManager.Instance != null && SavePointManager.Instance.CanSave;

        if (headerText != null)
        {
            if (!saveMode)
                headerText.text = "불러올 슬롯을 고르세요.";
            else if (canSave)
                headerText.text = $"저장할 슬롯을 고르세요.  (저장 지점: {SavePointManager.Instance.LastSavePointId})";
            else
                headerText.text = "아직 저장할 수 있는 지점을 지나지 않았습니다.";
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;

            SaveData data = SaveManager.Instance.Load(i);
            bool hasSave = data != null;

            if (slotLabels[i] != null)
            {
                if (hasSave)
                {
                    // 진행 지점 / 저장 시각 / 플레이 시간
                    string playTime = SavePointManager.FormatPlayTime(data.playTimeSeconds);
                    slotLabels[i].text = $"{data.chapterId}\n{data.timestamp}\n플레이 시간 {playTime}";
                }
                else
                {
                    // 저장하기 모드에서는 빈 슬롯도 골라야 하므로 "새로 저장"이라고 안내한다.
                    slotLabels[i].text = saveMode ? "빈 슬롯\n(여기에 저장)" : "빈 슬롯";
                }
            }

            // 불러오기 모드: 세이브가 있는 슬롯만 고를 수 있다.
            // 저장하기 모드: 세이브포인트를 지났다면 빈 슬롯도 포함해 전부 고를 수 있다(덮어쓰기).
            slotButtons[i].interactable = saveMode ? canSave : hasSave;
        }
    }

    // 슬롯을 클릭했을 때.
    private void OnClickSlot(int slotIndex)
    {
        if (SaveManager.Instance == null) return;

        if (OpenMode == Mode.Save)
        {
            SaveToSlot(slotIndex);
            return;
        }

        LoadFromSlot(slotIndex);
    }

    // ---------------------------------------------------------------------------------
    // 저장하기
    // ---------------------------------------------------------------------------------
    private void SaveToSlot(int slotIndex)
    {
        if (SavePointManager.Instance == null)
        {
            Debug.LogWarning("[SaveDataSceneController] SavePointManager가 없어 저장할 수 없습니다.");
            return;
        }

        bool ok = SavePointManager.Instance.SaveToSlot(slotIndex);
        if (!ok)
        {
            if (headerText != null) headerText.text = "아직 저장할 수 있는 지점을 지나지 않았습니다.";
            return;
        }

        // 저장 후 목록을 갱신해 방금 저장된 내용이 바로 보이게 한다.
        RefreshSlots();
        if (headerText != null) headerText.text = $"슬롯 {slotIndex + 1}에 저장했습니다.";
    }

    // ---------------------------------------------------------------------------------
    // 불러오기
    // ---------------------------------------------------------------------------------
    private void LoadFromSlot(int slotIndex)
    {
        var data = SaveManager.Instance.Load(slotIndex);
        if (data == null) return;

        // "지금 이어하는 세이브"로 지정한다. 게임 씬이 시작될 때 GameBootstrap이 이 값을
        // 보고 저장된 지점부터 대사를 이어서 재생한다 (GameBootstrap.cs 참고).
        SaveManager.Instance.SetActiveSave(data);

        SceneManager.LoadScene(string.IsNullOrEmpty(data.sceneId) ? gameplaySceneName : data.sceneId);
    }

    // ---------------------------------------------------------------------------------
    // 뒤로가기
    // ---------------------------------------------------------------------------------
    private void OnClickBack()
    {
        // 저장하기 모드로 게임 중에 들어온 경우엔 타이틀이 아니라 게임 씬으로 돌아가야 한다.
        if (OpenMode == Mode.Save)
        {
            OpenMode = Mode.Load; // 다음 진입을 위해 기본값으로 되돌린다
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }

    // 게임 중 "저장하기"를 누를 때 다른 스크립트에서 호출한다.
    // 예: 퀵바에 저장 버튼을 만들고 이 함수를 연결.
    public static void OpenForSaving(string saveSceneName = "SaveData")
    {
        OpenMode = Mode.Save;
        SceneManager.LoadScene(saveSceneName);
    }
}
