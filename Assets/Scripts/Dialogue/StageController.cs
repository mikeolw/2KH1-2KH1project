using System.Collections;              // 코루틴(IEnumerator) - 배경 크로스페이드에 쓴다
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

    // 지금 깔려 있는 배경 이름. 스탠딩/조사 오브젝트가 "이 화면 전용 배치 좌표"를 찾을 때 쓴다.
    public string CurrentBackgroundName => currentBackgroundName;

    // 배경을 바꾼다. fileName이 비어 있으면 아무것도 하지 않는다(= 이전 배경 유지).
    //
    // ===== 전환 연출 (CSV의 Transition / TransitionTime 칸) =====
    //   transition : ""(또는 "cut")  = 즉시 바뀐다. 예전과 같은 기본 동작.
    //                "fade"          = 이전 배경이 서서히 사라지며 새 배경이 드러난다(크로스페이드).
    //   seconds    : fade에 걸리는 시간(초). 0 이하면 기본값 0.35초.
    //
    // 대화창/UI는 건드리지 않고 배경만 바뀐다. 화면 전체를 검게 덮는 암전은 이것과 별개로
    // IsFadeOut 칸이 담당한다(DialogueSystem.cs 참고) - 둘은 같이 써도 된다.
    public void ApplyBackground(string fileName, string transition = null, float seconds = 0f)
    {
        if (backgroundImage == null) return;
        if (string.IsNullOrWhiteSpace(fileName)) return; // 빈 칸 = 유지

        fileName = fileName.Trim();

        // "none"이면 배경을 지운다.
        if (string.Equals(fileName, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            StopBackgroundFade();
            currentBackgroundName = null;
            backgroundImage.sprite = null;
            backgroundImage.enabled = false;

            // ===== 암전도 "장면이 바뀐 것"이므로 소품을 치운다 =====
            // 예전에는 배경만 끄고 소품은 그대로 둬서, 검은 화면 위에 이전 장면의 소품이
            // 둥둥 떠 있었다(예: 공사장 승강기가 "나는..." 암전 대사 위에 남음). 암전 화면은
            // 배치 도구 목록에 올릴 수 없는 화면이라 그림이 하나도 없어야 한다.
            // 다른 배경으로 바뀔 때와 똑같이 여기서도 치운다.
            ClearProps();
            return;
        }

        // 같은 배경이면 다시 로드하지 않는다.
        if (currentBackgroundName == fileName && backgroundImage.sprite != null) return;

        Sprite sprite = IllustLoader.LoadBackground(fileName);
        if (sprite == null) return; // 경고는 IllustLoader가 이미 남겼다

        // ===== 장면이 바뀌면 소품은 전부 치운다 =====
        // 소품은 "그 장면에 놓인 물건"이지 계속 들고 다니는 것이 아니다. 예전에는 Props 칸이
        // 비어 있으면 그대로 유지되기만 해서, 한 장면에서 올린 소품이 그 뒤 모든 장면과
        // 조사 화면까지 따라다녔다("소품이 여기저기 등장").
        //
        // 여기서 배경이 실제로 바뀔 때 비워주면, 그 줄의 Props 칸에 적힌 것만 다시 올라온다.
        // (DialogueSystem이 ApplyBackground -> ApplyProps 순서로 부르므로, 같은 줄에서
        //  배경과 소품을 같이 지정하면 치웠다가 곧바로 새로 올린다.)
        // 같은 배경이 이어지는 동안에는 위의 return에 걸려 여기까지 오지 않으므로,
        // 한 장면 안에서 Props 칸을 비워둔 줄들은 소품이 그대로 유지된다.
        ClearProps();

        bool wantFade = !string.IsNullOrWhiteSpace(transition)
                        && transition.Trim().Equals("fade", System.StringComparison.OrdinalIgnoreCase);

        // 아직 배경이 하나도 없을 때(게임 첫 줄 등)는 페이드할 "이전 그림"이 없으므로 그냥 즉시 건다.
        if (wantFade && backgroundImage.sprite != null && backgroundImage.enabled && isActiveAndEnabled)
        {
            StartBackgroundFade(sprite, fileName, seconds > 0f ? seconds : DefaultFadeSeconds);
            return;
        }

        StopBackgroundFade();
        currentBackgroundName = fileName;
        backgroundImage.sprite = sprite;
        backgroundImage.enabled = true;
        // 씬에 미리 만들어둔 Image를 쓰는 경우 반투명 placeholder 색이 남아 있을 수 있어
        // 배경이 뿌옇게 나온다. 흰색(= 그림 그대로)으로 확실히 되돌린다.
        backgroundImage.color = Color.white;
        RefreshPropPlacements();   // 새 배경 기준으로 소품 자리를 다시 잡는다
    }

    // ---------------------------------------------------------------------------------
    // 배경 크로스페이드
    // ---------------------------------------------------------------------------------
    // ===== 어떻게 만드나? =====
    // 이미지 한 장으로는 "서서히 바뀌는" 연출을 만들 수 없어서, 배경 이미지 바로 위에
    // 같은 크기의 임시 이미지를 한 장 겹쳐 둔다. 거기에 "이전 배경"을 넣어놓고,
    // 아래쪽 진짜 배경 이미지에는 "새 배경"을 바로 넣어버린다. 그런 다음 위에 덮인
    // 이전 배경의 투명도를 1 -> 0으로 천천히 낮추면, 이전 그림이 녹아 사라지면서
    // 아래 있던 새 그림이 드러나는 것처럼 보인다.
    //
    // 임시 이미지는 한 번 만들어두고 계속 재사용한다(매번 만들고 지우면 낭비이므로).

    private const float DefaultFadeSeconds = 0.35f;

    private Image fadeOverlayImage;          // 이전 배경을 덮어 두는 임시 이미지
    private Coroutine backgroundFadeRoutine;

    private void StartBackgroundFade(Sprite newSprite, string newName, float seconds)
    {
        // 이전 페이드가 아직 돌고 있으면 즉시 끝낸 상태로 만들고 새로 시작한다.
        StopBackgroundFade();

        EnsureFadeOverlay();
        if (fadeOverlayImage == null)
        {
            // 오버레이를 못 만들었으면 연출 없이 즉시 교체한다(게임이 멈추면 안 되므로).
            currentBackgroundName = newName;
            backgroundImage.sprite = newSprite;
            backgroundImage.enabled = true;
            RefreshPropPlacements();
            return;
        }

        // 위에 덮을 이미지 = 이전 배경
        fadeOverlayImage.sprite = backgroundImage.sprite;
        fadeOverlayImage.color = Color.white;
        fadeOverlayImage.enabled = true;
        fadeOverlayImage.rectTransform.SetSiblingIndex(backgroundImage.rectTransform.GetSiblingIndex() + 1);

        // 아래 진짜 배경 = 새 배경 (이미 바뀌어 있지만 위가 덮고 있어서 아직 안 보인다)
        currentBackgroundName = newName;
        backgroundImage.sprite = newSprite;
        backgroundImage.enabled = true;
        RefreshPropPlacements();   // 새 배경 기준으로 소품 자리를 다시 잡는다

        backgroundFadeRoutine = StartCoroutine(FadeOutOverlay(seconds));
    }

    private IEnumerator FadeOutOverlay(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // Time.deltaTime = 지난 프레임부터 지금까지 걸린 실제 시간(초).
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);

            var c = fadeOverlayImage.color;
            c.a = 1f - t;                  // 1(완전히 보임) -> 0(완전히 투명)
            fadeOverlayImage.color = c;

            yield return null;             // 다음 프레임까지 기다린다
        }

        fadeOverlayImage.enabled = false;
        fadeOverlayImage.sprite = null;    // 다 쓴 그림은 놓아준다
        backgroundFadeRoutine = null;
    }

    // 페이드를 도중에 멈추고 "끝난 상태"로 정리한다.
    private void StopBackgroundFade()
    {
        if (backgroundFadeRoutine != null)
        {
            StopCoroutine(backgroundFadeRoutine);
            backgroundFadeRoutine = null;
        }
        if (fadeOverlayImage != null)
        {
            fadeOverlayImage.enabled = false;
            fadeOverlayImage.sprite = null;
        }
    }

    private void EnsureFadeOverlay()
    {
        if (fadeOverlayImage != null) return;
        if (backgroundImage == null) return;

        var go = new GameObject("BackgroundFadeOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(backgroundImage.transform.parent, false);

        // 배경 이미지와 똑같은 크기/위치로 맞춘다.
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeOverlayImage = go.GetComponent<Image>();
        fadeOverlayImage.raycastTarget = false;   // 클릭이 통과해야 조사 오브젝트를 누를 수 있다
        fadeOverlayImage.enabled = false;
    }

    // ---------------------------------------------------------------------------------
    // 일반 대화 장면의 소품(Props)
    // ---------------------------------------------------------------------------------
    // ===== 무엇인가? =====
    // 조사 화면이 아닌 보통 대화 장면에도 배경 위에 오브젝트 그림을 얹고 싶을 때가 있다
    // (책상 위 서류, 떨어진 카메라 같은 연출용 그림).
    //
    // 조사 오브젝트와 다른 점은 **누를 수 없다**는 것이다. 일반 장면에서는 조사를 하지
    // 않으므로, 소품은 그냥 그림일 뿐이고 클릭은 전부 통과시킨다(대사 진행이 막히면 안 된다).
    //
    // ===== CSV 사용법 =====
    // scenario_*.csv에 Props 칸을 만들고 Objects 폴더의 파일 이름을 적는다.
    // 여러 개면 세로줄(|)로 구분한다. 스탠딩과 규칙이 같다.
    //   (빈칸)  : 이전 줄 그대로 유지
    //   none    : 소품 전부 치우기
    //   OBJ_A|OBJ_B : 이 둘만 남기고 나머지는 치운다
    //
    // 위치는 조사 오브젝트와 똑같이 IllustLayout.csv에서 찾는다(배치 도구로 잡으면 된다).
    public void ApplyProps(string propSpec)
    {
        if (string.IsNullOrWhiteSpace(propSpec)) return;   // 빈 칸 = 유지

        propSpec = propSpec.Trim();

        EnsurePropRoot();
        if (propRoot == null) return;

        // "none"이면 전부 치운다.
        if (string.Equals(propSpec, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            ClearProps();
            return;
        }

        // 이번 줄에 적힌 목록과 지금 올라와 있는 것을 비교해서, 바뀐 것만 손댄다.
        // (매번 전부 지우고 다시 만들면 같은 소품이 한 프레임 깜빡인다)
        var wanted = new List<string>();
        foreach (string raw in propSpec.Split('|'))
        {
            string name = raw.Trim();
            if (!string.IsNullOrEmpty(name)) wanted.Add(name);
        }

        // 목록에 없는 소품은 치운다.
        for (int i = activeProps.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(activeProps[i].name))
            {
                Destroy(activeProps[i].gameObject);
                activeProps.RemoveAt(i);
            }
        }

        // 아직 없는 소품은 새로 만든다.
        foreach (string name in wanted)
        {
            bool exists = false;
            foreach (var p in activeProps) if (p.name == name) { exists = true; break; }
            if (exists) continue;

            Sprite sprite = IllustLoader.LoadObject(name);
            if (sprite == null) continue;   // 경고는 IllustLoader가 남겼다

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(propRoot.transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            // 소품은 누를 수 없다. raycastTarget을 꺼서 클릭이 그대로 통과하게 한다
            // (안 그러면 소품이 대화창 위를 덮어 대사 진행 클릭을 먹어버린다).
            image.raycastTarget = false;

            // 위치는 조사 오브젝트와 같은 배치표를 쓴다(화면별 좌표도 그대로 적용).
            IllustLayout.Apply(image.rectTransform, sprite, name, default, currentBackgroundName);

            activeProps.Add(go.transform);
        }
    }

    // 소품을 전부 치운다. 조사 화면에 들어갈 때도 불러서 대화 장면의 소품이
    // 따라 들어오지 않게 한다 (InvestigationController.Enter 참고).
    public void ClearProps()
    {
        foreach (var p in activeProps) if (p != null) Destroy(p.gameObject);
        activeProps.Clear();
    }

    // ===== 배경이 바뀌었을 때 소품 위치를 다시 잡는다 =====
    // 소품은 Props 칸이 비어 있으면 그대로 남는데(유지), 배치표에 "이 배경 전용 좌표"가
    // 따로 있으면 배경이 바뀐 순간 그 좌표를 써야 맞다. 안 그러면 이전 배경 기준 자리에
    // 그대로 떠 있게 된다. (IllustLayout.cs의 [화면별 좌표] 주석 참고)
    private void RefreshPropPlacements()
    {
        foreach (var p in activeProps)
        {
            if (p == null) continue;

            var image = p.GetComponent<Image>();
            if (image == null || image.sprite == null) continue;

            IllustLayout.Apply(image.rectTransform, image.sprite, p.name, default, currentBackgroundName);
        }
    }

    // 소품을 담을 빈 그릇을 만든다. 배경보다 앞, 스탠딩보다 뒤에 둔다.
    private void EnsurePropRoot()
    {
        if (propRoot != null) return;
        if (targetCanvas == null) return;

        var go = new GameObject("Stage_Props", typeof(RectTransform));
        go.transform.SetParent(targetCanvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        propRoot = go.transform;

        // 배경 바로 뒤(= 배경보다 앞에 그려지는 자리)에 놓는다. 스탠딩은 그보다 더 뒤 순번이라
        // 자연스럽게 소품 위에 그려진다. 순서: 배경 → 소품 → 스탠딩 → 대화창/UI
        if (backgroundImage != null)
        {
            propRoot.SetSiblingIndex(backgroundImage.transform.GetSiblingIndex() + 1);
        }
    }

    private Transform propRoot;
    private readonly List<Transform> activeProps = new List<Transform>();

    // 캐릭터 스탠딩을 바꾼다.
    //   standingSpec : "STD_A" 또는 "STD_A|STD_B" (비면 유지, "none"이면 전원 퇴장)
    public void ApplyStandings(string standingSpec)
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

        // 이번 줄에서 실제로 사용할 자리들을 먼저 계산해둔다.
        // (계산이 끝난 뒤에 "쓰이지 않은 자리"를 비워야, 같은 캐릭터가 자리만 옮길 때
        //  잠깐 사라졌다 나타나는 깜빡임이 생기지 않는다.)
        var used = new bool[standingSlots.Length];

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            int slotIndex = ResolveSlotIndex(i, names.Length);
            if (slotIndex < 0 || slotIndex >= standingSlots.Length) continue;

            // 지금 배경 이름을 함께 넘겨서, 배치표에 "이 배경 전용 좌표"가 있으면 그것을 쓰게 한다
            // (IllustLayout.cs 상단의 [화면별 좌표] 주석 참고).
            standingSlots[slotIndex]?.Show(name, currentBackgroundName);
            used[slotIndex] = true;
        }

        // 이번 줄에 지정되지 않은 자리는 비운다(그 캐릭터는 퇴장).
        for (int i = 0; i < standingSlots.Length; i++)
        {
            if (!used[i]) standingSlots[i]?.Hide();
        }
    }

    // i번째 캐릭터가 어느 자리에 설지 결정한다.
    //   없으면 인원수에 맞춰 자동 배치한다: 1명=가운데, 2명=왼쪽/오른쪽, 3명=왼쪽/가운데/오른쪽
    private int ResolveSlotIndex(int index, int totalCount)
    {

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

    // ===== 무대(배경+스탠딩)가 캔버스 계층에서 어디까지 차지하는지 알려준다 =====
    // 암전(페이드) 연출용 검은 판을 "배경/캐릭터는 덮되 대화창과 버튼은 덮지 않는" 자리에
    // 놓아야 하는데, 그 경계가 바로 무대의 맨 마지막 자식이다.
    // (유니티 UI는 계층에서 아래에 있을수록 앞에 그려진다)
    public int GetTopStageSiblingIndex()
    {
        int top = -1;

        if (backgroundImage != null && backgroundImage.transform.parent == targetCanvas?.transform)
        {
            top = Mathf.Max(top, backgroundImage.transform.GetSiblingIndex());
        }

        if (standingSlots != null)
        {
            foreach (var slot in standingSlots)
            {
                if (slot == null) continue;
                if (slot.transform.parent != targetCanvas?.transform) continue;
                top = Mathf.Max(top, slot.transform.GetSiblingIndex());
            }
        }

        return top;
    }

    // 무대(배경+스탠딩)를 통째로 숨기거나 다시 보여준다.
    //
    // 조사 모드는 이제 이 배경을 그대로 쓰므로(InvestigationController가 ApplyBackground를
    // 호출한다) 조사 때문에 무대를 숨길 일은 없다. 대신 엔딩 연출이나 전체화면 자료처럼
    // 화면을 통째로 비워야 할 때 쓸 수 있게 남겨둔다.
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
