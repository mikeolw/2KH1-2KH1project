using System;
using System.Collections.Generic;

// 세이브 슬롯 1개에 저장되는 데이터.
// SaveManager가 이 객체를 JsonUtility로 직렬화해서 슬롯별 파일(save_0.json 등)로 저장한다.
//
// ===== 필드를 고칠 때 주의 =====
// JsonUtility는 저장된 JSON에 없는 필드는 "선언부의 기본값"으로 채운다. 그래서 필드를
// 새로 추가하는 건 안전하다(예전 세이브도 그대로 열린다). 반대로 필드 "이름"을 바꾸거나
// 지우면 그 값은 사라지므로, 이미 배포된 뒤에는 되도록 이름을 바꾸지 말 것.
[Serializable]
public class SaveData
{
    // 이 데이터가 저장된 슬롯 번호(0~2). SaveManager.Save()가 저장 시점에 자동으로 채운다.
    public int slotIndex;

    // 사람이 읽을 수 있는 진행 지점 이름. 시나리오 문서의 {세이브포인트}에 붙인 이름이
    // 그대로 들어간다 (예: "#01 사무실"). 세이브 슬롯 목록에 표시된다.
    // CSV의 SavePointId 칸 -> SavePointManager -> 여기로 전달된다.
    public string chapterId = "#01";

    // 이 세이브를 로드했을 때 이동할 씬 이름.
    public string sceneId = "SampleScene";

    // ===== 실제로 "어디까지 봤는지"를 가리키는 두 값 =====
    // 이 두 개가 있어야 정확히 그 지점부터 이어서 재생할 수 있다.
    // scenarioCsv : 저장 당시 진행 중이던 CSV 파일 이름 (확장자 제외, 예: scenario_02)
    // lineIndex   : 그 CSV의 몇 번째 줄까지 봤는지. 불러오면 이 줄부터 다시 재생한다.
    public string scenarioCsv = "";
    public int lineIndex = 0;

    // 마지막으로 저장된 시각 (SaveManager.Save()가 자동으로 "yyyy/MM/dd HH:mm" 형식으로 채움).
    public string timestamp;

    // 누적 플레이 시간(초). SavePointManager가 매 프레임 누적한 값을 저장 시점에 넣어준다.
    public float playTimeSeconds;

    // ===== 진행 상태 =====
    // 지금까지 획득한 아이템의 ItemId 목록. 불러올 때 InventoryManager에 그대로 되돌린다.
    // (List<string>은 JsonUtility가 직렬화할 수 있는 타입이다. Dictionary나 HashSet은 안 되므로
    //  InventoryManager 내부의 HashSet을 List로 바꿔서 여기 담는다.)
    public List<string> acquiredItemIds = new List<string>();

    // 지금까지 조사기록(메모장)에 추가된 항목의 EntryId 목록.
    public List<string> noteEntryIds = new List<string>();
}
