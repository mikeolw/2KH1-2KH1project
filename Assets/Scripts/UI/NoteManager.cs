using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 조사기록(메모장) 관리 - 조사가 진행될수록 주인공이 적어나가는 수첩
// =====================================================================================
// 시나리오상 주인공 재훈은 형사처럼 수첩에 조사 내용을 적어나간다. 플레이어가 무언가를
// 조사하거나 이야기가 진행되면, 그 사실이 "재훈의 시점에서 쓴 메모"로 수첩에 한 줄씩 쌓인다.
// 퀵바의 수첩 버튼(UIManager.ToggleNote)을 누르면 지금까지 쌓인 메모를 전부 볼 수 있다.
//
// ===== 메모 내용은 CSV로 관리한다 =====
// Assets/Resources/Dialogues/NoteEntries.csv 에 "언제 어떤 메모가 추가되는지"를 적어둔다.
// 컬럼:
//   EntryId    : 메모를 구분하는 고유 문자열. 중복 추가를 막고, 세이브에도 이 값이 저장된다.
//   TriggerType: 이 메모가 언제 추가되는지
//                  Item        - 특정 아이템을 얻었을 때 (TriggerKey = ItemId)
//                  Investigate - 특정 조사 화면을 마쳤을 때 (TriggerKey = InvestigationId)
//                  Hotspot     - 특정 조사 오브젝트를 살펴봤을 때 (TriggerKey = "조사id|핫스팟키")
//                  Manual      - 코드/CSV에서 직접 부를 때만 (TriggerKey 무시)
//   TriggerKey : 위 설명 참고
//   Chapter    : 수첩에서 묶어서 보여줄 소제목 (예: "#01 사무실")
//   Text       : 실제로 수첩에 적히는 문장. 재훈의 시점(1인칭/독백체)으로 쓴다.
//   Order      : 같은 Chapter 안에서의 정렬 순서(작을수록 위). 비우면 추가된 순서대로.
//
// ===== #07부터는 실시간으로 추가되지 않는다 =====
// 기획상 #07(회사 잠입 조사) 구간은 타임어택이 걸려 있어서, 조사할 때마다 수첩이 갱신되면
// 플레이어가 수첩만 들여다보게 되어 긴장감이 떨어진다. 그래서 이 구간의 메모는 즉시 쌓지 않고
// 따로 모아두었다가, 구간이 끝난 뒤 한꺼번에 수첩에 올린다.
// CSV에서 Chapter가 "#07"로 시작하는 메모가 그 대상이며, 아래 deferredEntries에 잠시 보관된다.
// (구간이 끝나면 FlushDeferredEntries()를 부르면 된다.)
public class NoteManager : MonoBehaviour
{
    public static NoteManager Instance;

    // 수첩 메모 한 줄의 정의(CSV에서 읽어온 것).
    public class NoteEntry
    {
        public string entryId;
        public string triggerType;   // Item / Investigate / Hotspot / Manual
        public string triggerKey;
        public string chapter;
        public string text;
        public int order;
    }

    private const string NoteCsv = "Dialogues/NoteEntries";

    // CSV에서 읽어온 모든 메모 정의. EntryId -> 정의
    private Dictionary<string, NoteEntry> allEntries;

    // 지금까지 실제로 수첩에 적힌 메모의 EntryId (추가된 순서 유지).
    private readonly List<string> recordedEntryIds = new List<string>();

    // #07 구간처럼 "나중에 한꺼번에 올릴" 메모를 잠시 담아두는 곳.
    private readonly List<string> deferredEntryIds = new List<string>();

    // 실시간 추가를 멈출지 여부. #07 구간에 들어가면 true로 바꾼다.
    // (SetRealtimeUpdate(false) 호출)
    private bool realtimeUpdateEnabled = true;

    // 수첩 내용이 바뀔 때마다 발생. 수첩 UI(NotePanelUI)가 구독해서 화면을 다시 그린다.
    public event System.Action OnNoteChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadEntries();
        AddInitialEntries();
    }

    // ===== 게임 시작 시점에 이미 적혀 있는 메모 =====
    // 수첩은 주인공 재훈이 업무용으로 쓰던 물건이라, 게임을 시작할 때부터 백지가 아니라
    // 회사 관련 메모가 적혀 있어야 한다. NoteEntries.csv에서 TriggerType이 "Initial"인
    // 줄들을 게임 시작과 동시에 수첩에 올린다.
    //
    // 조사로 얻는 메모는 그때그때 추가되지만(OnItemAcquired 등), 이 메모들은 조사와 무관하게
    // 처음부터 있어야 하므로 여기서 한 번에 넣는다.
    private void AddInitialEntries()
    {
        if (allEntries == null) return;

        foreach (var pair in allEntries)
        {
            var entry = pair.Value;
            if (!string.Equals(entry.triggerType, "Initial", System.StringComparison.OrdinalIgnoreCase)) continue;

            // AddEntry를 쓰지 않고 직접 넣는 이유: 시작 메모는 #07 구간의 보류 규칙과
            // 무관하게 항상 바로 보여야 하기 때문이다.
            if (!recordedEntryIds.Contains(entry.entryId)) recordedEntryIds.Add(entry.entryId);
        }

        if (recordedEntryIds.Count > 0) OnNoteChanged?.Invoke();
    }

    // ---------------------------------------------------------------------------------
    // CSV 로딩
    // ---------------------------------------------------------------------------------
    private void LoadEntries()
    {
        allEntries = new Dictionary<string, NoteEntry>();

        var rows = CSVReader.Read(NoteCsv);
        if (rows == null || rows.Count == 0)
        {
            Debug.LogWarning(
                $"[NoteManager] {NoteCsv}.csv를 읽지 못했습니다. 조사기록(메모장)이 비어 있게 됩니다.");
            return;
        }

        foreach (var row in rows)
        {
            string id = GetField(row, "EntryId").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            int.TryParse(GetField(row, "Order").Trim(), out int order);

            allEntries[id] = new NoteEntry
            {
                entryId = id,
                triggerType = GetField(row, "TriggerType").Trim(),
                triggerKey = GetField(row, "TriggerKey").Trim(),
                chapter = GetField(row, "Chapter").Trim(),
                text = GetField(row, "Text"),
                order = order
            };
        }
    }

    private string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // 메모 추가 (다른 시스템이 부르는 부분)
    // ---------------------------------------------------------------------------------

    // 아이템을 얻었을 때. InventoryManager.AddItem()이 호출한다.
    public void OnItemAcquired(string itemId)
    {
        AddEntriesMatching("Item", itemId);
    }

    // 조사 화면을 마쳤을 때. InventoryController.Exit()이 호출한다.
    public void OnInvestigationFinished(string investigationId)
    {
        AddEntriesMatching("Investigate", investigationId);
    }

    // 조사 오브젝트 하나를 살펴봤을 때. InvestigationController.Inspect()가 호출한다.
    //   투 개를 조합한 키를 쓴다: "조사화면id|핫스팟키" (예: "office_desk|Hotspot_Drawer")
    public void OnHotspotInspected(string investigationId, string hotspotKey)
    {
        AddEntriesMatching("Hotspot", investigationId + "|" + hotspotKey);
    }

    // EntryId를 직접 지정해서 추가한다(Manual 타입, 또는 특수한 경우).
    public void AddEntry(string entryId)
    {
        if (allEntries == null) return;
        if (string.IsNullOrWhiteSpace(entryId)) return;

        entryId = entryId.Trim();
        if (!allEntries.ContainsKey(entryId))
        {
            Debug.LogWarning($"[NoteManager] EntryId '{entryId}'가 NoteEntries.csv에 없습니다.");
            return;
        }

        // 이미 적힌 메모는 다시 적지 않는다(같은 곳을 두 번 조사해도 중복되지 않게).
        if (recordedEntryIds.Contains(entryId) || deferredEntryIds.Contains(entryId)) return;

        // #07 구간처럼 실시간 갱신이 꺼져 있으면 일단 보류함에 넣어둔다.
        if (!realtimeUpdateEnabled)
        {
            deferredEntryIds.Add(entryId);
            return;
        }

        recordedEntryIds.Add(entryId);
        OnNoteChanged?.Invoke();
    }

    // triggerType과 triggerKey가 모두 일치하는 메모를 전부 추가한다.
    // (하나의 조사로 메모가 두 줄 이상 늘어나는 경우도 있으므로 "전부" 찾는다.)
    private void AddEntriesMatching(string triggerType, string triggerKey)
    {
        if (allEntries == null) return;

        foreach (var pair in allEntries)
        {
            var entry = pair.Value;
            if (!string.Equals(entry.triggerType, triggerType, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(entry.triggerKey, triggerKey, System.StringComparison.OrdinalIgnoreCase)) continue;

            AddEntry(entry.entryId);
        }
    }

    // ---------------------------------------------------------------------------------
    // #07 구간용: 실시간 갱신 켜고 끄기
    // ---------------------------------------------------------------------------------

    // #07 조사 구간에 들어갈 때 false, 구간이 끝나면 true로 부른다.
    // false인 동안 추가된 메모는 보류함에 쌓이기만 하고 수첩에는 보이지 않는다.
    public void SetRealtimeUpdate(bool enabled)
    {
        realtimeUpdateEnabled = enabled;
        Debug.Log($"[NoteManager] 조사기록 실시간 갱신: {(enabled ? "켜짐" : "꺼짐(#07 구간)")}");
    }

    // 보류함에 쌓여 있던 메모를 한꺼번에 수첩에 올린다. #07 구간이 끝날 때 부른다.
    public void FlushDeferredEntries()
    {
        if (deferredEntryIds.Count == 0) return;

        foreach (string id in deferredEntryIds)
        {
            if (!recordedEntryIds.Contains(id)) recordedEntryIds.Add(id);
        }
        deferredEntryIds.Clear();
        realtimeUpdateEnabled = true;

        OnNoteChanged?.Invoke();
        Debug.Log("[NoteManager] 보류해둔 조사기록을 수첩에 반영했습니다.");
    }

    // ---------------------------------------------------------------------------------
    // 조회 (수첩 UI가 쓰는 부분)
    // ---------------------------------------------------------------------------------

    // 지금까지 적힌 메모들을 챕터별로 묶고, 챕터 안에서는 Order 순으로 정렬해서 돌려준다.
    // 수첩 UI는 이 결과를 그대로 위에서부터 그리면 된다.
    public List<NoteEntry> GetRecordedEntriesSorted()
    {
        var result = new List<NoteEntry>();
        if (allEntries == null) return result;

        foreach (string id in recordedEntryIds)
        {
            if (allEntries.TryGetValue(id, out var entry)) result.Add(entry);
        }

        // 챕터 이름 오름차순 -> 같은 챕터 안에서는 Order 오름차순.
        // 챕터 이름이 "#01", "#02"... 형태라 문자열 정렬만으로도 순서가 맞는다.
        result.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.chapter, b.chapter);
            return c != 0 ? c : a.order.CompareTo(b.order);
        });

        return result;
    }

    // 세이브용: 지금까지 적힌 메모의 EntryId 목록.
    public List<string> GetRecordedEntryList() => new List<string>(recordedEntryIds);

    // 로드용: 세이브에서 읽어온 목록으로 되돌린다.
    public void RestoreEntries(List<string> entryIds)
    {
        recordedEntryIds.Clear();
        deferredEntryIds.Clear();
        if (entryIds != null) recordedEntryIds.AddRange(entryIds);
        realtimeUpdateEnabled = true;
        OnNoteChanged?.Invoke();
    }
}
