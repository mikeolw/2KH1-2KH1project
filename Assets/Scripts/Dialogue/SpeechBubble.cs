using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 말풍선 대사 (1차) - 대사를 아래 대화창 대신 화면 위 말풍선으로 보여준다
// =====================================================================================
// ===== 언제 말풍선이 되나 (DialogueSystem.DisplayLine이 정한다) =====
//   - 일반 대사(나레이션 아님) + 화자 이름이 있고 + 주인공 차재훈이 아닐 때
//   - 긴 독백 / 조사 중 대사가 아닐 때
// 화자가 화면에 서 있으면 그 인물 옆에, 화면에 없으면(화면 밖 목소리 등) 화면 아래 가운데에 뜬다.
// 나레이션, 주인공 차재훈의 대사, 긴 독백, 조사 대사는 지금처럼 아래 대화창에 나온다.
//
// ===== 어떻게 동작하나 =====
// 타이핑, 쪽 넘김, 자동 진행, 스킵, 대사 기록은 전부 원래 대화창(sentenceText)이 그대로 한다.
// 말풍선은 "보여주기만" 한다: 매 프레임 대화창의 글자와 타이핑 진행(몇 글자까지 보였나)을
// 그대로 따라 그리고, 그동안 대화창의 그림·글자·이름 칸은 투명하게 감춘다(AUTO/SKIP 버튼은 남는다).
// 말풍선은 대화창(dialoguePanel)의 자식이라, 대화창이 꺼지거나 ↓로 숨겨지면 같이 사라진다.
//
// ===== 모양 (픽셀 스타일) =====
// 꼬리 없이, 뒤가 살짝 비치는 어두운 바탕 + 도트 한 칸 두께의 청회색 외곽선 +
// 계단식으로 깎인 모서리. 메인 글꼴이 도트 글꼴이라 둥근 모서리와 1px 선은 어울리지 않는다.
// 이름은 말풍선 왼쪽 위에 걸친 같은 모양의 이름표.
//
//      (머리)     ┌ 은사 ┐
//        ●     ┌──────────────────────────┐
//       /█\    │  자네, 여기서 뭐 하고 있나?  ▾│
//              └──────────────────────────┘
//
// 모서리는 PixelBoxSprite가 그린다. 둥근 모서리로 되돌리려면 CreateBubbleBox에서
// PixelBoxSprite 대신 RoundedSprite를 쓰면 된다(아래 주석 참고).
//
// ===== 위치 =====
// 스탠딩 그림은 대부분 1440x1080 화면 크기 그대로(인물이 그림 안의 제자리에 그려져 있음)라서,
// 그림에서 "불투명한 부분의 맨 위 = 머리"를 한 번 읽어 와(그림마다 기억) 그 옆에 말풍선을 놓는다.
// 인물이 화면 왼쪽이면 말풍선은 오른쪽에, 오른쪽이면 왼쪽에 놓인다.
public class SpeechBubble : MonoBehaviour
{
    // ===== 배치 (화면 1440x1080 기준, 화면 가운데가 0,0 / 위로 +) =====
    // public인 값은 자료 보기의 독백 말풍선(DocumentHighlightPlayer)도 같이 쓴다.
    public const float MaxTextWidth = 560f;
    public const float MinBodyWidth = 260f;
    public const float PadX = 40f, PadTop = 40f, PadBottom = 28f;   // 위는 이름표가 걸치는 만큼 더 띄운다
    public const float NameTagX = 24f;            // 이름표 왼쪽 끝 = 말풍선 왼쪽에서 이만큼 안쪽
    private const float GapFromHead = 110f;       // 머리 중심에서 말풍선 가장자리까지 가로 거리
    private const float MouthBelowTop = 0.10f;    // 입 높이 = 머리 맨 위에서 화면 높이의 10% 아래
    private const float ScreenMargin = 40f;
    private const float DialogueBoxTop = -240f;   // 말풍선이 이보다 아래로 내려가면 아래 대화창 자리(AUTO/SKIP)를 가린다
    public const float CornerRadius = 14f;        // 둥근 모서리 시절의 값. 레이아웃 계산이 아직 참고한다.

    // ===== 이름표 글자 크기 =====
    // 도트 글꼴은 고유 크기(16)의 정수배에서만 또렷하게 보여서, FontManager가 글자 크기를
    // 16 눈금으로 맞춘다(FontManager.SnapToPixelGrid). 그래서 19로 두면 16으로 내려앉아
    // 대사 글자(28 -> 32)의 딱 절반 크기로 나왔다 - 이름이 눈에 안 띄던 이유다.
    // 16과 32 사이에 쓸 수 있는 단이 없으므로 다음 단인 32가 되도록 24로 올린다.
    // 이름표 높이도 32짜리 글자가 들어갈 만큼 같이 키운다.
    public const float NameFontSize = 24f;
    public const float NameTagHeight = 50f;

    // ===== 픽셀 스타일 외곽선 =====
    // PixelStep : 화면에 보이는 "도트 한 칸"의 크기 (1440x1080 기준 단위).
    //             글꼴 격자(16px)의 1/4이라 글자와 테두리의 눈금이 어긋나 보이지 않는다.
    // PixelCut  : 모서리를 몇 칸 깎을지. 2면 두 단 계단이 되어 가장 흔한 도트 UI 모양이 된다.
    // PixelBorder: 외곽선 두께. 한 칸으로 두면 선이 도트 하나처럼 보인다.
    public const int PixelStep = 4;
    public const int PixelCut = 2;
    public const float PixelBorder = PixelStep;

    // 원래 대화창처럼 뒤가 살짝 비치는 바탕 + 얇은 외곽선
    public static readonly Color FillColor = UITheme.WithAlpha(UITheme.Panel, 0.82f);
    public static readonly Color OutlineColor = UITheme.WithAlpha(UITheme.Accent, 0.38f);
    // 위 두 겹(외곽선 + 바탕)이 어두운 화면 위에서 겹쳐 보이는 색을 그대로 불투명하게 만든 것.
    // 뒤가 흰 종이인 자료 보기에서 쓴다 - 비치게 두면 회색으로 뜨고 종이 글자가 대사와 겹친다.
    public static readonly Color OpaqueFillColor = UITheme.WithAlpha(
        (FillColor.linear * FillColor.a + OutlineColor.linear * ((1f - FillColor.a) * OutlineColor.a)).gamma, 1f);
    private static readonly Color NameFillColor = UITheme.WithAlpha(UITheme.Panel, 0.95f);
    private static readonly Color NameOutlineColor = UITheme.WithAlpha(UITheme.Accent, 0.6f);

    // 따라 그릴 원래 대화창의 글자와 ▼
    private TMP_Text source;
    private TMP_Text sourceIndicator;

    private RectTransform canvasRect;
    private RectTransform root, body, nameTag;
    private TMP_Text text, nameText, indicator;

    // 지금 씬의 말풍선 (CopyTextStyle이 글꼴을 물려줄 때 쓴다)
    private static SpeechBubble current;

    public bool IsShowing => root != null && root.gameObject.activeSelf;

    // DialogueSystem이 처음 한 번 만든다.
    public static SpeechBubble Create(Transform dialoguePanel, TMP_Text sentenceText, TMP_Text continueIndicator)
    {
        var canvas = UILayers.RootCanvas();
        if (dialoguePanel == null || sentenceText == null || canvas == null) return null;

        var go = new GameObject("SpeechBubble", typeof(RectTransform));
        go.transform.SetParent(dialoguePanel, false);
        var bubble = go.AddComponent<SpeechBubble>();
        bubble.source = sentenceText;
        bubble.sourceIndicator = continueIndicator;
        bubble.canvasRect = (RectTransform)canvas.transform;
        bubble.Build();
        go.SetActive(false);
        current = bubble;
        return bubble;
    }

    // ---------------------------------------------------------------------------------
    // 보이기 / 숨기기
    // ---------------------------------------------------------------------------------

    // ===== 예전 방식: 말하는 인물 옆에 붙이기 (지금은 쓰지 않는다) =====
    // 화자가 바뀔 때마다 말풍선이 건너뛰어서, 한 씬 동안 한자리에 두는 ShowAnchored로 바꿨다.
    // 되돌리려면 DialogueSystem.ApplySpeechBubble에서 ShowAnchored 대신 이것을 부르면 된다.
    // standing: 말하는 인물의 스탠딩 그림. 위치를 못 구하면 false (그러면 ShowOffscreen을 쓴다).
    public bool Show(string speaker, Image standing)
    {
        if (root == null || standing == null || standing.sprite == null) return false;
        root.gameObject.SetActive(true);
        FitToCanvas();

        // ----- 머리 위치 (화면 좌표) -----
        Vector2 headTop = HeadTopOnCanvas(standing);
        float half = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        float mouthY = headTop.y - canvasRect.rect.height * MouthBelowTop;

        Vector2 size = MeasureBody(out float textW);

        // ----- 가로: 인물이 왼쪽이면 오른쪽에, 오른쪽이면 왼쪽에 (가운데면 오른쪽) -----
        bool placeRight = headTop.x <= 0f;
        float cx = placeRight ? headTop.x + GapFromHead + size.x * 0.5f
                              : headTop.x - GapFromHead - size.x * 0.5f;
        // 화면 밖으로 나가면 반대쪽으로 옮겨 보고, 그래도 안 되면 화면 안으로 밀어 넣는다
        if (cx + size.x * 0.5f > half - ScreenMargin || cx - size.x * 0.5f < -half + ScreenMargin)
        {
            float other = placeRight ? headTop.x - GapFromHead - size.x * 0.5f : headTop.x + GapFromHead + size.x * 0.5f;
            if (other + size.x * 0.5f <= half - ScreenMargin && other - size.x * 0.5f >= -half + ScreenMargin) cx = other;
        }
        cx = Mathf.Clamp(cx, -half + ScreenMargin + size.x * 0.5f, half - ScreenMargin - size.x * 0.5f);

        // ----- 세로: 입 높이쯤이 말풍선 가운데. 화면 위·아래 대화창 자리를 넘지 않게 -----
        float cy = Mathf.Clamp(mouthY + 20f, DialogueBoxTop + size.y * 0.5f, halfH - ScreenMargin - NameTagHeight - size.y * 0.5f);

        Layout(speaker, new Vector2(cx, cy), size, textW);
        return true;
    }

    // 화면에 서 있지 않은 화자(화면 밖 목소리 등): 화면 아래 가운데, 아래 대화창 자리 바로 위.
    public void ShowOffscreen(string speaker)
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        FitToCanvas();

        Vector2 size = MeasureBody(out float textW);
        Layout(speaker, new Vector2(0f, DialogueBoxTop + 30f + size.y * 0.5f), size, textW);
    }

    // 배치표(BubbleLayout.csv)에 직접 정해 둔 위치에 띄운다. center: 말풍선 가운데 (화면 가운데 0,0 / 위로 +)
    public void ShowAt(string speaker, Vector2 center)
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        FitToCanvas();
        Vector2 size = MeasureBody(out float textW);
        Layout(speaker, ClampToScreen(center, size), size, textW);
    }

    public void Hide()
    {
        if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 한 씬 동안 자리를 고정하는 말풍선 (지금 쓰는 방식)
    // ---------------------------------------------------------------------------------
    // ===== 왜 고정하나 =====
    // 예전에는 줄마다 "말하는 사람 옆"에 붙였다(위 Show). 화자가 바뀔 때마다 말풍선이
    // 왼쪽↔오른쪽, 위↔아래로 건너뛰어서 눈이 글자를 쫓아다녀야 했다.
    // 그래서 무대가 차려질 때 자리를 한 번만 정하고, 무대가 바뀔 때까지 누가 말하든 같은
    // 자리에 띄운다. 줄마다 바뀌는 것은 이름표와 글자뿐이다.
    //
    // ===== 언제 다시 정하나 =====
    // "처음 보는 스탠딩"일 때만. 열쇠는 StageController.BubbleAnchorKey - 지금 서 있는
    // 스탠딩에서 표정과 입모양을 뗀 것이다. 한 종류의 스탠딩에는 중심점이 하나뿐이므로,
    // 한 번 고른 뒤에는 배경이 바뀌어도, 장이 넘어가도, 표정이 바뀌어도 다시 정하지 않는다.
    // (표정으로 다시 정하면 한 줄마다 덜덜 떨린다)
    //
    // ===== 어디에 놓나 =====
    // 서 있는 사람 전원의 "얼굴 네모"를 구해 두고, 놓을 수 있는 자리를 성기게 훑으며
    // 점수를 매겨 가장 좋은 곳을 고른다 (AnchorCost).
    //   - 얼굴을 가리면 크게 깎는다      ... "스탠딩의 얼굴은 최대한 가리지 않고"
    //   - 몸을 가리는 건 조금만 깎는다
    //   - 그 외에는 늘 쓰던 편한 자리(화면 아래 가운데, 대화창 바로 위)에 가까울수록 좋다
    // 그래서 얼굴이 위쪽에 있는 보통 장면에서는 편한 자리에 그대로 앉고,
    // 얼굴이 낮게 잡힌 장면(가슴 위 구도 등)에서만 그만큼 비켜난다.
    //
    // ===== 무엇을 붙박이로 두나: 한가운데 점 =====
    // 대사 길이에 따라 말풍선 크기가 줄마다 달라지므로, 어느 점을 붙박이로 둘지 정해야 한다.
    // 여기서는 말풍선의 한가운데를 고정한다. 짧은 대사든 긴 대사든 같은 점을 중심으로
    // 사방으로 고르게 늘어나므로, 말풍선이 한쪽으로 쏠리거나 아래로 흘러내리지 않는다.
    //
    // (예전에는 위쪽 변을 고정해서 아래로만 자라게 했다. 글자 첫 줄 높이는 일정했지만
    //  말풍선 아래쪽 변이 줄마다 들쭉날쭉해서 오히려 움직이는 느낌이 더 났다.)
    //
    // 자리를 고를 때는 긴 대사(ReserveLines줄)가 들어갈 네모로 재므로, 긴 줄이 와도
    // 미리 비워 둔 자리 안에서만 자란다.

    // 자리를 고를 때 "이만큼은 비워 두자"고 잡는 줄 수.
    private const int ReserveLines = 4;

    // ===== 얼굴 네모 =====
    // 머리 꼭대기에서 화면 높이의 FaceHeightRatio 만큼 아래까지를 얼굴로 본다 (턱 조금 아래).
    // 잰 머리 폭은 머리카락 끝이라 실제 얼굴보다 좁다. FaceWidthFactor로 넉넉하게 넓힌다.
    private const float FaceHeightRatio = 0.17f;
    private const float FaceWidthFactor = 1.7f;
    private const float FaceMinWidth = 200f;

    // 자리를 훑는 간격. 촘촘할수록 좋지만 20이면 눈으로 차이를 못 느낀다.
    private const float AnchorStep = 20f;

    // 점수 (낮을수록 좋은 자리). 겹치는 넓이는 1000px^2 단위로 줄여서 센다.
    private const float WeightFace = 60f;      // 얼굴을 가리는 것 - 거의 금지에 가깝게
    private const float WeightBody = 0.15f;    // 몸을 가리는 것 - 살짝 피하는 정도
    private const float WeightComfort = 0.3f;  // 편한 자리에서 1px 멀어질 때마다

    // 늘 쓰던 편한 자리: 화면 아래 가운데, 대화창 자리 바로 위 (ShowOffscreen과 같은 곳)
    private const float ComfortGap = 30f;

    // ===== 자동으로 고른 중심점은 스탠딩마다 하나씩 기억해 둔다 =====
    // 배치표(BubbleLayout.csv)에 적어 두지 않은 스탠딩도 중심점은 하나여야 한다. 그래서
    // 한 번 고른 점을 그 스탠딩 열쇠의 것으로 기억하고, 같은 스탠딩이 다시 나오면 무대
    // 사정(배경이 바뀌었든, 다른 인물이 드나들었든)과 상관없이 그 점을 그대로 쓴다.
    //
    // static인 이유: 말풍선 객체가 다시 만들어져도(장면 전환 등) 같은 점을 쓰게 하려고.
    // 글씨 크기도 열쇠에 넣지 않는다 - 크기가 바뀌어도 가운데는 그대로여야 한다.
    private static readonly Dictionary<string, Vector2> autoAnchors = new Dictionary<string, Vector2>();

    private static readonly List<Image> standingBuffer = new List<Image>();
    private readonly List<Rect> faceRects = new List<Rect>();
    private readonly List<Rect> bodyRects = new List<Rect>();

    // 기억해 둔 자동 자리를 잊는다 (F2 > R 로 배치표에서 지웠을 때 다시 고르게 하려고).
    public static void ForgetAutoAnchor(string standingKey)
    {
        if (string.IsNullOrEmpty(standingKey)) autoAnchors.Clear();
        else autoAnchors.Remove(standingKey);
    }

    // 이 스탠딩의 중심점에 띄운다. 그 스탠딩의 중심점은 처음 한 번만 고르고 계속 쓴다.
    public bool ShowAnchored(string speaker, string standingKey)
    {
        if (root == null) return false;
        root.gameObject.SetActive(true);
        FitToCanvas();

        if (string.IsNullOrEmpty(standingKey)) standingKey = StageController.NoStandingKey;

        if (!autoAnchors.TryGetValue(standingKey, out Vector2 center))
        {
            // 열쇠가 한 사람이면 그 사람 쪽에 붙여 고른다. 두 사람 조합/none이면 가운데 기준.
            center = ChooseAnchor(StageController.Instance, standingKey);
            autoAnchors[standingKey] = center;
        }

        // 비워 둔 자리보다 긴 대사가 와서 화면 밖으로 삐져나갈 때만 ClampToScreen이 밀어 넣는다.
        Vector2 size = MeasureBody(out float textW);
        Layout(speaker, ClampToScreen(center, size), size, textW);
        return true;
    }

    private Vector2 ChooseAnchor(StageController stage, string ownerKey)
    {
        float half = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;

        Vector2 reserve = MeasureReserve();
        CollectObstacles(stage);

        // 훑을 범위. 아래로는 대화창(AUTO/SKIP) 자리를 넘지 않는다.
        float minX = -half + ScreenMargin;
        float maxX = half - ScreenMargin - reserve.x;
        float maxTop = halfH - ScreenMargin - NameTagHeight;
        float minTop = DialogueBoxTop + reserve.y;
        if (maxX < minX) maxX = minX;        // 화면이 말풍선보다 좁으면 왼쪽에 붙인다
        if (minTop > maxTop) minTop = maxTop;

        // ===== 편한 자리 = 점수가 같을 때 끌려가는 곳 =====
        // 기본은 화면 아래 가운데(ShowOffscreen과 같은 곳)다.
        // 그런데 이 중심점의 주인이 화면에 서 있으면, 그 사람의 머리 가로 위치로 옮긴다.
        // 그러면 "한성의 말풍선은 한성 쪽"처럼 말하는 사람 옆에 붙는다. 얼굴을 가리는
        // 점수(WeightFace)가 훨씬 무거우므로, 붙되 얼굴은 비켜 간다.
        float comfortX = -reserve.x * 0.5f;
        var owner = stage != null ? stage.FindStandingByKey(ownerKey) : null;
        if (owner != null)
        {
            // 머리 가로 중심에 말풍선 가운데를 맞춘다 (훑을 때는 왼쪽 위 모서리로 세므로 반폭을 뺀다)
            comfortX = Mathf.Clamp(HeadTopOnCanvas(owner).x - reserve.x * 0.5f, minX, maxX);
        }
        var comfort = new Vector2(comfortX, DialogueBoxTop + ComfortGap + reserve.y);

        Vector2 best = comfort;
        float bestCost = float.MaxValue;

        for (float x = minX; x <= maxX + 0.5f; x += AnchorStep)
        {
            for (float top = minTop; top <= maxTop + 0.5f; top += AnchorStep)
            {
                var box = Rect.MinMaxRect(x, top - reserve.y, x + reserve.x, top + NameTagHeight);
                float cost = AnchorCost(box, new Vector2(x, top), comfort);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = new Vector2(x, top);
            }
        }

        // 훑을 때는 왼쪽 위 모서리로 셌지만, 기억해 둘 값은 비워 둔 네모의 한가운데다.
        return new Vector2(best.x + reserve.x * 0.5f, best.y - reserve.y * 0.5f);
    }

    private float AnchorCost(Rect box, Vector2 topLeft, Vector2 comfort)
    {
        float cost = 0f;
        for (int i = 0; i < faceRects.Count; i++) cost += Overlap(box, faceRects[i]) * WeightFace;
        for (int i = 0; i < bodyRects.Count; i++) cost += Overlap(box, bodyRects[i]) * WeightBody;
        cost += Vector2.Distance(topLeft, comfort) * WeightComfort;
        return cost;
    }

    // 두 네모가 겹치는 넓이. 점수를 다루기 쉬운 크기로 1000px^2 단위로 줄여서 돌려준다.
    private static float Overlap(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w <= 0f || h <= 0f ? 0f : w * h / 1000f;
    }

    // 서 있는 사람 전원의 얼굴/몸 네모를 구해 둔다 (화면 좌표).
    private void CollectObstacles(StageController stage)
    {
        faceRects.Clear();
        bodyRects.Clear();
        if (stage == null) return;

        stage.CollectVisibleStandings(standingBuffer);
        float halfH = canvasRect.rect.height * 0.5f;

        foreach (var standing in standingBuffer)
        {
            if (standing == null || standing.sprite == null) continue;

            Vector2 headTop = HeadTopOnCanvas(standing);
            float faceHalf = Mathf.Max(FaceMinWidth * 0.5f, HeadHalfWidthOnCanvas(standing) * FaceWidthFactor);
            float faceBottom = headTop.y - canvasRect.rect.height * FaceHeightRatio;

            faceRects.Add(Rect.MinMaxRect(headTop.x - faceHalf, faceBottom, headTop.x + faceHalf, headTop.y));

            // 몸: 얼굴 아래로 화면 끝까지. 가려도 얼굴만큼 아깝지는 않다.
            float bodyHalf = faceHalf * 1.6f;
            bodyRects.Add(Rect.MinMaxRect(headTop.x - bodyHalf, -halfH, headTop.x + bodyHalf, faceBottom));
        }
        standingBuffer.Clear();
    }

    // 머리 반폭을 화면 좌표로. 스탠딩은 캔버스와 배율이 다를 수 있다.
    private float HeadHalfWidthOnCanvas(Image standing)
    {
        var rt = standing.rectTransform;
        float scale = canvasRect.lossyScale.x != 0f ? rt.lossyScale.x / canvasRect.lossyScale.x : 1f;
        return rt.rect.width * scale * MeasureHead(standing.sprite).halfWidth;
    }

    // 긴 대사가 들어갈 네모. 자리를 고를 때 "이만큼은 비워 두자"는 기준이 된다.
    private Vector2 MeasureReserve()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ReserveLines; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append('가');
        }
        Vector2 pref = text.GetPreferredValues(sb.ToString(), MaxTextWidth, 0f);

        // 잰 뒤에는 원래 글자로 되돌려 둔다 (LateUpdate가 다시 맞추지만 한 프레임도 어긋나지 않게).
        text.text = source != null ? source.text : "";

        return new Vector2(MaxTextWidth + PadX * 2f, Mathf.Ceil(pref.y) + PadTop + PadBottom);
    }

    // 지금 말풍선 가운데 위치 (화면 가운데 0,0 / 위로 +)
    public Vector2 Center => body != null ? body.anchoredPosition : Vector2.zero;

    // 말풍선이 화면에서 차지하는 네모 (화면 가운데 0,0 / 위로 +).
    // 왼쪽 위에 반쯤 걸쳐 있는 이름표 높이까지 포함한다.
    // 선택지 패널이 이 자리를 피해 가려고 쓴다 (ChoicePanelStyle.PlaceClearOf).
    // 말풍선이 떠 있지 않으면 크기 0짜리 빈 네모를 돌려준다.
    public Rect Bounds
    {
        get
        {
            if (!IsShowing || body == null) return new Rect();
            Vector2 c = body.anchoredPosition;
            Vector2 s = body.sizeDelta;
            float top = c.y + s.y * 0.5f
                      + (nameTag != null && nameTag.gameObject.activeSelf ? NameTagHeight * 0.5f : 0f);
            return Rect.MinMaxRect(c.x - s.x * 0.5f, c.y - s.y * 0.5f, c.x + s.x * 0.5f, top);
        }
    }

    private Vector2 ClampToScreen(Vector2 center, Vector2 size)
    {
        float half = canvasRect.rect.width * 0.5f, halfH = canvasRect.rect.height * 0.5f;
        return new Vector2(
            Mathf.Clamp(center.x, -half + size.x * 0.5f, half - size.x * 0.5f),
            Mathf.Clamp(center.y, -halfH + size.y * 0.5f, halfH - NameTagHeight - size.y * 0.5f));
    }

    // ---------------------------------------------------------------------------------
    // 말풍선 배치 모드 (에디터 Play 중 F2 - BubbleLayoutTool.cs)
    // ---------------------------------------------------------------------------------
    // 켜면 말풍선을 마우스로 끌 수 있다. 놓을 때 onMoved(새 가운데 위치)를 부른다.
    // 말풍선에 Button을 붙여 두는 이유: 대화 진행 코드가 "버튼 위를 누른 클릭"은 대사 넘기기로
    // 치지 않으므로(DialogueSystem.IsPointerOverButton), 끄는 동안 대사가 넘어가지 않는다.
    private System.Action<Vector2> onMoved;
    private DragHandle dragHandle;

    public void SetEditable(bool editable, System.Action<Vector2> moved)
    {
        if (body == null) return;
        onMoved = moved;
        var img = body.GetComponent<Image>();
        img.raycastTarget = editable;
        if (editable && dragHandle == null)
        {
            var btn = body.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            dragHandle = body.gameObject.AddComponent<DragHandle>();
            dragHandle.owner = this;
        }
        if (dragHandle != null) dragHandle.enabled = editable;
        var button = body.GetComponent<Button>();
        if (button != null) button.enabled = editable;
    }

    private class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SpeechBubble owner;
        private Vector2 grabOffset;

        public void OnBeginDrag(PointerEventData e)
        {
            if (owner.ScreenToCanvas(e, out Vector2 p)) grabOffset = owner.Center - p;
        }

        public void OnDrag(PointerEventData e)
        {
            if (owner.ScreenToCanvas(e, out Vector2 p)) owner.MoveTo(p + grabOffset);
        }

        public void OnEndDrag(PointerEventData e) => owner.onMoved?.Invoke(owner.Center);
    }

    private bool ScreenToCanvas(PointerEventData e, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, e.pressEventCamera, out local);

    // 크기는 그대로 두고 위치만 옮긴다 (이름표도 같이)
    private void MoveTo(Vector2 center)
    {
        Vector2 size = body.sizeDelta;
        center = ClampToScreen(center, size);
        body.anchoredPosition = center;
        nameTag.anchoredPosition = new Vector2(center.x - size.x * 0.5f + NameTagX, center.y + size.y * 0.5f);
    }

    // 전체 문장을 넣어 말풍선 크기를 잰다 (글자는 원래 대화창 것을 따라간다).
    private Vector2 MeasureBody(out float textW)
    {
        text.text = source.text;
        text.maxVisibleCharacters = 0;
        Vector2 pref = text.GetPreferredValues(source.text, MaxTextWidth, 0f);
        textW = Mathf.Min(MaxTextWidth, Mathf.Ceil(pref.x) + 2f);
        return new Vector2(Mathf.Max(MinBodyWidth, textW + PadX * 2f), Mathf.Ceil(pref.y) + PadTop + PadBottom);
    }

    private void Layout(string speaker, Vector2 center, Vector2 size, float textW)
    {
        body.anchoredPosition = center;
        body.sizeDelta = size;

        // 글자: 왼쪽 정렬, 위는 이름표 자리만큼 더 띄운다
        var trt = (RectTransform)text.transform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(PadX, -PadTop);
        trt.sizeDelta = new Vector2(Mathf.Max(textW, size.x - PadX * 2f), size.y - PadTop - PadBottom);

        // 이름표: 말풍선 왼쪽 위 모서리에 반쯤 걸친다
        nameText.text = speaker ?? "";
        nameTag.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
        nameTag.anchoredPosition = new Vector2(center.x - size.x * 0.5f + NameTagX, center.y + size.y * 0.5f);
    }

    // 원래 대화창의 타이핑 진행과 ▼ 깜빡임을 그대로 따라 한다.
    private void LateUpdate()
    {
        if (source == null) return;
        if (text.text != source.text) text.text = source.text;
        text.maxVisibleCharacters = source.maxVisibleCharacters;

        bool showIndicator = sourceIndicator != null && sourceIndicator.gameObject.activeInHierarchy;
        if (indicator.gameObject.activeSelf != showIndicator) indicator.gameObject.SetActive(showIndicator);
        if (showIndicator) indicator.color = UITheme.WithAlpha(UITheme.Accent, sourceIndicator.color.a);
    }

    // 말풍선 좌표를 화면(캔버스) 좌표와 똑같이 쓰려고, 루트를 캔버스와 같은 자리·크기·배율로 맞춘다.
    // (부모인 대화창은 화면 아래쪽 띠라서 그 좌표를 그대로 쓰면 계산이 복잡해진다)
    private void FitToCanvas()
    {
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = canvasRect.rect.size;
        root.position = canvasRect.position;
        Vector3 parentScale = root.parent != null ? root.parent.lossyScale : Vector3.one;
        root.localScale = new Vector3(
            parentScale.x != 0f ? canvasRect.lossyScale.x / parentScale.x : 1f,
            parentScale.y != 0f ? canvasRect.lossyScale.y / parentScale.y : 1f, 1f);
    }

    // ---------------------------------------------------------------------------------
    // 인물 머리 위치 찾기
    // ---------------------------------------------------------------------------------
    // 스탠딩 그림의 불투명한 부분 중 맨 위 줄 근처의 가운데 = 머리 꼭대기.
    // 그림 파일은 보통 "읽기 금지"로 가져와져서 픽셀을 바로 못 읽으므로, 작게 복사해서(GPU) 읽는다.
    // 그림마다 한 번만 하고 결과를 기억한다.
    // 그림 안에서의 비율(0~1)로 기억한다. top = 머리 꼭대기 가운데, halfWidth = 머리 반폭.
    // halfWidth는 "얼굴을 피해서" 말풍선 자리를 잡을 때 쓴다 (FaceRectOnCanvas).
    private struct HeadInfo
    {
        public Vector2 top;
        public float halfWidth;
    }

    private static readonly Dictionary<Sprite, HeadInfo> headCache = new Dictionary<Sprite, HeadInfo>();

    private Vector2 HeadTopOnCanvas(Image standing)
    {
        var rt = standing.rectTransform;
        Rect r = rt.rect;
        Vector2 norm = MeasureHead(standing.sprite).top;   // 그림 안에서의 비율 (0~1)
        Vector3 world = rt.TransformPoint(new Vector3(r.xMin + r.width * norm.x, r.yMin + r.height * norm.y, 0f));
        Vector3 local = canvasRect.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private static HeadInfo MeasureHead(Sprite sprite)
    {
        if (headCache.TryGetValue(sprite, out var cached)) return cached;
        // 못 읽으면 그림 가운데 위쪽에 보통 크기의 머리가 있다고 친다.
        var result = new HeadInfo { top = new Vector2(0.5f, 0.85f), halfWidth = 0.09f };

        try
        {
            Texture2D tex = sprite.texture;
            Rect tr = sprite.textureRect;
            const int w = 160;
            int h = Mathf.Max(1, Mathf.RoundToInt(w * tr.height / tr.width));

            var rtex = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rtex, new Vector2(tr.width / tex.width, tr.height / tex.height),
                          new Vector2(tr.x / tex.width, tr.y / tex.height));
            var prev = RenderTexture.active;
            RenderTexture.active = rtex;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rtex);
            Color32[] px = read.GetPixels32();
            Destroy(read);

            // 맨 위에서부터 내려오며 불투명한 점이 몇 개 이상 있는 첫 줄을 찾는다
            int top = -1;
            for (int y = h - 1; y >= 0 && top < 0; y--)
            {
                int count = 0;
                for (int x = 0; x < w; x++) if (px[y * w + x].a > 60) count++;
                if (count >= 2) top = y;
            }
            if (top >= 0)
            {
                // 머리 가로 위치 = 맨 윗줄부터 조금 아래까지 불투명한 점들의 평균
                // 머리 반폭 = 같은 띠에서 가장 왼쪽 점과 가장 오른쪽 점까지의 절반
                //   (평균만으로는 머리가 얼마나 넓은지 알 수 없어서 따로 잰다)
                int band = Mathf.Max(3, Mathf.RoundToInt(h * 0.06f));
                float sum = 0f; int n = 0;
                int minX = w, maxX = -1;
                for (int y = top; y >= Mathf.Max(0, top - band); y--)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 60)
                        {
                            sum += x; n++;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                        }
                if (n > 0)
                {
                    result.top = new Vector2((sum / n + 0.5f) / w, (top + 0.5f) / h);
                    // 맨 윗줄은 머리카락 끝이라 좁다. 아래로 갈수록 넓어지므로 넉넉하게 본다.
                    if (maxX >= minX) result.halfWidth = Mathf.Max(0.05f, (maxX - minX + 1) * 0.5f / w);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SpeechBubble] 스탠딩 '{sprite.name}'의 머리 위치를 읽지 못해 그림 가운데 위쪽을 씁니다: {e.Message}");
        }

        headCache[sprite] = result;
        return result;
    }

    // ---------------------------------------------------------------------------------
    // 만들기
    // ---------------------------------------------------------------------------------
    private void Build()
    {
        root = (RectTransform)transform;

        // ----- 몸통: 픽셀 외곽선 위에 바탕을 한 칸 안쪽으로 겹친다 -----
        body = CreateBubbleBox("Body", root, OutlineColor, FillColor);

        // ----- 대사 글자 (원래 대화창과 같은 글꼴·크기·색·줄 간격) -----
        text = UIKit.CreateText(body, "Text", "", source.fontSize, FontStyles.Normal, source.color);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = source.lineSpacing;

        // ----- ▼ (다음 대사로 넘길 수 있을 때) -----
        indicator = CreateIndicator(body);

        // ----- 이름표: 둥근 알약 모양 (외곽선 + 바탕 + 이름), 글자 길이에 맞춰 늘어난다 -----
        nameTag = CreateNameTag(root, out nameText);

        UIFontHelper.ApplyToChildren(gameObject);
        // 대사 글자는 대화창과 똑같은 글꼴로 (같은 줄바꿈이 나오게)
        if (source.font != null) { text.font = source.font; nameText.font = source.font; }
        if (sourceIndicator != null && sourceIndicator.font != null) indicator.font = sourceIndicator.font;
    }

    // ---------------------------------------------------------------------------------
    // 말풍선 부품 - 본편 말풍선과 자료 보기의 독백 말풍선이 같이 쓴다 (모양이 갈라지지 않게)
    // ---------------------------------------------------------------------------------

    // 말풍선 상자: 외곽선 위에 바탕을 외곽선 두께만큼 안쪽으로 겹친다.
    // UIKit.AddBorder를 쓰지 않는 이유: 그건 곧은 선 4개라서 깎인 모서리 밖으로 네모 선이 튀어나온다.
    //
    // 둥근 모서리로 되돌리려면 PixelBoxSprite(...) 두 줄을 RoundedSprite(CornerRadius) /
    // RoundedSprite(CornerRadius - 1f) 로 바꾸고 아래 Stretch 두께를 1f로 돌리면 된다.
    public static RectTransform CreateBubbleBox(string name, Transform parent, Color outline, Color fillColor)
    {
        var box = NewImage(name, parent, PixelBoxSprite(PixelCut, PixelStep), outline);
        Sliced(box);

        // 바탕도 같은 계단 모양이라, 안쪽으로 들이면 테두리와 모서리가 나란히 간다.
        var fill = NewImage("Fill", box, PixelBoxSprite(PixelCut, PixelStep), fillColor);
        Sliced(fill);
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 레이아웃 그룹(이름표)에 끼지 않게
        UIKit.Stretch(fill, PixelBorder);
        return box;
    }

    // 이름표. 왼쪽 가운데가 기준점이라, 말풍선 왼쪽 위 모서리 높이에 놓으면 반쯤 걸친다.
    // 예전에는 둥근 알약 모양이었는데, 말풍선과 같은 계단 모서리로 맞췄다.
    // opaque: 뒤가 흰 종이일 때 바탕이 비치지 않게 (자료 보기)
    public static RectTransform CreateNameTag(Transform parent, out TMP_Text label, bool opaque = false)
    {
        var tag = CreateBubbleBox("NameTag", parent, NameOutlineColor,
                                  opaque ? UITheme.WithAlpha(NameFillColor, 1f) : NameFillColor);
        tag.pivot = new Vector2(0f, 0.5f);
        tag.sizeDelta = new Vector2(100f, NameTagHeight);
        var row = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(18, 18, 0, 0);
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        label = UIKit.CreateLabel(tag, "Name", "", NameFontSize, FontStyles.Bold, UITheme.Label);
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 4f;
        return tag;
    }

    // ▼ (오른쪽 아래 구석)
    public static TMP_Text CreateIndicator(Transform body)
    {
        var arrow = UIKit.CreateLabel(body, "Indicator", "▼", 16, FontStyles.Normal, UITheme.Accent);
        arrow.alignment = TextAlignmentOptions.Center;
        var rt = (RectTransform)arrow.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-14f, 10f);
        rt.sizeDelta = new Vector2(22f, 22f);
        return arrow;
    }

    // 본편 말풍선의 글꼴·크기·색·줄 간격을 그대로 물려준다. 본편 말풍선이 없는 씬이면 원래 값을 둔다.
    public static void CopyTextStyle(TMP_Text toText, TMP_Text toName, TMP_Text toIndicator)
    {
        if (current == null || current.text == null) return;
        toText.font = current.text.font;
        toText.fontSize = current.text.fontSize;
        toText.color = current.text.color;
        toText.lineSpacing = current.text.lineSpacing;
        toName.font = current.nameText.font;
        toIndicator.font = current.indicator.font;
    }

    private static RectTransform NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;   // 말풍선을 눌러도 대사가 넘어가게 (클릭은 원래대로 대화 진행)
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static void Sliced(RectTransform rt)
    {
        var img = rt.GetComponent<Image>();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
    }

    // =================================================================================
    // 픽셀 스타일 상자 (모서리가 계단처럼 깎인 흰 사각형)
    // =================================================================================
    // 그림 파일 없이 코드로 만들고 9-slice로 늘려 쓴다. 말풍선, 이름표, 오른쪽 위
    // AUTO/SKIP 판, 자료 보기의 독백 말풍선이 모두 이 한 장을 공유하므로 모양이 갈라지지 않는다.
    //
    // ===== 어떻게 그리나 =====
    // 모서리에서 몇 "칸"(step 단위) 떨어졌는지를 세고, 가로칸 + 세로칸이 cut보다 작으면
    // 비운다. 그러면 45도 계단이 생긴다. cut=2, step=4 일 때 왼쪽 위 모서리는 이렇게 된다:
    //
    //     y 0~3 : . . ■ ■ ■     (.=투명  ■=칠함)
    //     y 4~7 : . ■ ■ ■ ■
    //     y 8~  : ■ ■ ■ ■ ■
    //
    // ===== 왜 안티에일리어싱을 넣지 않나 =====
    // 아래 RoundedSprite는 경계의 알파를 0~1로 부드럽게 깎아 둥글게 보이게 한다.
    // 도트 스타일에서는 그 반투명 테두리가 "흐린 선"으로 보인다. 그래서 여기서는
    // 알파를 0 또는 1만 쓰고, 텍스처도 Point로 확대해 계단을 그대로 유지한다.
    private static readonly Dictionary<int, Sprite> pixelCache = new Dictionary<int, Sprite>();

    public static Sprite PixelBoxSprite(int cut, int step)
    {
        if (cut < 1) cut = 1;
        if (step < 1) step = 1;

        int key = cut * 1000 + step;
        if (pixelCache.TryGetValue(key, out var cached) && cached != null) return cached;

        int c = cut * step;        // 모서리에서 깎여 나가는 길이
        int S = c * 2 + 2;         // 가운데 2칸은 9-slice로 늘어나는 부분

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            // 이게 빠지면 늘릴 때 계단이 흐려진다 (도트 스타일의 핵심)
            filterMode = FilterMode.Point,
        };

        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // 가장 가까운 가장자리에서 몇 칸 떨어져 있나
                int bx = Mathf.Min(x, S - 1 - x) / step;
                int by = Mathf.Min(y, S - 1 - y) / step;
                bool inside = bx + by >= cut;
                px[y * S + x] = new Color(1f, 1f, 1f, inside ? 1f : 0f);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        // 9-slice 가장자리는 깎인 부분(c)까지. 가운데 2칸만 늘어난다.
        var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect, new Vector4(c, c, c, c));
        pixelCache[key] = sprite;
        return sprite;
    }

    // ===== 모서리가 둥근 흰 사각형을 코드로 만든다 (그림 파일 없이, 9-slice로 늘려 쓴다) =====
    // 픽셀 스타일로 바꾸기 전의 모양이다. 지금은 쓰이지 않지만, 둥근 모서리로 되돌릴 때
    // 필요하므로 남겨 둔다 (CreateBubbleBox 주석 참고).
    // 반지름마다 한 장씩 만들어 기억해 둔다.
    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    public static Sprite RoundedSprite(float radius)
    {
        int key = Mathf.RoundToInt(radius * 10f);
        if (roundedCache.TryGetValue(key, out var cached) && cached != null) return cached;

        int S = Mathf.CeilToInt(radius) * 2 + 4;
        float r = radius;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (S - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (S - r)));
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        float b = Mathf.Ceil(r) + 1f;
        var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        roundedCache[key] = sprite;
        return sprite;
    }
}
