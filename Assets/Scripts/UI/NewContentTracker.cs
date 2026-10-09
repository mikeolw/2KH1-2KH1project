using System.Collections.Generic;

// =====================================================================================
// 새로 생긴 메모/아이템 기억하기 - 퀵바 아이콘의 숫자와 수첩·가방 안의 "NEW" 표시에 쓴다
// =====================================================================================
// 두 가지를 따로 센다.
//   NEW 표시   : 아직 눌러 보지 않은 항목. 수첩에서 그 메모를 / 가방에서 그 아이템을 누르면 사라진다.
//   아이콘 숫자 : 그 창을 마지막으로 연 뒤에 새로 생긴 개수. 창을 열면 0이 된다.
//
// ===== 언제 세나 =====
// NoteManager.AddEntry / FlushDeferredEntries, InventoryManager.AddItem / TryCombine에서
// 새로 생긴 순간 Add()를 부른다. 세이브 불러오기·새 게임(Restore*/ClearAll)과 씬 시작
// (매니저 Awake)에서는 Reset()으로 비운다 - 불러온 직후에 예전 메모가 전부 NEW로 뜨지 않게.
//
// ===== 세이브에 넣지 않는 이유 =====
// 세이브 형식을 건드리지 않으려고 이번 플레이(세션) 동안만 기억한다.
public static class NewContentTracker
{
    public enum Kind { Note, Item }

    private static readonly HashSet<string> newNotes = new HashSet<string>();
    private static readonly HashSet<string> newItems = new HashSet<string>();
    private static readonly HashSet<string> badgeNotes = new HashSet<string>();
    private static readonly HashSet<string> badgeItems = new HashSet<string>();

    // 무엇이든 바뀌면 발생. 퀵바 숫자와 수첩/가방 화면이 구독해서 다시 그린다.
    public static event System.Action Changed;

    private static HashSet<string> NewSet(Kind kind) => kind == Kind.Note ? newNotes : newItems;
    private static HashSet<string> BadgeSet(Kind kind) => kind == Kind.Note ? badgeNotes : badgeItems;

    public static void Add(Kind kind, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        id = id.Trim();
        bool changed = NewSet(kind).Add(id);
        changed |= BadgeSet(kind).Add(id);
        if (changed) Changed?.Invoke();
    }

    public static bool IsNew(Kind kind, string id) => !string.IsNullOrEmpty(id) && NewSet(kind).Contains(id.Trim());

    // 그 항목을 눌러 봤다 -> NEW 표시를 지운다.
    public static void MarkSeen(Kind kind, string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (NewSet(kind).Remove(id.Trim())) Changed?.Invoke();
    }

    // 항목이 아예 없어졌다(조합 재료로 쓰여 사라진 아이템 등) -> 숫자에서도 뺀다.
    public static void Forget(Kind kind, string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        id = id.Trim();
        bool changed = NewSet(kind).Remove(id);
        changed |= BadgeSet(kind).Remove(id);
        if (changed) Changed?.Invoke();
    }

    public static int BadgeCount(Kind kind) => BadgeSet(kind).Count;

    // 그 창을 열었다 -> 아이콘 숫자를 0으로. (NEW 표시는 그대로 남는다)
    public static void ClearBadge(Kind kind)
    {
        var set = BadgeSet(kind);
        if (set.Count == 0) return;
        set.Clear();
        Changed?.Invoke();
    }

    public static void Reset(Kind kind)
    {
        bool changed = NewSet(kind).Count > 0 || BadgeSet(kind).Count > 0;
        NewSet(kind).Clear();
        BadgeSet(kind).Clear();
        if (changed) Changed?.Invoke();
    }
}
