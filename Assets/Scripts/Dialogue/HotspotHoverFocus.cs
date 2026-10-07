using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 조사 오브젝트에 마우스를 올렸을 때의 표시 (Figma "Screen / Investigation")
// =====================================================================================
//   - 오브젝트 크기에 맞춰 네 모서리에 괄호 [ ]
//   - 그 아래 가운데에 돋보기 + 오브젝트 이름표
// InvestigationController.CreateHotspot()이 조사 가능한 오브젝트마다 붙인다.
//
// ===== 왜 오브젝트 안이 아니라 따로 하나만 만들어 쓰나 =====
// 표시를 오브젝트의 자식으로 붙이면, 뒤에 만들어진 다른 오브젝트 그림에 이름표가 가려진다.
// 그렇다고 오브젝트를 맨 앞으로 옮기면 겹쳐 그려진 그림들의 앞뒤가 바뀌어 화면이 달라진다.
// 그래서 조사 화면 맨 위에 표시용 판을 하나만 두고, 마우스가 올라간 오브젝트 자리로 옮겨서 쓴다.
// 표시는 클릭을 막지 않는다(raycastTarget 끔).
public class HotspotHoverFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public string displayName;

    private const string LayerName = "__HotspotHover";
    private const string ArtFolder = "Illusts/UI/Investigation/";
    private const float Padding = 12f;   // 괄호를 오브젝트보다 이만큼 바깥에 그린다

    // 흰 선화 배경 위에서 잘 보이도록 괄호는 짙은 청흑색, 이름표는 패널 색 + 밝은 글자.
    private static readonly Color BracketColor = new Color(0.12f, 0.16f, 0.21f);
    private static readonly Color TagTextColor = new Color(0.851f, 0.89f, 0.922f);       // D9E3EB
    private static readonly Color TagFallbackColor = new Color(0.063f, 0.086f, 0.114f, 0.9f);

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 대사가 떠 있는 동안에는 표시하지 않는다 (대사를 읽는 중에 이름표가 깜빡이면 거슬린다).
        if (InvestigationController.Instance != null && InvestigationController.Instance.IsShowingTalkLine) return;
        Show();
        PlayHoverSound();
    }

    // ===== 마우스를 올렸을 때 효과음 =====
    // Assets/Resources/Sounds/SFX/HotspotHover_SFX.mp3 (git 제외 - 드라이브 공유). 없으면 소리 없이 넘어간다.
    // 오브젝트마다 AudioSource를 두지 않고 하나만 만들어 같이 쓴다. 효과음 볼륨 설정을 따르도록
    // AudioManager에 효과음으로 등록한다.
    private const string HoverSfxPath = "Sounds/SFX/HotspotHover_SFX";
    private static AudioSource hoverSfxSource;
    private static AudioClip hoverSfxClip;
    private static bool hoverSfxLoaded;

    private static void PlayHoverSound()
    {
        if (!hoverSfxLoaded)
        {
            hoverSfxClip = Resources.Load<AudioClip>(HoverSfxPath);
            hoverSfxLoaded = true;
        }
        if (hoverSfxClip == null) return;

        if (hoverSfxSource == null)   // 씬이 바뀌어 사라졌으면 다시 만든다
        {
            var go = new GameObject("HotspotHoverSfx");
            hoverSfxSource = go.AddComponent<AudioSource>();
            hoverSfxSource.playOnAwake = false;
            hoverSfxSource.spatialBlend = 0f;
            AudioManager.RegisterSafe(hoverSfxSource, AudioManager.Channel.Sfx);
        }
        hoverSfxSource.PlayOneShot(hoverSfxClip);
    }

    public void OnPointerExit(PointerEventData eventData) => Hide();
    public void OnPointerClick(PointerEventData eventData) => Hide();
    private void OnDisable() => Hide();
    private void OnDestroy() => Hide();

    private void Show()
    {
        var self = transform as RectTransform;
        var parent = transform.parent as RectTransform;
        if (self == null || parent == null) return;

        var layer = GetOrCreateLayer(parent);
        layer.gameObject.SetActive(true);
        layer.SetAsLastSibling();

        // 오브젝트의 화면 위 네 모서리를 표시판 부모 좌표로 옮겨서 자리를 잡는다
        // (오브젝트마다 앵커/피벗이 달라도 똑같이 맞는다).
        var corners = new Vector3[4];
        self.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        Vector2 parentCenterOffset = parent.rect.center;
        layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.anchoredPosition = (min + max) / 2f - parentCenterOffset;
        layer.sizeDelta = (max - min) + new Vector2(Padding * 2f, Padding * 2f);

        var tag = layer.Find("NameTag");
        if (tag != null)
        {
            tag.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
            var label = tag.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = displayName ?? "";
        }
    }

    private void Hide()
    {
        if (transform.parent == null) return;
        var layer = transform.parent.Find(LayerName);
        if (layer != null) layer.gameObject.SetActive(false);
    }

    private static RectTransform GetOrCreateLayer(RectTransform parent)
    {
        var existing = parent.Find(LayerName);
        if (existing != null) return (RectTransform)existing;

        var layerGo = new GameObject(LayerName, typeof(RectTransform), typeof(Image));
        layerGo.transform.SetParent(parent, false);
        var layer = (RectTransform)layerGo.transform;

        // 네 모서리 괄호 (가장자리 20px을 유지한 채 오브젝트 크기만큼 늘린다. 흰 그림에 색을 입힌다)
        var bracket = layerGo.GetComponent<Image>();
        if (UISpriteUtil.ApplySliced(bracket, ArtFolder + "HotspotFocus", 40f))
        {
            bracket.fillCenter = false;
            bracket.color = BracketColor;
        }
        else
        {
            bracket.color = new Color(BracketColor.r, BracketColor.g, BracketColor.b, 0.15f);
        }
        bracket.raycastTarget = false;

        // 이름표 (괄호 아래 가운데, 글자 길이에 맞춰 늘어난다)
        var tagGo = new GameObject("NameTag", typeof(RectTransform), typeof(Image),
                                   typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        tagGo.transform.SetParent(layer, false);
        var tagRt = (RectTransform)tagGo.transform;
        tagRt.anchorMin = tagRt.anchorMax = new Vector2(0.5f, 0f);
        tagRt.pivot = new Vector2(0.5f, 1f);
        tagRt.anchoredPosition = new Vector2(0f, -10f);
        var tagImg = tagGo.GetComponent<Image>();
        if (!UISpriteUtil.ApplySliced(tagImg, ArtFolder + "NameTag", 6f)) tagImg.color = TagFallbackColor;
        tagImg.raycastTarget = false;

        var layout = tagGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 14, 7, 7);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = tagGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(tagGo.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = UISpriteUtil.Load(ArtFolder + "Icon_Search");
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = iconImg.sprite != null;
        var iconLe = iconGo.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 16f;
        iconLe.preferredHeight = 16f;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(tagGo.transform, false);
        var label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 16;
        label.fontStyle = FontStyles.Bold;
        label.color = TagTextColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        UIFontHelper.ApplyToChildren(layerGo);
        layerGo.SetActive(false);
        return layer;
    }
}
