using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UIKit;   // AddBorder (UIKit.cs)

// =====================================================================================
// 선택지 화면 모양 (청회색 테마)
// =====================================================================================
// 선택지는 세 곳에서 띄운다 - 모두 DialogueSystem의 choicePanel/choiceContainer/choiceButtonPrefab을
// 같이 쓴다.
//   - DialogueSystem.ShowChoices()             : 대본 끝의 선택지
//   - InvestigationController.ShowTalkChoices() : 조사 중 대화 오브젝트의 선택지
//   - DeductionController.ShowCurrentStep()     : 추리 파트 보기
// 세 곳을 다 고치는 대신, 선택지 패널에 이 부품을 붙여 "버튼이 새로 생기면 모양을 입힌다".
// 씬 파일과 ChoiceButton 프리팹은 건드리지 않는다(팀원끼리 씬 충돌 방지).
//
//   ┌───────────────────────────────────────────┐
//   │               첫 번째 보기                  │   ← 마우스 올림: 테두리/왼쪽 막대 강조
//   └───────────────────────────────────────────┘
//   ┌───────────────────────────────────────────┐
//   │               두 번째 보기                  │
//   └───────────────────────────────────────────┘
//   (화면 정가운데, 글자는 가운데 정렬)
//
// 화면 전체를 어둡게 하되, 그 막을 대화창 "뒤"에 깔아서 대화창의 질문은 그대로 읽히게 한다.
// 숫자키 1~9로도 위에서부터 차례로 고를 수 있다.
public class ChoicePanelStyle : MonoBehaviour
{
    // ===== 색 (UITheme.cs) =====
    // 버튼 바탕만 이 화면에 맞게 투명도를 조금 다르게 쓴다 (뒤 장면이 살짝 비치게).
    private static readonly Color FillColor = UITheme.WithAlpha(UITheme.Panel, 0.92f);
    private static readonly Color FillHover = UITheme.WithAlpha(UITheme.SurfaceHover, 0.96f);

    // ===== 배치 (화면 1440x1080 기준) =====
    private const float Width = 760f;
    private const float RowHeight = 64f;
    private const float Spacing = 12f;
    private const float TextPadding = 32f;

    // 말풍선을 피해 비켜 놓을 때 띄우는 간격과, 화면 가장자리에서 남겨둘 여백.
    private const float AvoidGap = 24f;
    private const float ScreenEdge = 24f;

    // 뒤 화면을 어둡게 하는 막의 진하기. 선택지가 사라질 때 이 값에서 0까지 같이 흐려진다.
    private const float DimAlpha = 0.55f;

    // 나타날 때 / 사라질 때 걸리는 시간(초).
    private const float FadeInSeconds = 0.2f;
    private const float FadeOutSeconds = 0.25f;

    private GameObject dim;
    private RectTransform panelRect;   // 선택지 패널 자신 (이 부품은 그 안의 container에 붙어 있다)
    private Image dimImage;
    private CanvasGroup group;
    private Coroutine fadeRoutine;
    private bool fadingOut;   // 사라지는 중에는 클릭·숫자키를 받지 않는다

    // DialogueSystem이 시작할 때 한 번 부른다. 이미 붙어 있으면 그대로 둔다.
    public static void Attach(GameObject panel, Transform container, GameObject dialoguePanel)
    {
        if (panel == null || container == null) return;
        if (container.GetComponent<ChoicePanelStyle>() != null) return;
        var style = container.gameObject.AddComponent<ChoicePanelStyle>();
        style.Setup(panel, dialoguePanel);
    }

    private void Setup(GameObject panel, GameObject dialoguePanel)
    {
        // ----- 패널 자리: 화면 정가운데 -----
        // (말풍선과 겹치는 자리면 PlaceClearOf()가 위나 아래로 비켜 놓는다)
        var rt = (RectTransform)panel.transform;
        panelRect = rt;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);            // 가운데 기준 - 선택지가 많아지면 위아래로 같이 늘어난다
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(Width, rt.sizeDelta.y);

        // 예전 흰 반투명 바탕은 없앤다 (Linear 색 공간이라 알파가 조금만 있어도 회색 상자로 보인다 → 0)
        var bg = panel.GetComponent<Image>();
        if (bg != null) { bg.sprite = null; bg.color = new Color(0f, 0f, 0f, 0f); }

        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        // 선택지도 "창"으로 등록한다 - 떠 있는 동안 대사를 넘기지 않는다. Esc로는 닫히지 않는다
        // (반드시 하나를 골라야 하므로). 위에 수첩 등을 열면 Esc는 그쪽부터 닫는다.
        ModalStack.Register(panel, null);

        group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();

        // ----- 화면을 어둡게 하는 막 -----
        // 패널의 자식으로 두면 선택지를 다시 띄울 때마다 "자식 전부 지우기"에 같이 지워지므로 형제로 둔다.
        // 예전에는 대화창 윗변 위쪽만 덮었는데, 대화창 그림의 실제 윗변과 딱 맞지 않아 그 사이에
        // 밝은 띠가 생겼다. 그래서 화면 전체를 덮고, 대화창과 선택지 패널 중 더 앞쪽 형제 순서에
        // 끼워 넣어 둘 다 막 위에 그려지게 한다.
        Transform parent = panel.transform.parent;
        if (parent != null)
        {
            dim = new GameObject("ChoiceDim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(parent, false);
            int index = panel.transform.GetSiblingIndex();
            Transform dialogueRoot = FindChildOf(parent, dialoguePanel != null ? dialoguePanel.transform : null);
            if (dialogueRoot != null) index = Mathf.Min(index, dialogueRoot.GetSiblingIndex());
            dim.transform.SetSiblingIndex(index);
            var drt = (RectTransform)dim.transform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, DimAlpha);
            dimImage.raycastTarget = true;   // 선택지가 떠 있는 동안 뒤의 조사 오브젝트가 눌리지 않게
            dim.SetActive(panel.activeInHierarchy);
            // 창 등록은 선택지 패널이 하고(아래), 이 막은 그 일부다 - 개발 중 경고에서 뺀다.
            ModalStack.MarkNotWindow(dim, "선택지 뒤의 어두운 막. 창 등록은 선택지 패널(choicePanel)이 한다.");
        }

        StyleChildren();
    }

    // 패널이 켜질 때: 어두운 막을 켜고, 선택지를 살짝 떠오르듯 보여준다.
    // 사라지는 도중에 다시 켜졌다면(=다음 선택지가 바로 떴다면) 사라지던 상태를 전부 되돌린다.
    private void OnEnable()
    {
        fadingOut = false;
        // 비켜 놓았던 자리를 원래대로(한가운데) 되돌린다. 조사 중 대화 선택지나 추리 보기는
        // BuildChoicePanel을 거치지 않고 바로 패널을 켜므로, 여기서 되돌리지 않으면 앞선
        // 선택지가 말풍선을 피해 옮겨둔 자리에 그대로 뜬다.
        if (panelRect != null) panelRect.anchoredPosition = Vector2.zero;
        if (dim != null)
        {
            dim.SetActive(true);
            SetDimAlpha(DimAlpha);
            if (dimImage != null) dimImage.raycastTarget = true;
        }
        if (group != null)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn());
        }
    }

    private void OnDisable()
    {
        fadingOut = false;
        if (dim != null)
        {
            dim.SetActive(false);
            SetDimAlpha(DimAlpha);
        }
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private void OnDestroy()
    {
        if (dim != null) Destroy(dim);
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < FadeInSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / FadeInSeconds;
            yield return null;
        }
        group.alpha = 1f;
        fadeRoutine = null;
    }

    // ===== 선택지를 서서히 지우기 =====
    // 예전에는 보기를 고르는 순간 choicePanel.SetActive(false)로 툭 껐다. 바로 뒤에 암전
    // (IsFadeOut)이 이어지는 자리에서는 선택지만 먼저 사라지고 화면이 따로 어두워져서
    // 두 연출이 어긋나 보였다. 그래서 선택지도 함께 흐려지도록 이 길을 따로 뺐다.
    // 버튼과 어두운 막이 같이 흐려지고, 다 흐려지면 패널을 끈다(OnDisable에서 원래대로 되돌림).
    // 흐려지는 동안에는 클릭도 숫자키도 받지 않으므로 보기를 두 번 고를 수 없다.
    public static void FadeOut(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;

        var style = panel.GetComponentInChildren<ChoicePanelStyle>(true);
        if (style == null || style.group == null || !style.isActiveAndEnabled)
        {
            panel.SetActive(false);   // 모양 부품이 없으면 예전처럼 바로 끈다
            return;
        }
        style.BeginFadeOut(panel);
    }

    // =================================================================================
    // 말풍선과 겹치지 않게 비켜 놓기
    // =================================================================================
    // 선택지의 기본 자리는 화면 한가운데인데, 말풍선은 "말하는 인물 옆"에 뜨기 때문에 하필
    // 그 자리에 겹칠 수 있다. 겹치면 선택지 글자와 대사 글자가 포개져서 둘 다 못 읽는다.
    // (대사 도중 선택지를 넣으면서 실제로 이 모양이 나왔다)
    //
    // 그래서 말풍선이 떠 있으면 그 위/아래 중 자리가 넉넉한 쪽으로 선택지를 통째로 옮긴다.
    // 아래쪽을 먼저 보는 이유: 말풍선이 떠 있는 동안 아래 대화창은 감춰져 있어서 비어 있고,
    // "위에서 말하고 아래에서 고른다"는 읽는 순서가 자연스럽기 때문이다.
    //
    //   rowCount : 이번에 띄울 보기 개수 (높이 계산용 - 레이아웃은 다음 프레임에야 잡히므로
    //              여기서 직접 센다)
    //   avoid    : 피해야 할 네모(말풍선). 크기가 0이면 피할 것이 없다는 뜻.
    //   반환값   : 놓을 자리를 찾았으면 true. 위아래 어디에도 안 들어가면 false - 이때는
    //              부르는 쪽이 말풍선을 치우고 아래 대화창으로 되돌린다.
    public static bool PlaceClearOf(GameObject panel, int rowCount, Rect avoid)
    {
        if (panel == null) return true;
        var rt = (RectTransform)panel.transform;
        rt.anchoredPosition = Vector2.zero;            // 기본은 한가운데

        var canvas = UILayers.RootCanvas();
        if (canvas == null) return true;
        float halfH = ((RectTransform)canvas.transform).rect.height * 0.5f;

        if (avoid.width <= 0f || avoid.height <= 0f) return true;   // 말풍선이 없다

        float panelH = rowCount * RowHeight + Mathf.Max(0, rowCount - 1) * Spacing;
        float halfPanelH = panelH * 0.5f;
        float halfPanelW = Width * 0.5f;

        // 가로로도 세로로도 겹치지 않으면 한가운데 그대로 둔다.
        if (avoid.xMin >= halfPanelW || avoid.xMax <= -halfPanelW) return true;
        if (avoid.yMin >= halfPanelH || avoid.yMax <= -halfPanelH) return true;

        float roomBelow = (avoid.yMin - AvoidGap) - (-halfH + ScreenEdge);
        float roomAbove = (halfH - ScreenEdge) - (avoid.yMax + AvoidGap);

        if (roomBelow >= panelH)
        {
            rt.anchoredPosition = new Vector2(0f, avoid.yMin - AvoidGap - halfPanelH);
            return true;
        }
        if (roomAbove >= panelH)
        {
            rt.anchoredPosition = new Vector2(0f, avoid.yMax + AvoidGap + halfPanelH);
            return true;
        }
        return false;   // 말풍선이 너무 커서 위아래 어디에도 안 들어간다
    }

    private void BeginFadeOut(GameObject panel)
    {
        if (fadingOut) return;
        fadingOut = true;

        group.interactable = false;
        group.blocksRaycasts = false;
        if (dimImage != null) dimImage.raycastTarget = false;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(panel));
    }

    private IEnumerator FadeOutRoutine(GameObject panel)
    {
        float startAlpha = group.alpha;
        for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / FadeOutSeconds;
            group.alpha = startAlpha * k;
            SetDimAlpha(DimAlpha * k);
            yield return null;
        }
        group.alpha = 0f;
        SetDimAlpha(0f);
        fadeRoutine = null;
        panel.SetActive(false);
    }

    private void SetDimAlpha(float a)
    {
        if (dimImage == null) return;
        var c = dimImage.color;
        dimImage.color = new Color(c.r, c.g, c.b, a);
    }

    // 숫자키 1~9로 고르기 (위에서부터 1, 2, 3 …)
    private void Update()
    {
        if (fadingOut) return;

        for (int i = 0; i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            int n = 0;
            foreach (Transform child in transform)
            {
                var row = child.GetComponent<ChoiceRow>();
                if (row == null) continue;
                if (n++ == i) { row.Click(); return; }
            }
        }
    }

    // 버튼이 새로 생길 때마다 불린다.
    private void OnTransformChildrenChanged() => StyleChildren();

    private void StyleChildren()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<ChoiceRow>() == null) child.gameObject.AddComponent<ChoiceRow>().Build();
        }
    }

    // t의 조상 중 parent의 바로 아래 자식을 찾는다 (없으면 null).
    private static Transform FindChildOf(Transform parent, Transform t)
    {
        while (t != null && t.parent != parent) t = t.parent;
        return t;
    }

    // =================================================================================
    // 선택지 한 줄 (ChoiceButton 프리팹에 모양을 입힌다)
    // =================================================================================
    private class ChoiceRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image bg;
        private Image[] border;
        private Image accentBar;
        private TMP_Text label;
        private Button button;

        public ChoiceRow Build()
        {
            var rt = (RectTransform)transform;

            // 프리팹에 붙은 세로 정렬/크기 맞춤을 끈다. 켜 두면 아래에서 만드는 테두리·막대까지
            // 위에서부터 차례로 쌓아 버려서 테두리가 회색 덩어리가 된다.
            // 높이는 아래 LayoutElement로, 자식 위치는 직접 정한다.
            var innerLayout = GetComponent<VerticalLayoutGroup>();
            if (innerLayout != null) innerLayout.enabled = false;
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            bg = GetComponent<Image>();
            if (bg != null) { bg.sprite = null; bg.type = Image.Type.Simple; bg.color = FillColor; }

            button = GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;   // 마우스 올림 모양은 아래 SetHover가 맡는다
                var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            }

            // 테두리 (도트 한 칸 두께의 선 4개)
            // 말풍선과 같은 두께로 맞췄다. 1px이면 도트 글꼴 옆에서 선만 가늘어 겉돈다.
            // 모서리를 깎지 않고 네모로 두는 이유: 마우스 올림 때 선 4개의 색을 따로
            // 바꾸는 구조(SetHover)를 그대로 쓸 수 있고, 네모 모서리도 도트 스타일에 맞는다.
            border = AddBorder(rt, UITheme.Line, SpeechBubble.PixelBorder);

            // 마우스 올림 때만 보이는 왼쪽 강조 막대
            accentBar = NewImage("AccentBar", rt, UITheme.Accent);
            PlaceLeftColumn(accentBar.rectTransform, 0f, 3f, 0f);
            accentBar.enabled = false;

            // 선택지 글자 (프리팹에 들어 있는 TMP 글자를 옮겨 쓴다 - 부르는 쪽이 이미 글을 넣어 둠)
            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(TextPadding, 6f);
                lrt.offsetMax = new Vector2(-TextPadding, -6f);
                // ===== 글자 크기 =====
                // 도트 글꼴은 고유 크기(16px)의 배수로만 선명하다. 자동 줄이기를 켜 두면
                // 19.3px 같은 값이 나와 글자가 뭉개지므로, 도트일 때는 끄고 격자에 맞춘다.
                // (22 -> 16으로 작아지므로 글자가 넘칠 걱정은 오히려 줄어든다)
                if (FontManager.DotFontActive)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = FontManager.SnapToPixelGrid(22f);
                }
                else
                {
                    label.fontSize = 22;
                    label.enableAutoSizing = true;      // 긴 선택지는 줄이 넘치지 않게 글자를 줄인다
                    label.fontSizeMin = 16;
                    label.fontSizeMax = 22;
                }
                label.fontStyle = FontStyles.Normal;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.color = UITheme.Body;
                label.raycastTarget = false;
            }

            SetHover(false);
            return this;
        }

        public void Click()
        {
            if (button != null && button.interactable) button.onClick.Invoke();
        }

        public void OnPointerEnter(PointerEventData e) => SetHover(true);
        public void OnPointerExit(PointerEventData e) => SetHover(false);
        private void OnDisable() => SetHover(false);

        private void SetHover(bool on)
        {
            if (bg != null) bg.color = on ? FillHover : FillColor;
            if (border != null) foreach (var b in border) if (b != null) b.color = on ? UITheme.Accent : UITheme.Line;
            if (accentBar != null) accentBar.enabled = on;
            if (label != null) label.color = on ? UITheme.Title : UITheme.Body;
        }

        private static Image NewImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 왼쪽에서 x만큼 떨어진 세로 선 (위아래 inset만큼 띄움)
        private static void PlaceLeftColumn(RectTransform rt, float x, float width, float inset)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(x, inset);
            rt.offsetMax = new Vector2(x + width, -inset);
        }
    }
}
