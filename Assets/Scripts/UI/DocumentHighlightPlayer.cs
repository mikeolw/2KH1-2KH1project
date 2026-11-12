using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static UIKit;

// =====================================================================================
// 서류 형광펜 재생기 - 자료를 읽으면 밑줄이 그어지고 차재훈이 중얼거린다
// =====================================================================================
// DocumentViewerController가 서류를 펼칠 때 같이 붙는다. 혼자서는 아무것도 하지 않고,
// 뷰어가 SetPage / Advance / Stop 을 불러준다.
//
// ===== 한 군데를 읽는 순서 =====
//   1) 그 줄이 화면 가운데쯤 오도록 자료를 굴린다 (스스로 찾기 어려우니 데려다 준다)
//   2) 형광펜이 왼쪽에서 오른쪽으로 그어진다
//   3) 다 그어질 때쯤 말풍선이 뜨고 글자가 한 자씩 찍힌다
//   4) 누르면 다음 줄로. 글이 나오는 중에 누르면 먼저 한 번에 다 보여준다
//
// ===== 두 번째로 펼쳤을 때 =====
// 이미 읽은 곳은 처음부터 그어진 채로 보이고 말풍선은 뜨지 않는다. 읽을 곳이 남아 있으면
// 거기서부터 이어서 재생한다. (어디까지 읽었는지는 DocumentHighlights가 기억한다)
//
// ===== 어디에 그리는가 =====
// 형광펜 칸은 "자료 그림의 자식"으로 붙인다. 그래서 확대하거나 끌어 옮겨도 종이에
// 그어진 것처럼 같이 움직인다. 좌표는 비율(0~1)이라 그림 해상도가 바뀌어도 그대로 맞는다.
// 말풍선은 반대로 "화면"에 붙는다. 종이를 따라 확대되면 글자를 읽을 수 없기 때문이다.
public class DocumentHighlightPlayer : MonoBehaviour
{
    // ===== 형광펜 색 =====
    // 겹치기(multiply) 셰이더로 그린다. 흰 종이만 노래지고 검은 글자는 검은 채로 남는다.
    // 알파는 투명도가 아니라 "진하기"다. 0.8이면 종이가 또렷한 형광 노랑이 된다.
    // (Assets/Shaders/UIMultiply.shader 윗부분 참고)
    private static readonly Color PenColor = UITheme.HighlightPen;

    // 셰이더를 못 찾았을 때 쓸 예비 색. 예전처럼 위에 덮는 방식이라 글자가 조금 흐려지지만
    // 아예 안 보이는 것보다는 낫다. (보통은 쓰이지 않는다)
    private static readonly Color PenFallbackColor = UITheme.HighlightPenFallback;
    private const string MultiplyShader = "2KH1/UI Multiply";
    private static Material penMaterial;
    private static bool penMaterialTried;

    private static Material PenMaterial()
    {
        if (penMaterialTried) return penMaterial;
        penMaterialTried = true;
        Shader sh = Shader.Find(MultiplyShader);
        if (sh == null)
        {
            Debug.LogWarning($"[DocumentHighlightPlayer] '{MultiplyShader}' 셰이더를 못 찾아 " +
                             "형광펜을 예전 방식(위에 덮기)으로 그립니다. " +
                             "Assets/Shaders/UIMultiply.shader 가 있는지, " +
                             "Project Settings > Graphics > Always Included Shaders 에 들어 있는지 보세요.");
            return null;
        }
        penMaterial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        return penMaterial;
    }
    private const float PenPadX = 0.004f;      // 칸 좌우로 살짝 넉넉하게 (비율)
    private const float PenDrawTime = 0.42f;   // 한 군데를 다 긋는 데 걸리는 시간(초)
    private const float ScrollTime = 0.26f;    // 그 줄로 자료를 굴리는 시간
    private const float TypeInterval = 0.03f;  // 글자 한 자 간격
    private const float LineBreakPause = 0.14f;
    // 말풍선
    private const float BubbleMaxW = 470f, BubblePadX = 26f, BubblePadTop = 26f, BubblePadBottom = 20f;
    private const float BubbleFont = 24f, NameFont = 19f;
    private const float BubbleGap = 20f;       // 형광펜 칸과 말풍선 사이 간격

    private DocumentViewerController viewer;
    private RectTransform penLayer;            // 자료 그림의 자식 (종이에 붙는다)
    private RectTransform bubble, bubbleBody;
    private TMP_Text bubbleText, bubbleName, bubbleArrow;

    private readonly List<Image> pens = new List<Image>();
    private IReadOnlyList<DocumentHighlights.Spot> spots = System.Array.Empty<DocumentHighlights.Spot>();
    private int index = -1;                    // 지금 읽고 있는 칸 (-1이면 아직 시작 전)
    private Coroutine running;
    private bool typing;                       // 글자가 찍히는 중
    private string fullText = "";
    private bool bubbleWanted;                 // 지금 줄의 말풍선을 띄워야 하는가 (칸 밖이라 잠시 숨겨도 true)

    // 뷰어가 "지금 누르면 닫을까, 다음 줄로 갈까, 다음 장으로 갈까"를 판단할 때 쓴다
    public bool HasSpots => spots.Count > 0;
    public bool HasPending => spots.Count > 0 && index < spots.Count - 1;
    public bool IsBusy => running != null || typing;

    public static DocumentHighlightPlayer Attach(DocumentViewerController owner, RectTransform screenBox)
    {
        var player = owner.gameObject.AddComponent<DocumentHighlightPlayer>();
        player.viewer = owner;
        player.BuildBubble(screenBox);
        return player;
    }

    // ---------------------------------------------------------------------------------
    // 장을 펼칠 때 (DocumentViewerController.RefreshPage에서 부른다)
    // ---------------------------------------------------------------------------------
    public void SetPage(Sprite sprite, RectTransform pageRect)
    {
        StopAll();
        spots = DocumentHighlights.For(sprite != null ? sprite.name : null);
        BuildPenLayer(pageRect);
        index = -1;

        if (spots.Count == 0) { HideBubble(); return; }

        // 이미 읽은 곳은 처음부터 그어둔다
        for (int i = 0; i < spots.Count; i++)
        {
            if (!DocumentHighlights.IsRevealed(spots[i])) break;
            SetPenFill(i, 1f);
            index = i;
        }

        HideBubble();
        // 아직 안 읽은 곳이 남아 있으면 한 박자 뒤에 이어서 재생한다.
        // (뷰어가 크기·위치를 다 잡은 다음이라야 굴릴 목표를 제대로 계산할 수 있다)
        if (index < spots.Count - 1) running = StartCoroutine(PlayNextAfterLayout());
    }

    private IEnumerator PlayNextAfterLayout()
    {
        yield return null;                 // 한 프레임 기다려 레이아웃이 끝나게
        running = null;
        Advance();
    }

    // ---------------------------------------------------------------------------------
    // 다음 줄로 (클릭 / 스페이스 / 엔터)
    // ---------------------------------------------------------------------------------
    // 돌려주는 값: 내가 처리했으면 true. false면 뷰어가 원래 하던 일(닫기)을 한다.
    public bool Advance()
    {
        if (spots.Count == 0) return false;

        // 글이 다 안 나왔으면 첫 누름은 "한 번에 다 보여주기"로 쓴다
        if (IsBusy) { FinishNow(); return true; }
        if (index >= spots.Count - 1) return false;   // 다 읽었다 - 이제 누르면 뷰어가 닫힌다

        index++;
        var spot = spots[index];
        DocumentHighlights.MarkRevealed(spot);
        running = StartCoroutine(PlaySpot(index, spot));
        return true;
    }

    private IEnumerator PlaySpot(int i, DocumentHighlights.Spot spot)
    {
        HideBubble();
        // 글자를 찍기 전에 미리 넣어둔다. 긋는 도중에 눌러서 건너뛰면 FinishNow가
        // 이 값을 쓰는데, 나중에 넣으면 직전 줄의 대사가 튀어나온다.
        fullText = spot.text ?? "";

        // 1) 그 줄이 보이도록 자료를 굴린다
        float centerT = spot.rect.y + spot.rect.height * 0.5f;
        yield return viewer.ScrollToPageFractionRoutine(centerT, ScrollTime);

        // 2) 형광펜을 왼쪽에서 오른쪽으로
        float t = 0f;
        while (t < PenDrawTime)
        {
            t += Time.unscaledDeltaTime;
            SetPenFill(i, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / PenDrawTime)));
            yield return null;
        }
        SetPenFill(i, 1f);

        running = null;

        // 3) 말풍선. 대사가 없는 칸(드라이브 파일이 없거나 비워둔 칸)은 클릭을 기다리지 않고
        //    바로 다음 칸을 긋는다. 대사가 있는 칸이 나오거나 끝까지 갈 때까지 이어진다.
        if (!spot.HasText)
        {
            Advance();
            yield break;
        }
        ShowBubbleAt(i, spot.speaker);
        yield return StartCoroutine(TypeOut(fullText));
    }

    private IEnumerator TypeOut(string text)
    {
        typing = true;
        bubbleText.text = "";
        if (bubbleArrow != null) bubbleArrow.gameObject.SetActive(false);

        for (int i = 0; i < text.Length; i++)
        {
            bubbleText.text += text[i];
            yield return new WaitForSecondsRealtime(text[i] == '\n' ? LineBreakPause : TypeInterval);
        }

        typing = false;
        if (bubbleArrow != null) bubbleArrow.gameObject.SetActive(true);
    }

    // 긋는 중이든 글자가 찍히는 중이든, 한 번에 끝낸 상태로 만든다
    private void FinishNow()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        StopAllCoroutines();
        typing = false;

        if (index >= 0 && index < spots.Count)
        {
            SetPenFill(index, 1f);
            var spot = spots[index];
            if (spot.HasText)
            {
                ShowBubbleAt(index, spot.speaker);
                bubbleText.text = spot.text;
                if (bubbleArrow != null) bubbleArrow.gameObject.SetActive(true);
            }
            else if (index < spots.Count - 1)
            {
                // 대사 없는 칸을 건너뛰는 중이었다면 멈추지 말고 다음 칸으로 이어간다
                Advance();
            }
        }
    }

    public void StopAll()
    {
        StopAllCoroutines();
        running = null;
        typing = false;
        HideBubble();
    }

    // 뷰어를 닫을 때
    public void Close()
    {
        StopAll();
        spots = System.Array.Empty<DocumentHighlights.Spot>();
        index = -1;
    }

    // ---------------------------------------------------------------------------------
    // 형광펜 칸 만들기
    // ---------------------------------------------------------------------------------
    private void BuildPenLayer(RectTransform pageRect)
    {
        if (penLayer == null || penLayer.parent != pageRect)
        {
            if (penLayer != null) Destroy(penLayer.gameObject);
            var go = new GameObject("HighlightLayer", typeof(RectTransform));
            go.transform.SetParent(pageRect, false);
            penLayer = (RectTransform)go.transform;
            Stretch(penLayer);                       // 그림 전체를 덮는다
        }

        foreach (var p in pens) if (p != null) Destroy(p.gameObject);
        pens.Clear();

        for (int i = 0; i < spots.Count; i++)
        {
            var go = new GameObject("Pen" + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(penLayer, false);
            var rt = (RectTransform)go.transform;

            // ===== 비율 좌표를 앵커로 =====
            // 앵커로 붙여두면 그림이 커지든 작아지든 칸이 알아서 따라간다.
            // CSV의 Y는 "위에서부터"인데 유니티 앵커는 "아래에서부터"라 1에서 빼 준다.
            Rect r = spots[i].rect;
            float x0 = Mathf.Clamp01(r.x - PenPadX);
            float x1 = Mathf.Clamp01(r.x + r.width + PenPadX);
            rt.anchorMin = new Vector2(x0, Mathf.Clamp01(1f - (r.y + r.height)));
            rt.anchorMax = new Vector2(x1, Mathf.Clamp01(1f - r.y));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);        // 왼쪽부터 그어지게

            var img = go.GetComponent<Image>();
            Material mat = PenMaterial();
            if (mat != null) { img.material = mat; img.color = PenColor; }
            else img.color = PenFallbackColor;
            img.raycastTarget = false;               // 휠·끌기는 그림이 받아야 한다
            rt.localScale = new Vector3(0f, 1f, 1f); // 아직 안 그어진 상태
            pens.Add(img);
        }
    }

    private void SetPenFill(int i, float v)
    {
        if (i < 0 || i >= pens.Count || pens[i] == null) return;
        pens[i].rectTransform.localScale = new Vector3(Mathf.Clamp01(v), 1f, 1f);
    }

    // ---------------------------------------------------------------------------------
    // 말풍선
    // ---------------------------------------------------------------------------------
    // 모양은 SpeechBubble.cs와 똑같이 맞춘다(둥근 모서리, 바탕 82%, 청회색 외곽선, 이름표).
    // 다만 저 클래스는 아래 대화창의 글자를 따라 그리게 되어 있어서, 여기서는 쓰지 않고
    // 같은 색·같은 모양으로 따로 만든다.
    private void BuildBubble(RectTransform screenBox)
    {
        var go = new GameObject("ThoughtBubble", typeof(RectTransform));
        go.transform.SetParent(screenBox, false);
        bubble = (RectTransform)go.transform;
        bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
        bubble.pivot = new Vector2(0f, 1f);          // 왼쪽 위를 기준으로 놓는다

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Image));
        bodyGo.transform.SetParent(bubble, false);
        bubbleBody = (RectTransform)bodyGo.transform;
        bubbleBody.anchorMin = bubbleBody.anchorMax = new Vector2(0f, 1f);
        bubbleBody.pivot = new Vector2(0f, 1f);
        var bodyImg = bodyGo.GetComponent<Image>();
        bodyImg.sprite = SpeechBubble.RoundedSprite(18f);
        bodyImg.type = Image.Type.Sliced;
        bodyImg.color = SpeechBubble.FillColor;
        bodyImg.raycastTarget = false;
        AddBorder(bubbleBody, SpeechBubble.OutlineColor, 1f);

        bubbleText = CreateText(bubbleBody, "Text", "", BubbleFont, FontStyles.Normal, UITheme.Body);
        bubbleText.alignment = TextAlignmentOptions.TopLeft;
        bubbleText.raycastTarget = false;

        // 이름표 (말풍선 왼쪽 위에 걸친다)
        var nameGo = new GameObject("NameTag", typeof(RectTransform), typeof(Image));
        nameGo.transform.SetParent(bubbleBody, false);
        var nameRt = (RectTransform)nameGo.transform;
        nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 1f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.anchoredPosition = new Vector2(22f, 0f);
        var nameImg = nameGo.GetComponent<Image>();
        nameImg.sprite = SpeechBubble.RoundedSprite(17f);
        nameImg.type = Image.Type.Sliced;
        nameImg.color = UITheme.WithAlpha(UITheme.Panel, 0.95f);
        nameImg.raycastTarget = false;
        AddBorder(nameRt, UITheme.WithAlpha(UITheme.Accent, 0.6f), 1f);
        bubbleName = CreateText(nameRt, "Name", DocumentHighlights.DefaultSpeaker, NameFont, FontStyles.Normal, UITheme.Label);
        bubbleName.alignment = TextAlignmentOptions.Center;
        bubbleName.raycastTarget = false;
        Stretch(bubbleName.rectTransform);

        // 다 나왔다는 표시 ▼
        bubbleArrow = CreateText(bubbleBody, "Indicator", "▼", 16f, FontStyles.Normal, UITheme.Accent);
        bubbleArrow.alignment = TextAlignmentOptions.BottomRight;
        bubbleArrow.raycastTarget = false;

        bubble.gameObject.SetActive(false);
    }

    private void ShowBubbleAt(int i, string speaker)
    {
        if (bubble == null || i < 0 || i >= pens.Count || pens[i] == null) return;

        // 이 줄의 말풍선을 "보여주고 싶은 상태"로 기억한다. 아래에서 자료 칸 밖이라 잠시
        // 숨기더라도, 다시 굴려서 칸 안으로 돌아오면 Reposition이 이 값을 보고 다시 띄운다.
        bubbleWanted = true;
        bubbleName.text = string.IsNullOrEmpty(speaker) ? DocumentHighlights.DefaultSpeaker : speaker;
        bubble.gameObject.SetActive(true);

        // ----- 크기 -----
        float textW = BubbleMaxW - BubblePadX * 2f;
        bubbleText.text = fullText;                       // 다 나왔을 때 크기로 미리 잡는다
        bubbleText.ForceMeshUpdate();
        Vector2 pref = bubbleText.GetPreferredValues(fullText, textW, 0f);
        float w = Mathf.Min(BubbleMaxW, pref.x + BubblePadX * 2f);
        float h = pref.y + BubblePadTop + BubblePadBottom;
        bubbleBody.sizeDelta = new Vector2(w, h);
        PlaceTopLeft(bubbleText.rectTransform, BubblePadX, BubblePadTop, w - BubblePadX * 2f, pref.y);
        PlaceTopLeft(bubbleArrow.rectTransform, w - 44f, h - 26f, 30f, 18f);

        var nameRt = (RectTransform)bubbleName.transform.parent;
        nameRt.sizeDelta = new Vector2(bubbleName.GetPreferredValues(bubbleName.text).x + 28f, 32f);

        // ----- 위치 -----
        // 형광펜 칸이 화면 어디에 있는지 구해서(확대·스크롤까지 반영된 실제 위치) 그 위에 놓는다.
        Vector2 center, size;
        if (!viewer.PenRectOnScreenBox(pens[i].rectTransform, out center, out size))
        {
            bubble.gameObject.SetActive(false);
            return;
        }

        // 자료 칸 밖으로 밀려나 안 보이는 줄이면 말풍선도 띄우지 않는다
        if (!viewer.IsInsideViewport(center)) { bubble.gameObject.SetActive(false); return; }

        float x = center.x - size.x * 0.25f;
        float y = center.y + size.y * 0.5f + BubbleGap + h;    // 칸 위쪽
        if (y > viewer.ViewportTop) y = center.y - size.y * 0.5f - BubbleGap;   // 위가 좁으면 아래로
        bubble.anchoredPosition = viewer.ClampBubble(new Vector2(x, y), new Vector2(w, h));
    }

    private void HideBubble()
    {
        bubbleWanted = false;
        if (bubble != null) bubble.gameObject.SetActive(false);
    }

    // 확대하거나 굴렸을 때 말풍선이 따라오게 (뷰어가 매번 부른다)
    //
    // ShowBubbleAt은 크기를 재려고 말풍선 글을 "다 나온 문장"으로 바꿔 넣는다. 글자가 한 자씩
    // 찍히는 중에 그대로 두면, 문장이 통째로 나온 뒤에 TypeOut이 남은 글자를 또 이어 붙여서
    // 독백이 두 번 겹쳐 보였다. 그래서 지금까지 찍혀 있던 글을 기억해 두었다가 되돌린다.
    public void Reposition()
    {
        if (bubble == null || !bubbleWanted) return;
        if (index < 0 || index >= spots.Count) return;
        string shown = bubbleText.text;
        ShowBubbleAt(index, spots[index].speaker);
        bubbleText.text = typing ? shown : fullText;
    }
}
