using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// "무대" 연출 담당 - 배경 그림과 캐릭터 스탠딩을 화면에 띄운다
// =====================================================================================
// DialogueSystem이 CSV 한 줄을 읽을 때마다 이 컨트롤러에게 "배경은 이걸로, 스탠딩은 이 사람들로"
// 라고 알려주면, 여기서 실제 화면을 갈아끼운다. 대사 텍스트/사운드는 DialogueSystem이 직접
// 담당하고, "눈에 보이는 그림"만 이 클래스가 맡는 구조다.
//
// ===== CSV에서 쓰는 컬럼 (scenario_XX.csv) =====
//   Background   : 배경 파일 이름 (예: BG_01_Office)
//                  - 비워두면 "이전 줄의 배경을 그대로 유지"한다. 장면이 바뀔 때만 적으면 된다.
//                  - none 이라고 적으면 배경을 지운다(검은 화면).
//   Standing     : 캐릭터 스탠딩 파일 이름 (예: STD_Past01_Hansung_Default)
//                  - 비워두면 "이전 줄의 스탠딩을 그대로 유지"한다.
//                    => 표정을 바꾸고 싶은 줄에만 적으면 되므로, 매 줄마다 적을 필요가 없다.
//                  - none 이라고 적으면 모든 캐릭터를 화면에서 지운다.
//                  - 두 명 이상 세우려면 세로줄(|)로 구분한다.
//                    예) STD_Past01_Hansung_Default|STD_Past01_Jaehoon_Default
//   StandingPos  : 각 캐릭터의 서는 위치. Standing과 같은 순서로 세로줄(|)로 구분한다.
//                  L=왼쪽, C=가운데, R=오른쪽. 비워두면 인원수에 맞춰 자동 배치한다.
//                    1명 -> 가운데 / 2명 -> 왼쪽,오른쪽 / 3명 -> 왼쪽,가운데,오른쪽
//                  예) L|R
//   Talker       : 지금 말하고 있는 캐릭터의 자리(L/C/R). 입 뻐끔(립싱크) 연출에 쓴다.
//                  - 비워두면 Speaker 칸의 이름으로 자동으로 찾는다
//                    (Characters.csv의 이름 ↔ 스탠딩 매핑을 이용. 아래 설명 참고)
//                  - none 이라고 적으면 아무도 입을 움직이지 않는다(나레이션 등).
//
// ===== 화자 자동 인식 (Characters.csv) =====
// CSV의 Speaker 칸에는 "한성", "재훈" 같은 한글 이름이 적히는데, 스탠딩 파일 이름은
// "STD_Past01_Hansung_Default" 처럼 영문이다. 이 둘을 이어주기 위해
// Assets/Resources/Dialogues/Characters.csv 에 "한글 이름 -> 영문 토큰" 표를 적어둔다.
//   예) 한성,Hansung
// 그러면 Speaker가 "한성"인 줄에서는, 화면에 올라와 있는 스탠딩 중 파일 이름에 "Hansung"이
// 들어간 캐릭터를 찾아 그 캐릭터만 입을 움직인다. 표에 없는 이름이면 아무도 입을 안 움직인다.
//
// ===== 씬 배치에 대해 (유니티를 잘 모르는 팀원을 위한 설명) =====
// 아래 backgroundImage / standingSlots 필드는 인스펙터에서 직접 연결해도 되지만,
// 비워두면 이 스크립트가 게임 시작 시 Canvas 아래에 알아서 만들어준다.
// 그래서 씬에 빈 GameObject 하나 만들고 이 스크립트만 붙여두면 일단 동작한다.
// 나중에 위치를 정교하게 잡고 싶어지면 그때 씬에 직접 만들어서 연결하면 된다.
public class StageController : MonoBehaviour
{
    public static StageController Instance;

    [Header("배경 (비워두면 자동 생성)")]
    [Tooltip("화면 전체를 덮는 배경 Image. 비워두면 Canvas 아래에 자동으로 만들어진다.")]
    public Image backgroundImage;

    [Header("캐릭터 스탠딩 자리 (비워두면 자동 생성: 왼쪽/가운데/오른쪽 3자리)")]
    [Tooltip("순서대로 L(왼쪽), C(가운데), R(오른쪽) 자리로 쓴다.")]
    public StandingSlot[] standingSlots;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("자동 생성 시 스탠딩 자리의 가로 위치 (캔버스 가운데 기준, 픽셀)")]
    [Tooltip("왼쪽/가운데/오른쪽 자리가 화면 중앙에서 얼마나 떨어질지. 아트에 맞춰 조정하면 된다.")]
    public float leftSlotX = -380f;
    public float centerSlotX = 0f;
    public float rightSlotX = 380f;

    // 자리 이름(L/C/R)을 standingSlots 배열의 번호로 바꿔주는 표.
    private const int SlotLeft = 0;
    private const int SlotCenter = 1;
    private const int SlotRight = 2;

    // Characters.csv에서 읽어온 "한글 화자 이름 -> 스탠딩 파일 이름 속 영문 토큰" 표.
    // 예: "한성" -> "Hansung"
    private Dictionary<string, string> speakerTokenMap;

    // 지금 화면에 배경이 무엇인지 기억해둔다. 같은 배경을 다시 지정해도 다시 로드하지 않게 하기 위함.
    private string currentBackgroundName;

    private void Awake()
    {
        // 다른 매니저들(UIManager, DialogueSystem 등)과 완전히 동일한 싱글톤 패턴.
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        EnsureStageObjects();
        LoadCharacterMap();
    }

    // ---------------------------------------------------------------------------------
    // 화면 구성 요소 준비
    // ---------------------------------------------------------------------------------

    // 인스펙터에 연결이 안 되어 있으면 배경/스탠딩 자리를 코드로 만들어준다.
    // (씬을 아직 안 꾸민 상태에서도 바로 돌아가게 하기 위한 편의 기능)
    private void EnsureStageObjects()
    {
        // FindAnyObjectByType: 씬에서 Canvas 아무거나 하나를 찾는다.
        // (예전 FindObjectOfType은 유니티 6에서 사용 중단되어 경고가 뜬다)
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[StageController] 씬에 Canvas가 없습니다. 배경/스탠딩을 표시할 수 없습니다.");
            return;
        }

        if (backgroundImage == null)
        {
            backgroundImage = CreateFullScreenImage("Stage_Background");

            // 배경은 다른 모든 UI보다 뒤에 있어야 하므로 캔버스의 맨 첫 번째 자식으로 보낸다.
            // (유니티 UI는 계층에서 위에 있을수록 뒤에 그려진다.)
            backgroundImage.transform.SetAsFirstSibling();
            backgroundImage.enabled = false; // 배경이 지정되기 전엔 안 보이게
        }

        if (standingSlots == null || standingSlots.Length < 3)
        {
            standingSlots = new StandingSlot[3];
            standingSlots[SlotLeft] = CreateStandingSlot("Stage_Standing_L", leftSlotX);
            standingSlots[SlotCenter] = CreateStandingSlot("Stage_Standing_C", centerSlotX);
            standingSlots[SlotRight] = CreateStandingSlot("Stage_Standing_R", rightSlotX);

            // 스탠딩은 배경보다는 앞, 대사창보다는 뒤에 있어야 한다.
            // 배경(첫 번째) 바로 다음 자리로 옮겨서 대사창/버튼류가 위에 오도록 한다.
            for (int i = 0; i < standingSlots.Length; i++)
            {
                standingSlots[i].transform.SetSiblingIndex(i + 1);
            }
        }
    }

    // 화면 전체를 채우는 Image를 만든다 (배경용).
    private Image CreateFullScreenImage(string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(targetCanvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        // anchorMin(0,0) ~ anchorMax(1,1) + offset 0 = 부모(캔버스) 크기에 딱 맞게 늘어남
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        // 배경은 클릭 대상이 아니다. 켜두면 대사창 클릭을 가로챈다.
        img.raycastTarget = false;
        return img;
    }

    // 캐릭터 스탠딩 한 자리를 만든다. 화면 아래쪽 가운데를 기준으로 x만큼 옆으로 옮긴다.
    private StandingSlot CreateStandingSlot(string objectName, float x)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(StandingSlot));
        go.transform.SetParent(targetCanvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        // 실제 위치는 그림을 올릴 때 IllustLayout이 다시 잡는다(IllustLayout.Apply 참고).
        // 여기서는 화면 한가운데를 기준으로만 맞춰둔다.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);

        var slot = go.GetComponent<StandingSlot>();

        // 배치표(IllustLayout.csv)에 아직 위치가 없는 스탠딩은 이 좌표에 놓인다.
        // 배치를 잡기 전에도 여러 명이 한자리에 겹쳐 보이지 않게 하기 위한 임시값이다.
        slot.fallbackPosition = new Vector2(x, 0f);

        return slot;
    }

    // Characters.csv를 읽어서 "한글 이름 -> 영문 토큰" 표를 만든다.
    // 파일이 없어도 게임은 그대로 돌아간다(립싱크만 동작하지 않음).
    private void LoadCharacterMap()
    {
        speakerTokenMap = new Dictionary<string, string>();

        var rows = CSVReader.Read("Dialogues/Characters");
        if (rows == null || rows.Count == 0)
        {
            Debug.LogWarning(
                "[StageController] Dialogues/Characters.csv를 찾지 못했습니다. " +
                "화자 자동 인식(입 뻐끔 연출)이 동작하지 않습니다.");
            return;
        }

        foreach (var row in rows)
        {
            string speaker = GetField(row, "Speaker").Trim();
            string token = GetField(row, "StandingToken").Trim();
            if (string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(token)) continue;

            speakerTokenMap[speaker] = token;
        }
    }

    private string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // DialogueSystem이 호출하는 부분
    // ---------------------------------------------------------------------------------

    // 배경을 바꾼다. fileName이 비어 있으면 아무것도 하지 않는다(= 이전 배경 유지).
    public void ApplyBackground(string fileName)
    {
        if (backgroundImage == null) return;
        if (string.IsNullOrWhiteSpace(fileName)) return; // 빈 칸 = 유지

        fileName = fileName.Trim();

        // "none"이면 배경을 지운다.
        if (string.Equals(fileName, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            currentBackgroundName = null;
            backgroundImage.sprite = null;
            backgroundImage.enabled = false;
            return;
        }

        // 같은 배경이면 다시 로드하지 않는다.
        if (currentBackgroundName == fileName && backgroundImage.sprite != null) return;

        Sprite sprite = IllustLoader.LoadBackground(fileName);
        if (sprite == null) return; // 경고는 IllustLoader가 이미 남겼다

        currentBackgroundName = fileName;
        backgroundImage.sprite = sprite;
        backgroundImage.enabled = true;
    }

    // 캐릭터 스탠딩을 바꾼다.
    //   standingSpec : "STD_A" 또는 "STD_A|STD_B" (비면 유지, "none"이면 전원 퇴장)
    //   posSpec      : "L|R" 같은 자리 지정 (비면 인원수에 맞춰 자동 배치)
    public void ApplyStandings(string standingSpec, string posSpec)
    {
        if (standingSlots == null) return;
        if (string.IsNullOrWhiteSpace(standingSpec)) return; // 빈 칸 = 유지

        standingSpec = standingSpec.Trim();

        // "none"이면 전원 퇴장
        if (string.Equals(standingSpec, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            foreach (var slot in standingSlots) slot?.Hide();
            return;
        }

        string[] names = standingSpec.Split('|');
        string[] positions = string.IsNullOrWhiteSpace(posSpec)
            ? null
            : posSpec.Trim().Split('|');

        // 이번 줄에서 실제로 사용할 자리들을 먼저 계산해둔다.
        // (계산이 끝난 뒤에 "쓰이지 않은 자리"를 비워야, 같은 캐릭터가 자리만 옮길 때
        //  잠깐 사라졌다 나타나는 깜빡임이 생기지 않는다.)
        var used = new bool[standingSlots.Length];

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            int slotIndex = ResolveSlotIndex(positions, i, names.Length);
            if (slotIndex < 0 || slotIndex >= standingSlots.Length) continue;

            standingSlots[slotIndex]?.Show(name);
            used[slotIndex] = true;
        }

        // 이번 줄에 지정되지 않은 자리는 비운다(그 캐릭터는 퇴장).
        for (int i = 0; i < standingSlots.Length; i++)
        {
            if (!used[i]) standingSlots[i]?.Hide();
        }
    }

    // i번째 캐릭터가 어느 자리에 설지 결정한다.
    //   posSpec이 있으면 그대로 따르고(L/C/R),
    //   없으면 인원수에 맞춰 자동 배치한다: 1명=가운데, 2명=왼쪽/오른쪽, 3명=왼쪽/가운데/오른쪽
    private int ResolveSlotIndex(string[] positions, int index, int totalCount)
    {
        if (positions != null && index < positions.Length)
        {
            switch (positions[index].Trim().ToUpperInvariant())
            {
                case "L": return SlotLeft;
                case "C": return SlotCenter;
                case "R": return SlotRight;
            }
        }

        // 자동 배치
        if (totalCount <= 1) return SlotCenter;
        if (totalCount == 2) return index == 0 ? SlotLeft : SlotRight;
        return index; // 3명 이상이면 순서대로 L, C, R
    }

    // 지금 말하고 있는 캐릭터만 입을 움직이게 한다.
    //   speaker    : CSV의 Speaker 칸 (예: "한성")
    //   talkerSpec : CSV의 Talker 칸. "L"/"C"/"R"로 직접 지정하거나, "none"이면 아무도 안 움직임.
    //                비어 있으면 speaker 이름으로 자동으로 찾는다.
    //   talking    : 대사 타이핑이 진행 중이면 true, 끝났으면 false
    public void SetTalking(string speaker, string talkerSpec, bool talking)
    {
        if (standingSlots == null) return;

        // 말이 끝났으면 전원 입 다물기
        if (!talking)
        {
            foreach (var slot in standingSlots) slot?.SetTalking(false);
            return;
        }

        int targetSlot = ResolveTalkerSlot(speaker, talkerSpec);

        for (int i = 0; i < standingSlots.Length; i++)
        {
            standingSlots[i]?.SetTalking(i == targetSlot);
        }
    }

    // 어느 자리의 캐릭터가 말하고 있는지 알아낸다. 못 찾으면 -1(아무도 입을 안 움직임).
    private int ResolveTalkerSlot(string speaker, string talkerSpec)
    {
        // 1) CSV의 Talker 칸에 직접 적어둔 경우 그대로 따른다.
        if (!string.IsNullOrWhiteSpace(talkerSpec))
        {
            switch (talkerSpec.Trim().ToUpperInvariant())
            {
                case "L": return SlotLeft;
                case "C": return SlotCenter;
                case "R": return SlotRight;
                case "NONE": return -1;
            }
        }

        // 2) Speaker 이름으로 자동 인식.
        if (string.IsNullOrWhiteSpace(speaker)) return -1;
        if (speakerTokenMap == null) return -1;
        if (!speakerTokenMap.TryGetValue(speaker.Trim(), out string token)) return -1;

        // 화면에 올라와 있는 스탠딩 중 파일 이름에 그 토큰이 들어간 자리를 찾는다.
        // 예: 화자 "한성" -> 토큰 "Hansung" -> "STD_Past01_Hansung_Default"가 올라온 자리
        for (int i = 0; i < standingSlots.Length; i++)
        {
            string current = standingSlots[i] != null ? standingSlots[i].CurrentFileName : null;
            if (!string.IsNullOrEmpty(current) && current.Contains(token)) return i;
        }

        return -1;
    }

    // 조사 화면 등으로 잠깐 넘어갈 때 무대(배경+스탠딩)를 통째로 숨기거나 다시 보여준다.
    // 조사 화면은 자기 배경을 따로 가지고 있으므로, 그 뒤에 대사 배경이 비쳐 보이면 안 된다.
    public void SetStageVisible(bool visible)
    {
        if (backgroundImage != null && backgroundImage.sprite != null)
        {
            backgroundImage.enabled = visible;
        }

        if (standingSlots != null)
        {
            foreach (var slot in standingSlots)
            {
                if (slot == null) continue;
                // 그림이 올라와 있는 자리만 껐다 켠다(빈 자리는 원래 꺼져 있음).
                if (!string.IsNullOrEmpty(slot.CurrentFileName)) slot.gameObject.SetActive(visible);
            }
        }
    }
}
