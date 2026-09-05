using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 아이템 정의(이름/설명/그림/조합)를 CSV에서 읽어오는 데이터베이스
// =====================================================================================
// ===== 왜 CSV인가? =====
// 대사(scenario_XX.csv)나 조사 텍스트(InvestigationData.csv)와 마찬가지로, 아이템 이름과
// 설명도 "글"이라서 기획/시나리오 담당자가 유니티를 열지 않고 고칠 수 있어야 한다.
// 그래서 인스펙터가 아니라 CSV로 관리한다.
//
// ===== 읽어오는 파일 2개 =====
//  1) Assets/Resources/Dialogues/ItemData.csv       - 아이템 하나하나의 정의
//  2) Assets/Resources/Dialogues/ItemCombinations.csv - 아이템끼리 조합하는 규칙
//
// ===== ItemData.csv 컬럼 설명 =====
//   ItemId       : 아이템을 구분하는 고유 문자열(영문 소문자+밑줄 권장). 이 값이 열쇠 역할을 한다.
//                  InvestigationData.csv의 ItemId 칸, InventorySlotUI.itemId와 반드시 같아야 한다.
//   DisplayName  : 가방에 표시될 한글 이름 (예: 메모장)
//   Description  : 가방에서 아이템을 눌렀을 때 나오는 설명글
//   Icon         : 가방 목록에 뜰 아이콘 그림 파일 이름 (Resources/Illusts/Objects/ 기준)
//   ViewerType   : 이 아이템을 조사했을 때 큰 화면으로 자료를 펼쳐 보여줄지 여부
//                    None     - 펼치지 않음 (설명글만)
//                    Document - 서류 보기 화면으로 펼침
//                    Photo    - 사진 보기 화면으로 펼침
//                  (Document/Photo는 지금 보여주는 틀만 다르고 동작은 같다.
//                   나중에 서류는 종이 질감, 사진은 앨범 느낌으로 꾸미려고 구분해둔 것.)
//   ViewerImages : 펼쳐서 보여줄 그림 파일 이름들. 여러 장이면 세로줄(|)로 구분한다.
//                  예) OBJ_07_Documents_F01L|OBJ_07_Documents_F01R
//                  비워두면 Icon 그림을 크게 보여준다.
//   AcquireScene : 이 아이템을 얻는 장면 (예: #01). 지금은 참고용 메모이고 코드에서 쓰지 않는다.
//
// ===== ItemCombinations.csv 컬럼 설명 =====
//   ItemA, ItemB : 조합할 두 아이템의 ItemId. 순서는 상관없다(A+B == B+A).
//   ResultItem   : 조합 성공 시 새로 얻는 아이템의 ItemId.
//   ConsumeA / ConsumeB : TRUE면 조합에 쓴 재료가 가방에서 사라진다. FALSE면 남는다.
//   ResultMessage: 조합 성공 시 화면에 띄울 안내 문구.
public static class ItemDatabase
{
    // 아이템 하나의 정의를 담는 그릇.
    public class ItemInfo
    {
        public string itemId;
        public string displayName;
        public string description;
        public string iconName;       // Resources/Illusts/Objects/ 기준 파일 이름
        public ItemViewerType viewerType;
        public string[] viewerImages; // 펼쳐 볼 그림들 (없으면 길이 0)
        public string acquireScene;

        // 아이콘 Sprite를 실제로 불러온다(캐시는 IllustLoader가 해준다).
        public Sprite GetIcon() => IllustLoader.LoadObject(iconName);
    }

    // 아이템을 펼쳐 보는 화면의 종류.
    public enum ItemViewerType
    {
        None,      // 펼치지 않음
        Document,  // 서류 보기
        Photo      // 사진 보기
    }

    // 아이템 조합 규칙 하나.
    public class CombinationRule
    {
        public string itemA;
        public string itemB;
        public string resultItem;
        public bool consumeA;
        public bool consumeB;
        public string resultMessage;
    }

    private const string ItemCsv = "Dialogues/ItemData";
    private const string CombinationCsv = "Dialogues/ItemCombinations";

    // ItemId -> 아이템 정의
    private static Dictionary<string, ItemInfo> items;
    private static List<CombinationRule> combinations;

    // ---------------------------------------------------------------------------------
    // 로딩
    // ---------------------------------------------------------------------------------

    // 아직 안 읽었으면 CSV를 읽어온다. 어느 함수를 먼저 부르든 알아서 준비되도록
    // 모든 공개 함수 앞에서 이걸 부른다 (InvestigationController의 LoadHotspotDataIfNeeded와 같은 방식).
    private static void EnsureLoaded()
    {
        if (items != null) return;

        items = new Dictionary<string, ItemInfo>();
        combinations = new List<CombinationRule>();

        LoadItems();
        LoadCombinations();
    }

    private static void LoadItems()
    {
        var rows = CSVReader.Read(ItemCsv);
        if (rows == null || rows.Count == 0)
        {
            Debug.LogWarning(
                $"[ItemDatabase] {ItemCsv}.csv를 읽지 못했습니다. " +
                "가방에 아이템 이름/설명이 표시되지 않습니다. " +
                "구글 드라이브에서 받은 CSV를 Assets/Resources/Dialogues/에 넣었는지 확인하세요.");
            return;
        }

        foreach (var row in rows)
        {
            string id = GetField(row, "ItemId").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var info = new ItemInfo
            {
                itemId = id,
                displayName = GetField(row, "DisplayName").Trim(),
                description = GetField(row, "Description"),
                iconName = GetField(row, "Icon").Trim(),
                acquireScene = GetField(row, "AcquireScene").Trim(),
                viewerType = ParseViewerType(GetField(row, "ViewerType")),
                viewerImages = SplitList(GetField(row, "ViewerImages"))
            };

            // 같은 ItemId가 두 번 적혀 있으면 뒤엣것이 앞엣것을 덮어쓴다.
            // 오타로 인한 중복을 눈치챌 수 있게 경고를 남긴다.
            if (items.ContainsKey(id))
            {
                Debug.LogWarning($"[ItemDatabase] ItemId '{id}'가 ItemData.csv에 두 번 이상 있습니다. 마지막 것만 사용됩니다.");
            }
            items[id] = info;
        }
    }

    private static void LoadCombinations()
    {
        var rows = CSVReader.Read(CombinationCsv);
        if (rows == null || rows.Count == 0)
        {
            // 조합 규칙은 없어도 게임이 돌아가므로 경고 수준을 낮춘다(로그만).
            Debug.Log($"[ItemDatabase] {CombinationCsv}.csv가 없거나 비어 있습니다. 아이템 조합 기능이 비활성화됩니다.");
            return;
        }

        foreach (var row in rows)
        {
            string a = GetField(row, "ItemA").Trim();
            string b = GetField(row, "ItemB").Trim();
            string result = GetField(row, "ResultItem").Trim();
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(result)) continue;

            combinations.Add(new CombinationRule
            {
                itemA = a,
                itemB = b,
                resultItem = result,
                consumeA = GetField(row, "ConsumeA").Trim().ToLower() == "true",
                consumeB = GetField(row, "ConsumeB").Trim().ToLower() == "true",
                resultMessage = GetField(row, "ResultMessage")
            });
        }
    }

    private static ItemViewerType ParseViewerType(string raw)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "document": return ItemViewerType.Document;
            case "photo": return ItemViewerType.Photo;
            default: return ItemViewerType.None;
        }
    }

    // "A|B|C" 형태의 문자열을 배열로 자른다. 비어 있으면 길이 0 배열을 돌려준다
    // (null을 돌려주면 쓰는 쪽에서 매번 null 검사를 해야 해서 번거롭다).
    private static string[] SplitList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new string[0];

        string[] parts = raw.Split('|');
        var list = new List<string>();
        foreach (var p in parts)
        {
            string t = p.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list.ToArray();
    }

    private static string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // 조회
    // ---------------------------------------------------------------------------------

    // ItemId로 아이템 정의를 찾는다. 없으면 null.
    public static ItemInfo Get(string itemId)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        return items.TryGetValue(itemId.Trim(), out var info) ? info : null;
    }

    // 가방에 표시할 이름. 정의가 없으면 ItemId를 그대로 보여준다(빈칸보다는 낫다).
    public static string GetDisplayName(string itemId)
    {
        var info = Get(itemId);
        return info != null && !string.IsNullOrEmpty(info.displayName) ? info.displayName : itemId;
    }

    public static string GetDescription(string itemId)
    {
        var info = Get(itemId);
        return info != null ? info.description : "";
    }

    // 등록된 모든 아이템 정의. 가방 UI가 목록을 그릴 때 쓴다.
    public static IEnumerable<ItemInfo> All
    {
        get
        {
            EnsureLoaded();
            return items.Values;
        }
    }

    // 두 아이템을 조합할 수 있는지 찾아본다. 순서는 상관없다(A+B == B+A).
    // 조합할 수 없으면 null을 반환한다.
    public static CombinationRule FindCombination(string itemA, string itemB)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(itemA) || string.IsNullOrWhiteSpace(itemB)) return null;

        itemA = itemA.Trim();
        itemB = itemB.Trim();

        foreach (var rule in combinations)
        {
            bool sameOrder = rule.itemA == itemA && rule.itemB == itemB;
            bool swapped = rule.itemA == itemB && rule.itemB == itemA;
            if (sameOrder || swapped) return rule;
        }
        return null;
    }

    // CSV를 고치고 게임을 다시 시작하지 않고 반영하고 싶을 때 쓴다(에디터 테스트용).
    public static void Reload()
    {
        items = null;
        combinations = null;
        EnsureLoaded();
    }
}
