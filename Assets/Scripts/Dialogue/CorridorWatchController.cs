using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static UIKit;   // CreateText, PlaceTopLeft, AddRect, Stretch ... (UIKit.cs)

// =====================================================================================
// 자료실 앞 복도 감시 (BG_07_InvestigationSite_04 전용)
// =====================================================================================
// ===== 어떤 게임인가 =====
// 자료실 앞 복도에는 야근 중인 직원들이 서 있다. 그 사람들이 일정 시간마다 바뀌는데,
// 그중에는 "들키면 안 되는 인물"이 섞여 있다. 그 사람이 복도에 있는 동안 자료실
// 문을 건드리면 곧바로 들켜서 배드엔딩 C로 간다.
//   - 총 10개 "그룹"이 번갈아 나온다. 그룹 하나가 그림 한 장이다.
//   - changeIntervalSeconds 초마다 다음 그룹으로 바뀐다.
//   - 위험 인물이 없는 그룹이 대부분이고, 아무도 없는 그룹은 낮은 확률로만 나온다
//     (emptyGroupWeight - 다른 그룹보다 뽑힐 가중치를 낮게 준다).
//   - 같은 그룹이 연달아 두 번 나오지 않게 해서 "바뀌었다"는 느낌을 준다.
//
// ===== 그림 =====
// Resources/Illusts/Objects/OBJ_07_Normal1~4 (일반 직원), OBJ_07_Suspect1~4 (위험 인물).
// 여백을 잘라낸 그림이라 IllustLayout.csv에 화면별 좌표를 적어두었다
// (Screen = BG_07_InvestigationSite_04, IllustLayout.cs의 "방법 2" 참고).
//
// 한 그룹에 여러 명이 설 수 있다. spriteName에 "이름1|이름2"처럼 세로줄로 이어 적으면
// 그 사람들이 한꺼번에 복도에 선다 (CSV의 ItemId 칸과 같은 표기).
//
// ===== 자료실 문 앞은 반드시 비워 둔다 =====
// 문(X -642~-304)과 그 옆 '자료실' 안내판(X -279~-141)을 사람이 가리면 어디를 눌러야
// 하는지 안 보여서 게임이 성립하지 않는다. 그래서 사람은 그 오른쪽(FreeLeft 이후)에만 선다.
//   - 혼자일 때  : IllustLayout.csv에 적힌 자기 자리. 전부 문 오른쪽으로 잡아 두었다.
//   - 여럿일 때  : 문 오른쪽 빈 복도를 사람 수만큼 나눠 선다 (SlotX). 배치표의 세로(Y)는
//                  그대로 쓰므로 발이 바닥에 붙은 채로 가로로만 자리를 바꾼다.
//
// 예전 목업은 Standings/STD_07_Corridor_01~10 이었고 캔버스 크기(1440x1080)
// 그대로였다. 인스펙터에 그 이름이 남아 있어도 동작하도록 LoadCorridorSprite()가
// 이름 앞머리를 보고 폴더를 고른다.
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

    // ===== 사람이 설 수 있는 가로 범위 (화면 가운데 0, 오른쪽 +) =====
    // 왼쪽 끝을 -115로 잡은 근거: 자료실 문이 X -642~-304, 배경에 그려진 '자료실' 안내판이
    // X -279~-141을 쓴다. 그 오른쪽으로 26px 띄운 자리가 -115다.
    // 오른쪽은 화면 끝(720)에서 조금 안쪽.
    private const float FreeLeft = -115f;
    private const float FreeRight = 710f;

    // 여러 명을 적을 때 쓰는 구분자 ("OBJ_07_Normal1|OBJ_07_Suspect2").
    private static readonly char[] NameSeparator = { '|' };

    // 복도에 서 있는 사람 한 무리.
    [System.Serializable]
    public class CorridorGroup
    {
        [Tooltip("Resources/Illusts/Objects/ 의 그림 파일 이름 (확장자 없이). " +
                 "여러 명이면 \"이름1|이름2\"처럼 세로줄로 이어 적는다. 비우면 복도에 아무도 없다. " +
                 "예전 목업 이름(STD_...)을 적으면 Standings/ 에서 찾는다.")]
        public string spriteName;
        [Tooltip("복도에 보이는 사람들의 이름. 비워두면 '복도에 아무도 없다'로 표시한다.")]
        public string caption;
        [Tooltip("들키면 안 되는 인물이 섞여 있는가. 이 그룹일 때 문을 누르면 들킨다.")]
        public bool dangerous;
        [Tooltip("뽑힐 가중치. 클수록 자주 나온다.")]
        public float weight = 1f;
    }

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("난이도 조절")]
    [Tooltip("복도 인물이 바뀌는 간격(초). 짧을수록 어렵다.")]
    public float changeIntervalSeconds = 1.8f;
    [Tooltip("사람이 바뀌는 순간 문을 눌러도 억울하지 않도록, 바뀐 직후 이만큼은 판정을 유예한다(초).")]
    public float graceSeconds = 0.25f;

    [Header("안내 문구를 화면에 띄울지")]
    [Tooltip("누가 서 있는지 글로 확인하기 위한 개발용 표시. 정식 아트가 들어와서 기본은 꺼둔다. " +
             "켜면 위험 인물인지까지 그대로 보이므로 실제 플레이에서는 켜지 말 것.")]
    public bool showCaption = false;

    [Header("복도 인물 그룹 (그림 한 장 = 그룹 하나)")]
    public List<CorridorGroup> groups = new List<CorridorGroup>();

    // 지금 복도에 위험 인물이 있는가. 문을 누르는 쪽(InvestigationController)이 이것만 본다.
    // 막 바뀐 직후(graceSeconds)에는 아직 안 바뀐 셈 치고 안전하다고 답한다.
    public bool IsDangerous
    {
        get
        {
            if (!IsRunning || current == null) return false;
            if (!frozen && Time.time - lastChangeTime < graceSeconds) return false;
            return current.dangerous;
        }
    }

    public bool IsRunning { get; private set; }

    // 지금 복도에 있는 사람들의 이름(엔딩 문구에 쓴다). 아무도 없으면 빈 문자열.
    public string CurrentCaption => current != null ? current.caption : "";

    private CorridorGroup current;
    private bool frozen;      // 문을 누른 뒤 true: 그룹 교체를 멈추고 유예 없이 판정한다
    private int currentIndex = -1;
    private float lastChangeTime;
    private float nextChangeTime;

    private GameObject root;
    // 복도에 선 사람 그림들. 한 그룹에 여러 명이 설 수 있어서 필요한 만큼 늘려 쓴다
    // (EnsurePersonImage). 안 쓰는 칸은 꺼 두고 다음 그룹에서 다시 쓴다.
    private readonly List<Image> personImages = new List<Image>();
    private TMP_Text captionText;
    private GameObject captionChip;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (groups == null || groups.Count == 0) FillDefaultGroups();
    }

    // 인스펙터에서 아무것도 채우지 않았을 때 쓰는 기본 구성.
    // 정식 아트 8장(OBJ_07_Normal1~4 / OBJ_07_Suspect1~4)과 짝이 맞다.
    //
    // caption은 화면에 띄우는 글이 아니라(showCaption 기본 꺼짐) 개발 중에 누가 서 있었는지
    // 확인하려고 남기는 이름이다. 들켰을 때 콘솔 로그에도 이 이름이 찍힌다
    // (InvestigationController). 인물의 극중 이름은 아직 정해지지 않아서 번호만 붙였다.
    private void FillDefaultGroups()
    {
        groups = new List<CorridorGroup>
        {
            // 아무도 없는 순간. 그림 없이 복도만 보인다.
            // 가중치를 낮게 줘서 어쩌다 한 번만 나온다 (예전 목업 구성과 같은 값).
            new CorridorGroup { spriteName = "", caption = "", dangerous = false, weight = 0.35f },

            // ===== 안전 - 일반 직원뿐이라 이때 들어가면 아무 일도 없다 =====
            new CorridorGroup { spriteName = "OBJ_07_Normal1", caption = "직원 1", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal2", caption = "직원 2", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal3", caption = "직원 3", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal4", caption = "직원 4", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal1|OBJ_07_Normal3", caption = "직원 1, 3", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal2|OBJ_07_Normal4", caption = "직원 2, 4", dangerous = false, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal4|OBJ_07_Normal1|OBJ_07_Normal3", caption = "직원 4, 1, 3", dangerous = false, weight = 1f },

            // ===== 위험 - 위험 인물이 한 명이라도 섞여 있으면 문을 누르는 순간 들킨다 =====
            // 판정 자체는 예전과 똑같다. InvestigationController가 문을 누른 순간
            // Freeze() 후 IsDangerous만 보고 TriggerEnding(EndingType.Bad_C)을 부른다.
            // 여럿이 섞인 그룹은 "누가 서 있는지 훑어봐야 한다"는 긴장을 만든다.
            new CorridorGroup { spriteName = "OBJ_07_Suspect1", caption = "위험 인물 1", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Suspect2", caption = "위험 인물 2", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Suspect3", caption = "위험 인물 3", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Suspect4", caption = "위험 인물 4", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal2|OBJ_07_Suspect1", caption = "직원 2, 위험 인물 1", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Suspect4|OBJ_07_Normal3", caption = "위험 인물 4, 직원 3", dangerous = true, weight = 1f },
            new CorridorGroup { spriteName = "OBJ_07_Normal1|OBJ_07_Suspect2|OBJ_07_Normal4", caption = "직원 1, 위험 인물 2, 직원 4", dangerous = true, weight = 1f },
        };
    }

    // ---------------------------------------------------------------------------------
    // 시작 / 정지
    // ---------------------------------------------------------------------------------
    public void Begin()
    {
        if (!EnsureUI()) return;
        frozen = false;
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

    // 자료실 문을 누른 순간에 부른다. 복도 그림은 그대로 보여주되 더 이상 바뀌지 않게 멈춘다.
    // 멈춘 그림이 위험 그룹이면 IsDangerous가 true로 남아 배드엔딩 판정에 걸린다.
    // 멈춘 뒤에는 유예 시간(graceSeconds)을 적용하지 않는다: 눈앞에 멈춰 있는 그림이
    // 곧 판정 기준이어야 하기 때문이다. 다음 Begin()에서 다시 풀린다.
    public void Freeze()
    {
        frozen = true;
    }

    public void Stop()
    {
        frozen = false;
        IsRunning = false;
        current = null;
        currentIndex = -1;
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        if (!IsRunning || frozen) return;
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

    // 복도 인물 그림을 불러온다.
    // 정식 아트는 조사 오브젝트와 같은 Objects/ 폴더에 OBJ_* 이름으로 들어 있다.
    // 예전 목업은 Standings/ 의 STD_* 였고 인스펙터에 그 이름이 남아 있을 수 있어서,
    // 이름 앞머리를 보고 찾을 폴더를 고른다.
    // (System.StringComparison을 그대로 적는 이유: 이 파일에 using System;을 넣으면
    //  System.Random과 UnityEngine.Random이 부딪혀 PickNext()의 Random.value가 깨진다)
    private static Sprite LoadCorridorSprite(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        string folder = fileName.Trim().StartsWith("STD_", System.StringComparison.OrdinalIgnoreCase)
            ? IllustLoader.StandingFolder
            : IllustLoader.ObjectFolder;
        return IllustLoader.Load(folder, fileName);
    }

    // "이름1|이름2"를 갈라서 돌려준다. 비어 있으면 빈 배열(= 복도에 아무도 없다).
    private static string[] SplitNames(string names)
    {
        if (string.IsNullOrWhiteSpace(names)) return System.Array.Empty<string>();
        string[] parts = names.Split(NameSeparator, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    // 여럿이 설 때 index번째 사람의 가로 위치.
    // 문과 안내판을 피한 빈 복도(FreeLeft~FreeRight)를 사람 수만큼 똑같이 나눠 가운데에 세운다.
    // 그림이 넓어서 그 칸을 넘치면 빈 복도 밖으로는 안 나가도록 잡아당긴다 - 이 잡아당김이
    // 문 앞으로 비집고 들어가는 것을 막는 마지막 안전장치다.
    private static float SlotX(int index, int count, float width)
    {
        float span = FreeRight - FreeLeft;
        float x = FreeLeft + span * (index + 0.5f) / Mathf.Max(1, count);
        float half = width * 0.5f;
        return Mathf.Round(Mathf.Clamp(x, FreeLeft + half, FreeRight - half));
    }

    // 사람 수만큼 그림칸을 마련한다 (안내 글줄보다 뒤에 깔리도록 순서를 맞춘다).
    private Image EnsurePersonImage(int index)
    {
        while (personImages.Count <= index)
        {
            var go = new GameObject($"CorridorPerson{personImages.Count + 1}",
                                    typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            // 크기/위치는 그림마다 IllustLayout이 잡아준다 (Apply 참고). 여기서 preserveAspect를
            // 켜두면 캔버스 크기 그림이 레터박스되어 배경과 어긋난다.
            img.preserveAspect = false;
            // 복도 인물은 "보이기만 하는" 그림이다. 클릭이 통과해야 뒤의 자료실 문을 누를 수 있다.
            img.raycastTarget = false;
            go.transform.SetSiblingIndex(personImages.Count);
            personImages.Add(img);
        }
        return personImages[index];
    }

    private void Apply(CorridorGroup g)
    {
        string[] names = SplitNames(g.spriteName);
        for (int i = 0; i < names.Length; i++)
        {
            Sprite s = LoadCorridorSprite(names[i]);
            var img = EnsurePersonImage(i);
            img.sprite = s;
            img.enabled = s != null;
            if (s == null) continue;

            // 배치는 조사 오브젝트/스탠딩과 똑같은 규칙을 쓴다(IllustLayout.cs 참고).
            // 캔버스 크기(1440x1080)로 내보낸 그림은 배경처럼 꽉 채워 깔리고,
            // 여백을 잘라낸 그림이면 배치표의 좌표대로 놓인다.
            IllustLayout.Apply(img.rectTransform, s, names[i], default, ScreenId);

            // 여럿이면 가로 자리만 다시 잡는다. 세로(Y)는 배치표 값을 그대로 둬야
            // 발이 바닥선에 붙은 채로 남는다 (사람마다 키가 달라서 Y가 제각각이다).
            if (names.Length > 1)
            {
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(SlotX(i, names.Length, s.rect.width),
                                                  rt.anchoredPosition.y);
            }
        }

        // 이번 그룹에서 안 쓰는 칸은 꺼 둔다 (앞 그룹의 사람이 남아 있으면 안 된다).
        for (int i = names.Length; i < personImages.Count; i++) personImages[i].enabled = false;

        if (captionText != null)
        {
            if (captionChip != null) captionChip.SetActive(showCaption);
            captionText.text = string.IsNullOrWhiteSpace(g.caption)
                ? "<color=#8C9AA6>복도에 아무도 없다.</color>"
                : "<color=#8FA4B7><b>복도</b></color>    " + g.caption;
        }
    }

    // ---------------------------------------------------------------------------------
    // UI (그림 한 장 + 안내 글줄. 조사 오브젝트를 가리지 않게 뒤쪽에 깐다)
    // ---------------------------------------------------------------------------------
    private bool EnsureUI()
    {
        if (root != null) return true;

        if (targetCanvas == null) targetCanvas = UILayers.RootCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[CorridorWatchController] 씬에 Canvas가 없어 복도 인물을 띄우지 못합니다.");
            return false;
        }

        root = new GameObject("CorridorWatch", typeof(RectTransform));
        root.transform.SetParent(targetCanvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());

        // 사람 그림칸은 그룹마다 필요한 수만큼 EnsurePersonImage()가 만든다.
        // 첫 한 칸만 미리 만들어 두어 안내 글줄보다 뒤에 깔리는 순서를 잡아둔다.
        EnsurePersonImage(0);

        // 안내 글줄: 퀵바 아래 왼쪽의 어두운 띠 (미니게임 공통 모양).
        //   ▌ 복도   오수정 사원, 한도윤 사원
        var chip = MinigameTheme.AddImage(root.transform, "CorridorCaption", MinigameTheme.Fill);
        captionChip = chip.gameObject;
        var chipRt = chip.rectTransform;
        chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0f, 1f);
        chipRt.anchoredPosition = new Vector2(20f, -124f);
        chipRt.sizeDelta = new Vector2(520f, 52f);
        MinigameTheme.AddBorder(chipRt, MinigameTheme.Line);
        var bar = MinigameTheme.AddImage(chipRt, "AccentBar", MinigameTheme.Accent);
        var barRt = bar.rectTransform;
        barRt.anchorMin = new Vector2(0f, 0f); barRt.anchorMax = new Vector2(0f, 1f); barRt.pivot = new Vector2(0f, 0.5f);
        barRt.sizeDelta = new Vector2(3f, 0f); barRt.anchoredPosition = Vector2.zero;

        captionText = MinigameTheme.AddText(chipRt, "Text", 22, MinigameTheme.TextBright, TextAlignmentOptions.Left);
        var capRt = captionText.rectTransform;
        MinigameTheme.Stretch(capRt);
        capRt.offsetMin = new Vector2(22f, 0f);
        capRt.offsetMax = new Vector2(-16f, 0f);
        captionText.richText = true;
        captionText.overflowMode = TextOverflowModes.Ellipsis;

        UIFontHelper.ApplyToChildren(root);

        // 자리는 Begin()에서 잡는다 (PlaceAboveStage 참고). 여기서 맨 앞자리로 보내면
        // 배경보다 뒤가 되어 아무것도 안 보인다.
        root.SetActive(false);
        return true;
    }

}
