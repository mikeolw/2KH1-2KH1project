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
// ===== 모양 (원래 대화창과 같은 톤) =====
// 꼬리 없이, 뒤가 살짝 비치는 어두운 바탕 + 얇은(1px) 청회색 외곽선 + 둥근 모서리.
// 이름은 말풍선 왼쪽 위에 걸친 둥근 알약 모양 이름표.
//
//      (머리)    ╭ 은사 ╮
//        ●     ╭──────────────────────────╮
//       /█\    │  자네, 여기서 뭐 하고 있나?  ▾│
//              ╰──────────────────────────╯
//
// ===== 위치 =====
// 스탠딩 그림은 대부분 1440x1080 화면 크기 그대로(인물이 그림 안의 제자리에 그려져 있음)라서,
// 그림에서 "불투명한 부분의 맨 위 = 머리"를 한 번 읽어 와(그림마다 기억) 그 옆에 말풍선을 놓는다.
// 인물이 화면 왼쪽이면 말풍선은 오른쪽에, 오른쪽이면 왼쪽에 놓인다.
public class SpeechBubble : MonoBehaviour
{
    // ===== 배치 (화면 1440x1080 기준, 화면 가운데가 0,0 / 위로 +) =====
    private const float MaxTextWidth = 560f;
    private const float MinBodyWidth = 260f;
    private const float PadX = 40f, PadTop = 34f, PadBottom = 28f;   // 위는 이름표가 걸치는 만큼 더 띄운다
    private const float GapFromHead = 110f;       // 머리 중심에서 말풍선 가장자리까지 가로 거리
    private const float MouthBelowTop = 0.10f;    // 입 높이 = 머리 맨 위에서 화면 높이의 10% 아래
    private const float ScreenMargin = 40f;
    private const float DialogueBoxTop = -240f;   // 말풍선이 이보다 아래로 내려가면 아래 대화창 자리(AUTO/SKIP)를 가린다
    private const float CornerRadius = 14f;
    private const float NameTagHeight = 34f;

    // 원래 대화창처럼 뒤가 살짝 비치는 바탕 + 얇은 외곽선
    public static readonly Color FillColor = UITheme.WithAlpha(UITheme.Panel, 0.82f);
    public static readonly Color OutlineColor = UITheme.WithAlpha(UITheme.Accent, 0.38f);
    private static readonly Color NameFillColor = UITheme.WithAlpha(UITheme.Panel, 0.95f);
    private static readonly Color NameOutlineColor = UITheme.WithAlpha(UITheme.Accent, 0.6f);

    // 따라 그릴 원래 대화창의 글자와 ▼
    private TMP_Text source;
    private TMP_Text sourceIndicator;

    private RectTransform canvasRect;
    private RectTransform root, body, nameTag;
    private TMP_Text text, nameText, indicator;

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
        return bubble;
    }

    // ---------------------------------------------------------------------------------
    // 보이기 / 숨기기
    // ---------------------------------------------------------------------------------

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

    // 지금 말풍선 가운데 위치 (화면 가운데 0,0 / 위로 +)
    public Vector2 Center => body != null ? body.anchoredPosition : Vector2.zero;

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
        nameTag.anchoredPosition = new Vector2(center.x - size.x * 0.5f + 24f, center.y + size.y * 0.5f);
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
        nameTag.anchoredPosition = new Vector2(center.x - size.x * 0.5f + 24f, center.y + size.y * 0.5f);
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
    private static readonly Dictionary<Sprite, Vector2> headCache = new Dictionary<Sprite, Vector2>();

    private Vector2 HeadTopOnCanvas(Image standing)
    {
        var rt = standing.rectTransform;
        Rect r = rt.rect;
        Vector2 norm = HeadTopNormalized(standing.sprite);   // 그림 안에서의 비율 (0~1)
        Vector3 world = rt.TransformPoint(new Vector3(r.xMin + r.width * norm.x, r.yMin + r.height * norm.y, 0f));
        Vector3 local = canvasRect.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private static Vector2 HeadTopNormalized(Sprite sprite)
    {
        if (headCache.TryGetValue(sprite, out var cached)) return cached;
        Vector2 result = new Vector2(0.5f, 0.85f);   // 못 읽으면 그림 가운데 위쪽

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
                int band = Mathf.Max(3, Mathf.RoundToInt(h * 0.06f));
                float sum = 0f; int n = 0;
                for (int y = top; y >= Mathf.Max(0, top - band); y--)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 60) { sum += x; n++; }
                if (n > 0) result = new Vector2((sum / n + 0.5f) / w, (top + 0.5f) / h);
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

        // ----- 몸통: 외곽선(1px) 위에 바탕을 1px 안쪽으로 겹친다 -----
        body = NewImage("Body", root, RoundedSprite(CornerRadius), OutlineColor);
        Sliced(body);
        var fill = NewImage("Fill", body, RoundedSprite(CornerRadius - 1f), FillColor);
        Sliced(fill);
        UIKit.Stretch(fill, 1f);

        // ----- 대사 글자 (원래 대화창과 같은 글꼴·크기·색·줄 간격) -----
        text = UIKit.CreateText(body, "Text", "", source.fontSize, FontStyles.Normal, source.color);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = source.lineSpacing;

        // ----- ▼ (다음 대사로 넘길 수 있을 때) -----
        indicator = UIKit.CreateLabel(body, "Indicator", "▼", 16, FontStyles.Normal, UITheme.Accent);
        indicator.alignment = TextAlignmentOptions.Center;
        var irt = (RectTransform)indicator.transform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(1f, 0f);
        irt.anchoredPosition = new Vector2(-14f, 10f);
        irt.sizeDelta = new Vector2(22f, 22f);

        // ----- 이름표: 둥근 알약 모양 (외곽선 + 바탕 + 이름), 글자 길이에 맞춰 늘어난다 -----
        nameTag = NewImage("NameTag", root, RoundedSprite(NameTagHeight * 0.5f), NameOutlineColor);
        Sliced(nameTag);
        nameTag.pivot = new Vector2(0f, 0.5f);
        nameTag.sizeDelta = new Vector2(100f, NameTagHeight);
        var row = nameTag.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(18, 18, 0, 0);
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        nameTag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var nameFill = NewImage("Fill", nameTag, RoundedSprite(NameTagHeight * 0.5f - 1f), NameFillColor);
        Sliced(nameFill);
        nameFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 이름 글자 배치에 끼지 않게
        UIKit.Stretch(nameFill, 1f);

        nameText = UIKit.CreateLabel(nameTag, "Name", "", 19, FontStyles.Bold, UITheme.Label);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.characterSpacing = 4f;

        UIFontHelper.ApplyToChildren(gameObject);
        // 대사 글자는 대화창과 똑같은 글꼴로 (같은 줄바꿈이 나오게)
        if (source.font != null) { text.font = source.font; nameText.font = source.font; }
        if (sourceIndicator != null && sourceIndicator.font != null) indicator.font = sourceIndicator.font;
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

    // ===== 모서리가 둥근 흰 사각형을 코드로 만든다 (그림 파일 없이, 9-slice로 늘려 쓴다) =====
    // 반지름마다 한 장씩 만들어 기억해 둔다. 말풍선과 같은 모양을 쓰는 곳(오른쪽 위 AUTO/SKIP 판)에서도 쓴다.
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
