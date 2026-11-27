using TMPro;
using UnityEngine;

// =====================================================================================
// 말풍선 배치 모드 - 에디터에서 Play 중에 줄마다 말풍선/대화창, 말풍선 위치를 고친다
// =====================================================================================
// 게임을 실제로 진행하면서, 어색한 줄만 그 자리에서 고친다. 고친 내용은 바로
// Assets/StreamingAssets/Stage/BubbleLayout.csv 에 저장되고(BubbleLayout.cs), git으로 공유된다.
//
//   F2  : 배치 모드 켜기 / 끄기  (화면 위에 안내가 뜬다)
//   끌기 : 말풍선을 원하는 자리로 옮긴다 (놓는 순간 "지금 서 있는 스탠딩"의 자리로 저장)
//   B   : 이 줄을 말풍선 ↔ 대화창으로 바꾼다 (모양은 줄 단위)
//   R   : 이 스탠딩의 자리를 지워 자동 배치로 되돌린다
//   대사 넘기기는 평소처럼 스페이스/엔터/말풍선 바깥 클릭
//
// 자리는 스탠딩 단위라, 한 인물에 대해 한 번만 잡으면 그 인물이 나오는 모든 장면·모든
// 대사가 같은 자리에 뜬다. 중심점은 스탠딩 한 종류당 하나뿐이다
// (BubbleLayout.cs 상단 설명 참고).
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
        // 지금 줄의 중심점 주인. 말하는 사람이 서 있으면 그 사람, 아니면 무대 구성.
        string anchorKey = AnchorKeyNow();

        if (hasLine && Input.GetKeyDown(KeyCode.R))
        {
            BubbleLayout.Clear(anchorKey);
            SpeechBubble.ForgetAutoAnchor(anchorKey);   // 기억해 둔 자동 자리도 잊고 다시 고르게
            dialogue.ReapplySpeechBubble();
        }

        // 끌어 놓으면 "이 스탠딩"의 자리로 저장된다. 열쇠 하나에 점 하나이므로 덮어쓰기가 된다.
        bool canDrag = bubble.IsShowing && hasLine && !string.IsNullOrEmpty(anchorKey);
        bubble.SetEditable(canDrag, center =>
        {
            string key = AnchorKeyNow();
            BubbleLayout.SetPosition(key, center);
            SpeechBubble.ForgetAutoAnchor(key);   // 이제 배치표 값을 쓰므로 기억은 지운다
        });

        UpdateHint(hasLine, scenario, index, line, anchorKey);
    }

    // 지금 보이는 줄의 중심점 주인 열쇠. DialogueSystem이 쓰는 것과 같은 값이어야 한다
    // (다르면 안내에 뜨는 열쇠와 실제로 저장되는 열쇠가 어긋난다).
    private string AnchorKeyNow()
    {
        var stage = StageController.Instance;
        if (stage == null) return StageController.NoStandingKey;

        var line = dialogue.CurrentLineForBubble;
        if (line == null) return stage.StageAnchorKey;

        string speaker = line.lineType == LineType.Narration ? "" : line.speaker;
        return stage.BubbleAnchorKey(speaker, line.talkerSlot);
    }

    private void SetEditing(bool on)
    {
        editing = on;
        if (!on) bubble.SetEditable(false, null);
        if (hintRoot == null && on) BuildHint();
        if (hintRoot != null) hintRoot.SetActive(on);
        lastHint = null;
    }

    private void UpdateHint(bool hasLine, string scenario, int index, DialogueLine line, string anchorKey)
    {
        string state;
        if (!hasLine) state = "고칠 대사가 없습니다";
        else
        {
            var mode = BubbleLayout.GetMode(scenario, index, line.sentence);
            bool fixedPos = BubbleLayout.TryGetPosition(anchorKey, out _);
            string now = bubble.IsShowing ? "말풍선" : "대화창";
            string shape = mode == BubbleLayout.Mode.Auto ? "자동" : "직접 정함";
            string where = fixedPos ? "직접 정한 자리" : "자동 자리";
            state = $"{scenario}  {index}번째 줄   ·   모양: {now}({shape})   ·   스탠딩: {anchorKey} ({where})";
        }
        string text = "말풍선 배치 모드   " + state
                    + "\n<size=80%>끌어서 이 스탠딩의 자리 저장   ·   B 말풍선↔대화창(이 줄만)   ·   R 이 스탠딩 자동으로   ·   F2 끄기</size>";
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
