using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 엔딩 카드 - 엔딩 대본을 다 본 뒤 타이틀로 돌아가기 전에 띄우는 마무리 화면
// =====================================================================================
// 엔딩 대본의 마지막 줄을 넘기면 GameFlowManager.FinishEnding()이 이 카드를 띄우고,
// 플레이어가 [타이틀로]를 누르면 그때 진행 상태를 비우고 타이틀로 돌아간다.
//
//   (화면이 0.5초 만에 검게 어두워진다)
//
//                       BAD END  ·  03
//                         검은 바다
//                           ────
//                       [  타이틀로  ]        ← 조금 늦게 나타난다
//
// ===== 엔딩 이름 바꾸는 법 =====
// 아래 Entries 표의 이름만 고치면 된다. 지금 이름은 임시로 붙인 것이다.
// 번호는 엔딩 목록에서의 순서(True=01 …)이며, 화면에 "BAD END · 03"처럼 함께 나온다.
//
// 게임 화면의 다른 UI(대화창, 퀵바, 조사 상세 등) 위에 확실히 덮이도록, 정렬 순서가 높은
// 전용 캔버스를 따로 만들어 그 위에 그린다.
public class EndingCardUI : MonoBehaviour
{
    private struct Entry
    {
        public string kind;   // TRUE END / NORMAL END / BAD END
        public string name;   // 엔딩 이름 (임시)
        public Entry(string kind, string name) { this.kind = kind; this.name = name; }
    }

    // ===== 엔딩 이름 (임시) =====
    private static readonly Dictionary<EndingType, Entry> Entries = new Dictionary<EndingType, Entry>
    {
        { EndingType.True,   new Entry("TRUE END",   "내가 해야만 하는 일") },
        { EndingType.Normal, new Entry("NORMAL END", "늦은 신고") },
        { EndingType.Bad_A,  new Entry("BAD END",    "붉은 손") },
        { EndingType.Bad_B,  new Entry("BAD END",    "새 사장") },
        { EndingType.Bad_C,  new Entry("BAD END",    "검은 바다") },
        { EndingType.Bad_D,  new Entry("BAD END",    "평범한 하루") },
        { EndingType.Bad_E,  new Entry("BAD END",    "타오르는 사무실") },
    };

    private const int CanvasSortOrder = 5000;
    private const float FadeSeconds = 0.5f;
    private const float TextDelaySeconds = 0.1f;
    private const float TextFadeSeconds = 0.4f;
    private const float ButtonDelaySeconds = 0.3f;   // 글자가 다 뜬 뒤

    private Action onDone;
    private bool done;
    private Button button;

    // 엔딩 카드를 띄운다. [타이틀로]를 누르면 onTitle이 불린다.
    public static void Show(EndingType ending, Action onTitle)
    {
        var go = new GameObject("EndingCard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var card = go.AddComponent<EndingCardUI>();
        card.onDone = onTitle;
        card.Build(ending);
    }

    private CanvasGroup backGroup;
    private CanvasGroup textGroup;
    private CanvasGroup buttonGroup;

    private void Build(EndingType ending)
    {
        Entries.TryGetValue(ending, out var entry);
        if (string.IsNullOrEmpty(entry.kind)) entry = new Entry("END", ending.ToString());
        int number = (int)ending;   // None=0, True=1, Normal=2, Bad_A=3 …

        // 검은 막 (뒤 화면 클릭도 막는다)
        var back = MinigameTheme.AddImage(transform, "Back", Color.black);
        back.raycastTarget = true;
        MinigameTheme.Stretch(back.rectTransform);
        backGroup = back.gameObject.AddComponent<CanvasGroup>();
        backGroup.alpha = 0f;

        // 글자 묶음
        var textRoot = new GameObject("Text", typeof(RectTransform));
        textRoot.transform.SetParent(transform, false);
        var trt = (RectTransform)textRoot.transform;
        MinigameTheme.Stretch(trt);
        textGroup = textRoot.AddComponent<CanvasGroup>();
        textGroup.alpha = 0f;
        textGroup.blocksRaycasts = false;

        bool bad = entry.kind == "BAD END";
        var kind = MinigameTheme.AddText(trt, "Kind", 20, bad ? MinigameTheme.Danger : MinigameTheme.Accent);
        MinigameTheme.Center(kind.rectTransform, new Vector2(800f, 30f), new Vector2(0f, 96f));
        kind.fontStyle = FontStyles.Bold;
        kind.characterSpacing = 28f;
        kind.text = $"{entry.kind}  ·  {number:00}";

        var title = MinigameTheme.AddText(trt, "Name", 56, MinigameTheme.TextBright);
        MinigameTheme.Center(title.rectTransform, new Vector2(1100f, 80f), new Vector2(0f, 26f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6f;
        title.text = entry.name;

        var rule = MinigameTheme.AddImage(trt, "Rule", MinigameTheme.Line);
        MinigameTheme.Center(rule.rectTransform, new Vector2(64f, 2f), new Vector2(0f, -36f));

        // [타이틀로]
        button = MinigameTheme.MakeButton(transform, "Btn_Title", "타이틀로", new Vector2(240f, 56f), new Vector2(0f, -140f), Finish);
        buttonGroup = button.gameObject.AddComponent<CanvasGroup>();
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = false;
        buttonGroup.blocksRaycasts = false;

        UIFontHelper.ApplyToChildren(gameObject);
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            backGroup.alpha = t / FadeSeconds;
            yield return null;
        }
        backGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(TextDelaySeconds);
        yield return Fade(textGroup, TextFadeSeconds);

        yield return new WaitForSecondsRealtime(ButtonDelaySeconds);
        buttonGroup.interactable = true;
        buttonGroup.blocksRaycasts = true;
        yield return Fade(buttonGroup, 0.25f);
    }

    private static IEnumerator Fade(CanvasGroup group, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / seconds;
            yield return null;
        }
        group.alpha = 1f;
    }

    // 버튼이 나타난 뒤에는 Enter로도 넘어갈 수 있다.
    private void Update()
    {
        if (done || buttonGroup == null || !buttonGroup.interactable) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Finish();
    }

    private void Finish()
    {
        if (done) return;
        done = true;
        var callback = onDone;
        onDone = null;
        callback?.Invoke();
    }
}
