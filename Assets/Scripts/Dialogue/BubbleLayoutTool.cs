using TMPro;
using UnityEngine;

// =====================================================================================
// 말풍선 배치 모드 - 에디터에서 Play 중에 줄마다 말풍선/대화창, 말풍선 위치를 고친다
// =====================================================================================
// 게임을 실제로 진행하면서, 어색한 줄만 그 자리에서 고친다. 고친 내용은 바로
// Assets/StreamingAssets/Stage/BubbleLayout.csv 에 저장되고(BubbleLayout.cs), git으로 공유된다.
//
//   F2  : 배치 모드 켜기 / 끄기  (화면 위에 안내가 뜬다)
//   끌기 : 말풍선을 원하는 자리로 옮긴다 (놓는 순간 저장)
//   B   : 이 줄을 말풍선 ↔ 대화창으로 바꾼다
//   R   : 이 줄을 자동 규칙으로 되돌린다
//   대사 넘기기는 평소처럼 스페이스/엔터/말풍선 바깥 클릭
//
// 출시 빌드에는 들어가지 않는다 (에디터 전용).
#if UNITY_EDITOR
public class BubbleLayoutTool : MonoBehaviour
{
    private DialogueSystem dialogue;
    private SpeechBubble bubble;
    private bool editing;
    private GameObject hintRoot;
    private TMP_Text hintText;
    private string lastHint;

    public void Init(DialogueSystem owner, SpeechBubble speechBubble)
    {
        dialogue = owner;
        bubble = speechBubble;
    }

    private void Update()
    {
        if (dialogue == null || bubble == null) return;

        if (Input.GetKeyDown(KeyCode.F2)) SetEditing(!editing);
        if (!editing) return;

        var line = dialogue.CurrentLineForBubble;
        string scenario = dialogue.CurrentScenarioCsv;
        int index = dialogue.ShownLineIndex;
        bool hasLine = line != null && index >= 0 && !string.IsNullOrEmpty(scenario);

        if (hasLine && Input.GetKeyDown(KeyCode.B))
        {
            // 지금 보이는 모양의 반대로 바꾼다
            BubbleLayout.SetMode(scenario, index, line.sentence, bubble.IsShowing ? BubbleLayout.Mode.Box : BubbleLayout.Mode.Bubble);
            dialogue.ReapplySpeechBubble();
        }
        if (hasLine && Input.GetKeyDown(KeyCode.R))
        {
            BubbleLayout.Remove(scenario, index);
            dialogue.ReapplySpeechBubble();
        }

        bubble.SetEditable(bubble.IsShowing && hasLine, center =>
        {
            var l = dialogue.CurrentLineForBubble;
            if (l != null) BubbleLayout.SetPosition(dialogue.CurrentScenarioCsv, dialogue.ShownLineIndex, l.sentence, center);
        });

        UpdateHint(hasLine, scenario, index, line);
    }

    private void SetEditing(bool on)
    {
        editing = on;
        if (!on) bubble.SetEditable(false, null);
        if (hintRoot == null && on) BuildHint();
        if (hintRoot != null) hintRoot.SetActive(on);
        lastHint = null;
    }

    private void UpdateHint(bool hasLine, string scenario, int index, DialogueLine line)
    {
        string state;
        if (!hasLine) state = "고칠 대사가 없습니다";
        else
        {
            var mode = BubbleLayout.GetMode(scenario, index, line.sentence);
            bool fixedPos = BubbleLayout.TryGetPosition(scenario, index, line.sentence, out _);
            string now = bubble.IsShowing ? "말풍선" : "대화창";
            string how = mode == BubbleLayout.Mode.Auto ? "자동" : fixedPos ? "직접 정함 · 위치 고정" : "직접 정함";
            state = $"{scenario}  {index}번째 줄   ·   지금: {now} ({how})";
        }
        string text = "말풍선 배치 모드   " + state + "\n<size=80%>끌어서 위치 저장   ·   B 말풍선↔대화창   ·   R 자동으로   ·   F2 끄기</size>";
        if (text == lastHint) return;
        lastHint = text;
        hintText.text = text;
    }

    private void BuildHint()
    {
        var canvas = UILayers.RootCanvas();
        if (canvas == null) return;
        hintRoot = new GameObject("BubbleLayoutHint", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        hintRoot.transform.SetParent(canvas.transform, false);
        UILayers.Place(hintRoot, UILayer.Timer);   // 다른 UI에 가리지 않게
        var rt = (RectTransform)hintRoot.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -12f);
        rt.sizeDelta = new Vector2(960f, 64f);
        var bg = hintRoot.GetComponent<UnityEngine.UI.Image>();
        bg.color = UITheme.WithAlpha(UITheme.Panel, 0.9f);
        bg.raycastTarget = false;
        UIKit.AddBorder(rt, UITheme.Accent);

        hintText = UIKit.CreateText(hintRoot.transform, "Text", "", 18, FontStyles.Normal, UITheme.Title);
        hintText.alignment = TextAlignmentOptions.Center;
        UIKit.Stretch(hintText.rectTransform, 6f);
        UIFontHelper.ApplyToChildren(hintRoot);
    }
}
#endif
