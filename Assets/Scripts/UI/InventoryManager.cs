using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 조사 모드에서 획득한 아이템을 세션 동안 기억하는 매니저 (InvestigationController.cs,
// InventorySlotUI.cs와 함께 동작)
// =====================================================================================
// 아이템 "정의"(이름/설명/그림)는 여기 없다 - 그건 InvestigatableObject 쪽 Inspector 필드에
// 이미 들어있고, 여기서는 오직 "어떤 itemId를 획득했는가"만 문자열 집합으로 들고 있는다.
// 수첩(notePanel) 안의 각 아이템 슬롯(InventorySlotUI)이 이 매니저를 보고 "내 itemId가
// 집합에 있으면 보이기, 없으면 숨기기"를 스스로 판단한다.
//
// ===== 세이브 연동에 대해 =====
// 지금은 이 클래스가 들고 있는 acquiredItemIds가 세이브 파일(SaveData.cs)에 저장되지
// 않는다 - 즉 게임을 재시작하면 초기화된다. SaveData.cs의 chapterId/playTimeSeconds와
// 똑같은 상태(TODO)로 남겨둔 것으로, 우선순위가 낮아서가 아니라 "세이브 시스템 자체가
// 아직 진행 위치를 복원하는 로직이 없어서" 인벤토리만 먼저 연결해봐야 반쪽짜리이기
// 때문이다. 나중에 SaveManager 쪽 복원 로직이 갖춰지면:
//   1) SaveData.cs에 public List<string> acquiredItemIds 필드를 추가하고,
//   2) SaveManager.Save() 호출 직전에 InventoryManager.Instance가 들고 있는 집합을
//      리스트로 변환해서 SaveData에 채우고,
//   3) 세이브 로드 시점에 InventoryManager 쪽에 "복원용 메서드"를 만들어 acquiredItemIds를
//      다시 채워주면 된다.
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    private readonly HashSet<string> acquiredItemIds = new HashSet<string>();

    // InventorySlotUI들이 구독해서, 아이템이 새로 추가될 때마다 자기 표시 상태를 갱신한다.
    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // InvestigationController.Inspect()가 Item 타입 오브젝트를 클릭했을 때 호출한다.
    // 이미 획득한 아이템을 다시 조사해도 안전하다 - HashSet.Add()는 이미 있으면 false만
    // 반환할 뿐 예외를 던지지 않으므로, 중복 추가 방지를 위한 별도 분기가 필요 없다.
    public void AddItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        itemId = itemId.Trim();

        if (acquiredItemIds.Add(itemId))
        {
            // 아이템을 새로 얻었을 때 조사기록(수첩)에도 관련 메모가 있으면 함께 추가한다.
            // NoteEntries.csv에서 TriggerType=Item, TriggerKey=이 itemId인 줄을 찾는다.
            if (NoteManager.Instance != null) NoteManager.Instance.OnItemAcquired(itemId);

            OnInventoryChanged?.Invoke();
        }
    }

    // InventorySlotUI가 자기 슬롯을 보여줄지 말지 판단할 때 쓴다.
    public bool HasItem(string itemId) => acquiredItemIds.Contains(itemId);

    // =================================================================================
    // 아이템 조합 (예: SD카드 + 카메라 -> 사진이 들어있는 카메라)
    // =================================================================================
    // ===== 어떻게 동작하나 =====
    // 가방 화면에서 아이템 하나를 고른 뒤 다른 아이템을 고르면, 그 두 개의 조합 규칙이
    // ItemCombinations.csv에 있는지 찾아본다. 있으면 결과 아이템을 새로 얻고, 규칙에 따라
    // 재료를 없앤다. 없으면 "조합할 수 없다"는 뜻으로 false를 돌려준다.
    //
    // 조합 규칙 자체는 코드가 아니라 CSV에 있으므로, 새로운 조합을 추가할 때 스크립트를
    // 고칠 필요가 없다 (ItemDatabase.cs 상단 주석의 ItemCombinations.csv 설명 참고).

    // 조합에 성공하면 화면에 띄울 안내 문구가 여기에 담긴다(실패하면 빈 문자열).
    public string LastCombinationMessage { get; private set; } = "";

    // 조합이 성공했을 때 발생. 가방 UI가 구독해서 안내 문구를 띄우는 데 쓴다.
    public event Action<string> OnCombinationSucceeded;

    // 두 아이템을 조합해본다.
    // 반환값: 조합에 성공하면 true, 규칙이 없거나 재료가 없으면 false.
    public bool TryCombine(string itemA, string itemB)
    {
        LastCombinationMessage = "";

        if (string.IsNullOrWhiteSpace(itemA) || string.IsNullOrWhiteSpace(itemB)) return false;

        // 같은 아이템끼리는 조합할 수 없다(실수로 두 번 클릭한 경우).
        if (itemA.Trim() == itemB.Trim()) return false;

        // 두 아이템을 실제로 가지고 있어야 한다.
        if (!HasItem(itemA.Trim()) || !HasItem(itemB.Trim())) return false;

        var rule = ItemDatabase.FindCombination(itemA, itemB);
        if (rule == null) return false;

        // 재료 소모. 규칙에서 ConsumeA/ConsumeB가 TRUE인 것만 없앤다.
        // (예: SD카드는 카메라에 꽂으면 사라지지만, 카메라 자체는 남는 식)
        //
        // rule.itemA / rule.itemB는 CSV에 적힌 순서이고, 호출자가 넘긴 순서와 다를 수 있다.
        // ItemDatabase.FindCombination이 순서를 바꿔서도 찾아주기 때문이다.
        // 그래서 "누가 A였는지"는 rule 쪽 이름을 기준으로 판단해야 한다.
        if (rule.consumeA) acquiredItemIds.Remove(rule.itemA);
        if (rule.consumeB) acquiredItemIds.Remove(rule.itemB);

        // 결과 아이템 획득. AddItem을 거치므로 수첩 메모도 자동으로 연동된다.
        acquiredItemIds.Add(rule.resultItem);
        if (NoteManager.Instance != null) NoteManager.Instance.OnItemAcquired(rule.resultItem);

        LastCombinationMessage = string.IsNullOrEmpty(rule.resultMessage)
            ? $"{ItemDatabase.GetDisplayName(rule.itemA)}와(과) {ItemDatabase.GetDisplayName(rule.itemB)}를 합쳤다."
            : rule.resultMessage;

        OnInventoryChanged?.Invoke();
        OnCombinationSucceeded?.Invoke(LastCombinationMessage);

        Debug.Log($"[InventoryManager] 조합 성공: {rule.itemA} + {rule.itemB} -> {rule.resultItem}");
        return true;
    }

    // =================================================================================
    // 세이브 / 로드 연동
    // =================================================================================
    // SaveData.acquiredItemIds에 담아 저장하고, 불러올 때 되돌린다.
    // (HashSet은 JsonUtility가 직렬화하지 못하므로 List로 바꿔서 주고받는다.)

    // 세이브용: 지금까지 얻은 아이템 목록.
    public List<string> GetAcquiredItemList() => new List<string>(acquiredItemIds);

    // 로드용: 세이브에서 읽어온 목록으로 통째로 되돌린다.
    public void RestoreItems(List<string> itemIds)
    {
        acquiredItemIds.Clear();
        if (itemIds != null)
        {
            foreach (string id in itemIds)
            {
                if (!string.IsNullOrWhiteSpace(id)) acquiredItemIds.Add(id.Trim());
            }
        }
        OnInventoryChanged?.Invoke();
    }

    // 새 게임을 시작할 때 가방을 비운다.
    public void ClearAll()
    {
        acquiredItemIds.Clear();
        OnInventoryChanged?.Invoke();
    }
}
