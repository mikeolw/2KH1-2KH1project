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

        if (acquiredItemIds.Add(itemId))
        {
            OnInventoryChanged?.Invoke();
        }
    }

    // InventorySlotUI가 자기 슬롯을 보여줄지 말지 판단할 때 쓴다.
    public bool HasItem(string itemId) => acquiredItemIds.Contains(itemId);
}
