using System;
using System.IO;
using UnityEngine;

// 세이브 슬롯 3개를 파일로 저장/불러오는 매니저.
//
// 씬 구조: Title.unity에서 이 컴포넌트를 가진 GameObject가 생성되고, DontDestroyOnLoad로
// 살아남아서 SaveData -> SampleScene 등 다른 씬으로 넘어가도 계속 존재한다.
// 다른 스크립트는 SaveManager.Instance로 언제든 접근 가능 (싱글톤 패턴, UIManager.cs와 동일한 방식).
//
// 저장 방식: 슬롯마다 별도 JSON 파일(save_0.json, save_1.json, save_2.json)을
// Application.persistentDataPath에 저장한다. PlayerPrefs를 안 쓰는 이유는 SaveData가
// 구조화된 여러 필드를 가지고 있어서 JSON 파일로 관리하는 게 더 명확하기 때문.
//
// 사용 흐름:
//   1. SaveData.unity에서 슬롯 목록을 보여줄 때 Load(slotIndex)로 각 슬롯 내용을 읽어와 표시.
//   2. 세이브가 있는 슬롯을 클릭하면 SetActiveSave()로 "지금 이어서 할 세이브"를 지정하고
//      게임 씬으로 이동. 게임 씬은 SaveManager.Instance.ActiveSave를 읽어서 이어할 위치를
//      복원해야 한다 (TODO: 아직 이 복원 로직이 없음. 아래 ActiveSave 주석 참고).
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    // 세이브 슬롯 개수.
    //
    // 시나리오 문서의 {세이브포인트}가 총 9곳이라, 지나온 지점을 각각 따로 남겨둘 수 있도록
    // 슬롯도 9개로 맞췄다. (예전에는 3개뿐이라 앞 지점으로 되돌아가려면 덮어써야 했다)
    //   #01 사무실 / #02 해안도로 / #03 경찰서 / #04 한성의 방 / #회상05 단서 조합 /
    //   #05 공사장 / #07 회사 조사 / #07 자료실 / #07 사장실
    // 세이브포인트가 늘거나 줄면 이 숫자만 바꾸면 UI도 따라간다(슬롯 UI가 이 값을 보고 만들어진다).
    public const int SlotCount = 9;

    // 타이틀에서 슬롯을 골라 게임 씬으로 넘어갈 때, "지금 어떤 세이브로 플레이 중인지"를
    // 담아두는 값. 게임 씬(DialogueSystem/GameFlowManager 등)이 시작할 때 이 값을 확인해서
    // 저장된 챕터부터 이어가야 하는데, 현재는 DialogueSystem.Start()가 무조건
    // scenario_sample.csv를 처음부터 로드하도록 하드코딩되어 있어 이 값이 아직 쓰이지 않는다.
    // TODO: DialogueSystem이 ActiveSave.chapterId를 참고해서 이어할 지점을 찾도록 연결 필요.
    //
    // 참고: "시작하기" 버튼(TitleManager)은 슬롯 선택 없이 바로 게임 씬으로 이동하므로,
    // 새 게임을 시작한 경우 ActiveSave는 null인 채로 게임이 시작된다. 새 게임 진행 중
    // {세이브포인트}(DialogueLine.isSavePoint)에 도달했을 때 어느 슬롯에 저장할지 정하는
    // 로직도 아직 없다 (예: 비어있는 슬롯에 자동 저장하거나, 저장 시점에 슬롯 선택 UI를 띄우는 등).
    public SaveData ActiveSave { get; private set; }

    private void Awake()
    {
        // 씬을 이동해도 SaveManager가 새로 또 생기지 않도록, 이미 하나 있으면 새로 만든 걸 파괴.
        // (Title 씬을 다시 로드하는 경우 등에 중복 생성되는 걸 막기 위함)
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

    // 슬롯 번호(0~2)에 대응하는 세이브 파일의 실제 경로를 계산한다.
    private string GetSlotPath(int slotIndex) => Path.Combine(Application.persistentDataPath, $"save_{slotIndex}.json");

    // 해당 슬롯에 세이브 파일이 존재하는지 여부만 빠르게 확인할 때 사용.
    public bool HasSave(int slotIndex) => File.Exists(GetSlotPath(slotIndex));

    // 슬롯의 세이브 데이터를 읽어온다. 파일이 없으면(= 빈 슬롯) null을 반환하므로,
    // 호출하는 쪽에서 반드시 null 체크를 해야 한다 (SaveDataSceneController 참고).
    public SaveData Load(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
    }

    // 세이브 데이터를 지정한 슬롯에 저장(덮어쓰기)한다. slotIndex와 timestamp는
    // 호출하는 쪽에서 미리 채우지 않아도 여기서 자동으로 채워준다.
    public void Save(int slotIndex, SaveData data)
    {
        data.slotIndex = slotIndex;
        data.timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
        File.WriteAllText(GetSlotPath(slotIndex), JsonUtility.ToJson(data, true));
    }

    // 슬롯을 완전히 비운다(파일 삭제). TODO: 아직 이 메서드를 호출하는 UI가 없다.
    // 세이브 데이터 화면에 "슬롯 삭제" 버튼이 필요하다면 SaveDataSceneController에서
    // 이 메서드를 연결하면 된다.
    public void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
    }

    // 지금 이어서 플레이할 세이브를 지정한다. 슬롯을 골라 게임 씬으로 넘어가기 직전에 호출.
    public void SetActiveSave(SaveData data)
    {
        ActiveSave = data;
    }
}
