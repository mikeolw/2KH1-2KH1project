using UnityEngine;

// =====================================================================================
// 수첩(notePanel) 안에 아이템별로 미리 고정 배치해두는 슬롯 (InventoryManager.cs와 함께 동작)
// =====================================================================================
// 이 컴포넌트는 "쌓이는 리스트 UI"가 아니라, 수첩 안의 정해진 자리에 아이템 개수만큼
// 미리 놓아둔 GameObject 하나하나에 붙는다 (예: 메모장 자리, 사직서 자리 등). itemId가
// InventoryManager에 없으면 이 GameObject 전체를 꺼서 "아직 안 얻은 아이템은 안 보이게"
// 만들고, 있으면 켜서 아이콘이 보이게 한다. 아이콘 이미지 자체는 이 오브젝트의 Image
// 컴포넌트에 에디터에서 미리 꽂아두면 된다 - 슬롯 자리마다 어차피 아이템이 고정이므로
// 코드에서 스프라이트를 갈아끼울 필요가 없다.
public class InventorySlotUI : MonoBehaviour
{
    [Tooltip("InvestigatableObject의 itemId와 반드시 같은 문자열이어야 한다.")]
    public string itemId;

    // 구독을 먼저 걸고 나서 Refresh()를 호출해야 한다 (순서 중요). Refresh()가 아직 아이템을
    // 얻지 못한 슬롯이라 gameObject.SetActive(false)를 호출하면, 그 순간 OnDisable()이
    // "같은 프레임 안에서" 바로 실행된다. 만약 구독보다 Refresh()를 먼저 하면: Refresh()가
    // 비활성화 -> OnDisable이 아직 걸리지도 않은 구독을 해제 시도(아무 일도 안 일어남) ->
    // 그 다음에야 구독이 걸려서, 결과적으로 "비활성 상태인데 구독은 살아있는" 상태가 남는다.
    // 구독을 먼저 걸면 위와 같은 순서로 OnDisable이 정확히 그 구독을 해제하므로 깔끔하게
    // 끝난다.
    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        bool hasItem = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(itemId);
        gameObject.SetActive(hasItem);
    }
}
