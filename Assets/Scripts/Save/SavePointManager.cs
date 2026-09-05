using UnityEngine;

// =====================================================================================
// 세이브포인트 관리 - "지정된 지점에서만 저장할 수 있게" 하는 규칙 담당
// =====================================================================================
// ===== 왜 아무 데서나 저장하면 안 되는가? =====
// 이 게임은 대사 진행 중간에 조사 화면, 미니게임, 선택지 같은 특수 상태가 자주 끼어든다.
// 그런 상태 한가운데서 저장해버리면 "조사 화면이 반쯤 열린 상태" 같은 걸 정확히 복원해야 해서
// 매우 복잡해지고 버그도 많아진다. 그래서 시나리오 문서에 {세이브포인트}라고 표시된
// 안전한 지점들만 저장 지점으로 삼는다.
//
// ===== 동작 방식 =====
//   1) CSV의 IsSavePoint=TRUE인 줄을 지나가면 DialogueSystem이 ReachSavePoint()를 부른다.
//   2) 이 매니저는 "마지막으로 지나온 세이브포인트가 어디였는지"만 기억해둔다.
//   3) 플레이어가 저장하기를 누르면, 지금 있는 위치가 아니라 "마지막으로 지나온 세이브포인트"를
//      저장한다. 그래서 나중에 불러오면 항상 안전한 지점에서 다시 시작한다.
//   4) 세이브포인트를 아직 하나도 지나지 않았다면(게임 시작 직후) 저장할 수 없다(CanSave = false).
//
// 이 방식의 장점: 플레이어는 아무 때나 저장 버튼을 눌러도 되고(막힌 느낌이 덜함),
// 복원은 항상 검증된 지점에서만 이뤄진다.
//
// ===== 씬 배치 =====
// SaveManager와 마찬가지로 Title.unity에 빈 GameObject를 만들고 이 스크립트를 붙여두면 된다.
// (DontDestroyOnLoad라서 씬이 바뀌어도 살아남는다.)
public class SavePointManager : MonoBehaviour
{
    public static SavePointManager Instance;

    // 마지막으로 지나온 세이브포인트의 정보.
    // scenarioCsv : 그때 진행 중이던 CSV 파일 이름 (예: scenario_01)
    // lineIndex   : 그 CSV의 몇 번째 줄이었는지 (여기부터 이어서 재생하면 된다)
    // savePointId : 세이브 슬롯 목록에 보여줄 사람이 읽을 수 있는 이름 (예: "#01 사무실")
    public string LastScenarioCsv { get; private set; }
    public int LastLineIndex { get; private set; }
    public string LastSavePointId { get; private set; }

    // 세이브포인트를 한 번이라도 지났는지. 저장 버튼을 켜고 끌 때 이 값을 본다.
    public bool CanSave { get; private set; }

    // 세이브포인트에 도달했을 때 발생하는 이벤트. UI가 구독해두면 "저장 가능" 표시를
    // 켜거나 "체크포인트 통과" 안내를 잠깐 띄우는 데 쓸 수 있다.
    public event System.Action<string> OnSavePointReached;

    // 게임 시작 시점부터 누적된 플레이 시간(초). 세이브 슬롯 목록에 표시할 용도.
    private float playTimeSeconds;

    private void Awake()
    {
        // SaveManager/SettingsManager와 동일한 싱글톤 + DontDestroyOnLoad 패턴.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // 플레이 시간을 계속 누적한다. Time.deltaTime은 "지난 프레임에서 지금까지 걸린 시간(초)".
        playTimeSeconds += Time.deltaTime;
    }

    // ---------------------------------------------------------------------------------
    // 세이브포인트 도달
    // ---------------------------------------------------------------------------------

    // DialogueSystem이 IsSavePoint=TRUE인 줄을 표시할 때 호출한다.
    //   savePointId : CSV의 SavePointId 칸 (비어 있으면 CSV 파일 이름을 대신 쓴다)
    //   scenarioCsv : 지금 진행 중인 CSV 파일 이름
    //   lineIndex   : 지금 몇 번째 줄인지 (DialogueSystem의 lineIndex를 그대로 받는다)
    public void ReachSavePoint(string savePointId, string scenarioCsv, int lineIndex)
    {
        LastScenarioCsv = scenarioCsv;
        LastLineIndex = lineIndex;
        LastSavePointId = string.IsNullOrWhiteSpace(savePointId) ? scenarioCsv : savePointId.Trim();
        CanSave = true;

        Debug.Log($"[SavePointManager] 세이브포인트 도달: {LastSavePointId} ({scenarioCsv} {lineIndex}번째 줄)");
        OnSavePointReached?.Invoke(LastSavePointId);
    }

    // ---------------------------------------------------------------------------------
    // 저장 / 불러오기
    // ---------------------------------------------------------------------------------

    // 마지막으로 지나온 세이브포인트를 지정한 슬롯에 저장한다.
    // 반환값: 저장에 성공하면 true, 아직 세이브포인트를 안 지났으면 false.
    public bool SaveToSlot(int slotIndex)
    {
        if (!CanSave)
        {
            Debug.LogWarning("[SavePointManager] 아직 세이브포인트를 지나지 않아 저장할 수 없습니다.");
            return false;
        }
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SavePointManager] SaveManager가 없습니다. Title 씬을 거쳐서 실행했는지 확인하세요.");
            return false;
        }

        var data = new SaveData
        {
            chapterId = LastSavePointId,
            sceneId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            scenarioCsv = LastScenarioCsv,
            lineIndex = LastLineIndex,
            playTimeSeconds = playTimeSeconds,
            acquiredItemIds = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetAcquiredItemList()
                : new System.Collections.Generic.List<string>(),
            noteEntryIds = NoteManager.Instance != null
                ? NoteManager.Instance.GetRecordedEntryList()
                : new System.Collections.Generic.List<string>()
        };

        SaveManager.Instance.Save(slotIndex, data);
        Debug.Log($"[SavePointManager] 슬롯 {slotIndex}에 저장했습니다. ({LastSavePointId})");
        return true;
    }

    // 세이브 데이터를 실제 게임 상태로 되돌린다.
    // 게임 씬(SampleScene)이 시작될 때 SaveManager.ActiveSave가 있으면 이 함수를 부른다.
    public void RestoreFrom(SaveData data)
    {
        if (data == null) return;

        playTimeSeconds = data.playTimeSeconds;

        // 저장 시점의 세이브포인트 정보를 그대로 복원한다.
        // (불러온 직후에 바로 다시 저장해도 같은 지점이 저장되도록)
        LastScenarioCsv = data.scenarioCsv;
        LastLineIndex = data.lineIndex;
        LastSavePointId = data.chapterId;
        CanSave = !string.IsNullOrEmpty(data.scenarioCsv);

        // 가방과 조사기록도 되돌린다.
        if (InventoryManager.Instance != null && data.acquiredItemIds != null)
        {
            InventoryManager.Instance.RestoreItems(data.acquiredItemIds);
        }
        if (NoteManager.Instance != null && data.noteEntryIds != null)
        {
            NoteManager.Instance.RestoreEntries(data.noteEntryIds);
        }

        Debug.Log($"[SavePointManager] 세이브를 복원했습니다: {data.chapterId} ({data.scenarioCsv} {data.lineIndex}번째 줄)");
    }

    // 새 게임을 시작할 때 호출한다(타이틀의 "시작하기").
    public void ResetForNewGame()
    {
        LastScenarioCsv = null;
        LastLineIndex = 0;
        LastSavePointId = null;
        CanSave = false;
        playTimeSeconds = 0f;
    }

    // 세이브 슬롯 목록에 "1시간 23분" 같이 표시할 때 쓰는 도우미.
    public static string FormatPlayTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        return hours > 0 ? $"{hours}시간 {minutes}분" : $"{minutes}분";
    }
}
