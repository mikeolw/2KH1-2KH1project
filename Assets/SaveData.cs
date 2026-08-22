using System;

// 세이브 슬롯 1개에 저장되는 데이터.
// SaveManager가 이 객체를 JsonUtility로 직렬화해서 슬롯별 파일(save_0.json 등)로 저장한다.
// 필드를 추가/삭제하면 기존에 저장된 JSON 파일과 구조가 달라지니, 필드를 지울 땐
// 기존 세이브가 깨질 수 있다는 점을 염두에 둘 것 (JsonUtility는 없는 필드는 기본값으로 채움).
[Serializable]
public class SaveData
{
    // 이 데이터가 저장된 슬롯 번호(0~2). SaveManager.Save()가 저장 시점에 자동으로 채운다.
    public int slotIndex;

    // 현재 진행 중인 챕터/씬 번호. 시나리오 문서의 "#01", "#02" 같은 씬 태그와 맞춰서 쓰는 걸 권장.
    // TODO: 아직 실제 게임 진행에 따라 이 값을 갱신하는 코드가 없다. DialogueLine.isSavePoint에
    // 도달했을 때(= 시나리오 문서의 {세이브포인트}) 이 값을 갱신하고 SaveManager.Save()를
    // 호출하는 오토세이브 로직이 DialogueSystem 쪽에 추가로 필요하다.
    public string chapterId = "#01";

    // 이 세이브를 로드했을 때 이동할 씬 이름. 지금은 항상 "SampleScene" 하나뿐이라 의미가 크지
    // 않지만, 챕터별로 씬이 나뉘게 되면 여기에 해당 챕터의 씬 이름을 저장하면 된다.
    public string sceneId = "SampleScene";

    // 마지막으로 저장된 시각 (SaveManager.Save()가 자동으로 "yyyy/MM/dd HH:mm" 형식으로 채움).
    // 세이브 슬롯 목록 UI에서 "언제 저장했는지" 표시하는 용도.
    public string timestamp;

    // 누적 플레이 시간(초). TODO: 아직 실제로 시간을 누적시키는 타이머 코드가 없다.
    // 게임 진행 중 Time.deltaTime을 누적해서 세이브 시점에 이 값을 채워주는 처리가 필요하다.
    public float playTimeSeconds;
}
