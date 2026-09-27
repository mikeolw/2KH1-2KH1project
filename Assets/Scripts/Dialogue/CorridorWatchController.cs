using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 자료실 앞 복도 감시 (BG_07_InvestigationSite_04 전용)
// =====================================================================================
// ===== 어떤 게임인가 =====
// 자료실 앞 복도에는 야근 중인 직원들이 서 있다. 그 사람들이 일정 시간마다 바뀌는데,
// 그중에는 "범인과 친분이 있는 인물"이 섞여 있다. 그 사람이 복도에 있는 동안 자료실
// 문을 건드리면 곧바로 들켜서 배드엔딩 C로 간다.
//   - 총 10개 "그룹"이 번갈아 나온다. 그룹 하나가 그림 한 장이다.
//   - changeIntervalSeconds 초마다 다음 그룹으로 바뀐다.
//   - 범인측이 없는 그룹이 대부분이고, 아무도 없는 그룹은 낮은 확률로만 나온다
//     (emptyGroupWeight - 다른 그룹보다 뽑힐 가중치를 낮게 준다).
//   - 같은 그룹이 연달아 두 번 나오지 않게 해서 "바뀌었다"는 느낌을 준다.
//
// ===== 그림 =====
// Resources/Illusts/Standings/STD_07_Corridor_01 ~ _10.
// 캔버스 크기(1440x1080) 그대로 그려져 있어서 IllustLayout.csv에 좌표를 적을 필요가 없다
// (IllustLayout.cs의 "방법 1" 참고). 지금 들어있는 것은 목업이므로, 정식 아트가 나오면
// 같은 이름으로 덮어쓰기만 하면 된다.
//
// ===== 어디서 켜고 끄나 =====
// InvestigationController가 자료실 앞 화면을 열 때 Begin(), 그 화면을 벗어나거나 조사를
// 끝낼 때 Stop()을 부른다. 문을 눌렀을 때의 판정은 IsDangerous 하나만 보면 된다.
//
// ===== 씬 배치 =====
// TimingClickMinigameController와 같은 방식이다. 인스펙터 연결이 필요 없고, 없으면
// GameBootstrap이 자동으로 만들어준다. 스스로 Canvas를 찾아 UI를 만든다.
public class CorridorWatchController : MonoBehaviour
{
    public static CorridorWatchController Instance;

    // 이 미니게임이 도는 조사 화면. 배치표(IllustLayout.csv)에서 화면별 좌표를 찾을 때 쓴다.
    public const string ScreenId = "BG_07_InvestigationSite_04";

    // 복도에 서 있는 사람 한 무리.
    [System.Serializable]
    public class CorridorGroup
    {
        [Tooltip("Resources/Illusts/Standings/ 의 그림 파일 이름 (확장자 없이)")]
        public string spriteName;
        [Tooltip("복도에 보이는 사람들의 이름. 비워두면 '복도에 아무도 없다'로 표시한다.")]
        public string caption;
        [Tooltip("범인과 친분이 있는 인물이 섞여 있는가. 이 그룹일 때 문을 누르면 들킨다.")]
        public bool dangerous;
        [Tooltip("뽑힐 가중치. 클수록 자주 나온다.")]
        public float weight = 1f;
    }

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("난이도 조절")]
    [Tooltip("복도 인물이 바뀌는 간격(초). 짧을수록 어렵다.")]
    public float changeIntervalSeconds = 3.5f;
    [Tooltip("사람이 바뀌는 순간 문을 눌러도 억울하지 않도록, 바뀐 직후 이만큼은 판정을 유예한다(초).")]
    public float graceSeconds = 0.25f;

    [Header("안내 문구를 화면에 띄울지")]
    [Tooltip("목업 단계에서 누가 서 있는지 글로 확인하기 위한 것. 정식 아트가 들어오면 끄면 된다.")]
    public bool showCaption = true;

    [Header("복도 인물 그룹 (그림 한 장 = 그룹 하나)")]
    public List<CorridorGroup> groups = new List<CorridorGroup>();

    // 지금 복도에 범인측 인물이 있는가. 문을 누르는 쪽(InvestigationController)이 이것만 본다.
    // 막 바뀐 직후(graceSeconds)에는 아직 안 바뀐 셈 치고 안전하다고 답한다.
    public bool IsDangerous
    {
        get
        {
            if (!IsRunning || current == null) return false;
            if (Time.time - lastChangeTime < graceSeconds) return false;
            return current.dangerous;
        }
    }

    public bool IsRunning { get; private set; }

    // 지금 복도에 있는 사람들의 이름(엔딩 문구에 쓴다). 아무도 없으면 빈 문자열.
    public string CurrentCaption => current != null ? current.caption : "";

    private CorridorGroup current;
    private int currentIndex = -1;
    private float lastChangeTime;
    private float nextChangeTime;

    private GameObject root;
    private Image personImage;
    private TMP_Text captionText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (groups == null || groups.Count == 0) FillDefaultGroups();
    }

    // 인스펙터에서 아무것도 채우지 않았을 때 쓰는 기본 구성.
    // 그림 10장(STD_07_Corridor_01~10)과 짝이 맞다.
    private void FillDefaultGroups()
    {
        groups = new List<CorridorGroup>
        {
            // 아무도 없는 순간. 가중치를 낮게 줘서 어쩌다 한 번만 나온다.
            new CorridorGroup { spriteName = "STD_07_Corridor_01", caption = "", dangerous = false, weight = 0.35f },

            new CorridorGroup { spriteName = "STD_07_Corridor_02", caption = "오수정 사원", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_03", caption = "한도윤 과장", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_04", caption = "서지민 사원, 배유리 사원", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_05", caption = "오수정 사원, 한도윤 과장", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_06", caption = "배유리 사원", dangerous = false, weight = 1f },

            // 범인과 친분이 있는 인물 - 이때 문을 누르면 들킨다.
            new CorridorGroup { spriteName = "STD_07_Corridor_07", caption = "최지훈 대리", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_08", caption = "나영석 대리", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_09", caption = "최지훈 대리, 오수정 사원", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "STD_07_Corridor_10", caption = "나영석 대리, 서지민 사원", dangerous = true, weight = 1f },
        };
    }

    // ---------------------------------------------------------------------------------
    // 시작 / 정지
    // ---------------------------------------------------------------------------------
    public void Begin()
    {
        if (!EnsureUI()) return;
        IsRunning = true;
        root.SetActive(true);
        PlaceAboveStage();      // 배경보다 앞, 조사 오브젝트보다 뒤
        currentIndex = -1;
        PickNext();
    }

    // ===== 그리는 순서 =====
    // 배경(StageController) < 복도 인물 < 조사 오브젝트 < 대화창.
    // 배경보다 뒤에 두면 아예 안 보이고, 조사 오브젝트보다 앞에 두면 문을 가려서
    // 누를 수 없다. InvestigationController가 조사 오브젝트를 "무대 바로 위"에 놓으므로,
    // 복도도 같은 자리에 끼워 넣으면 조사 오브젝트가 한 칸 밀려 자연스럽게 앞에 온다.
    // (조사 오브젝트가 화면마다 다시 만들어지므로 Begin()마다 다시 잡아준다)
    private void PlaceAboveStage()
    {
        var stage = StageController.Instance;
        if (stage == null) return;
        int stageTop = stage.GetTopStageSiblingIndex();
        if (stageTop < 0) return;
        root.transform.SetSiblingIndex(stageTop + 1);
    }

    public void Stop()
    {
        IsRunning = false;
        current = null;
        currentIndex = -1;
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (Time.time < nextChangeTime) return;
        PickNext();
    }

    // 다음 그룹을 가중치에 따라 고른다. 같은 그룹이 연달아 나오지는 않게 한다.
    private void PickNext()
    {
        if (groups == null || groups.Count == 0) return;

        float total = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            if (i == currentIndex && groups.Count > 1) continue;   // 연속 중복 방지
            total += Mathf.Max(0f, groups[i].weight);
        }
        if (total <= 0f) total = 1f;

        float roll = Random.value * total;
        int picked = -1;
        for (int i = 0; i < groups.Count; i++)
        {
            if (i == currentIndex && groups.Count > 1) continue;
            roll -= Mathf.Max(0f, groups[i].weight);
            if (roll <= 0f) { picked = i; break; }
        }
        if (picked < 0) picked = (currentIndex + 1) % groups.Count;

        currentIndex = picked;
        current = groups[picked];
        lastChangeTime = Time.time;
        nextChangeTime = Time.time + Mathf.Max(0.5f, changeIntervalSeconds);
        Apply(current);
    }

    private void Apply(CorridorGroup g)
    {
        if (personImage != null)
        {
            Sprite s = string.IsNullOrWhiteSpace(g.spriteName) ? null : IllustLoader.LoadStanding(g.spriteName);
            personImage.sprite = s;
            personImage.enabled = s != null;
            if (s != null)
            {
                // 배치는 조사 오브젝트/스탠딩과 똑같은 규칙을 쓴다(IllustLayout.cs 참고).
                // 캔버스 크기(1440x1080)로 내보낸 그림은 배경처럼 꽉 채워 깔리고,
                // 여백을 잘라낸 그림이면 배치표의 좌표대로 놓인다.
                IllustLayout.Apply(personImage.rectTransform, s, g.spriteName, default, ScreenId);
            }
        }
        if (captionText != null)
        {
            captionText.gameObject.SetActive(showCaption);
            captionText.text = string.IsNullOrWhiteSpace(g.caption)
                ? "복도에 아무도 없다."
                : "복도: " + g.caption;
        }
    }

    // ---------------------------------------------------------------------------------
    // UI (그림 한 장 + 안내 글줄. 조사 오브젝트를 가리지 않게 뒤쪽에 깐다)
    // ---------------------------------------------------------------------------------
    private bool EnsureUI()
    {
        if (root != null) return true;

        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[CorridorWatchController] 씬에 Canvas가 없어 복도 인물을 띄우지 못합니다.");
            return false;
        }

        root = new GameObject("CorridorWatch", typeof(RectTransform));
        root.transform.SetParent(targetCanvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var personGo = new GameObject("CorridorPerson", typeof(RectTransform), typeof(Image));
        personGo.transform.SetParent(root.transform, false);
        Stretch(personGo.GetComponent<RectTransform>());
        personImage = personGo.GetComponent<Image>();
        // 크기/위치는 그림마다 IllustLayout이 잡아준다 (Apply 참고). 여기서 preserveAspect를
        // 켜두면 캔버스 크기 그림이 레터박스되어 배경과 어긋난다.
        personImage.preserveAspect = false;
        // 복도 인물은 "보이기만 하는" 그림이다. 클릭이 통과해야 뒤의 자료실 문을 누를 수 있다.
        personImage.raycastTarget = false;

        var capGo = new GameObject("CorridorCaption", typeof(RectTransform));
        capGo.transform.SetParent(root.transform, false);
        var capRt = capGo.GetComponent<RectTransform>();
        capRt.anchorMin = new Vector2(0.06f, 0.80f);
        capRt.anchorMax = new Vector2(0.60f, 0.88f);
        capRt.offsetMin = Vector2.zero;
        capRt.offsetMax = Vector2.zero;
        captionText = capGo.AddComponent<TextMeshProUGUI>();
        captionText.fontSize = 26;
        captionText.alignment = TextAlignmentOptions.Left;
        captionText.color = Color.white;
        captionText.raycastTarget = false;

        UIFontHelper.ApplyToChildren(root);

        // 자리는 Begin()에서 잡는다 (PlaceAboveStage 참고). 여기서 맨 앞자리로 보내면
        // 배경보다 뒤가 되어 아무것도 안 보인다.
        root.SetActive(false);
        return true;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
