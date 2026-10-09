using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 퀵바 버튼 하나의 Hover/Active 상태를 그린다. QuickBarStyler가 붙인다.
public class QuickButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image bg, frame, icon;
    private GameObject activeBar, label;
    private GameObject panel;
    private bool hovered;
    private bool lastHovered, lastActive;
    private bool initialized;

    // 새 항목 숫자 (수첩/가방만)
    private bool hasBadge;
    private NewContentTracker.Kind badgeKind;
    private GameObject badge;
    private TMPro.TMP_Text badgeText;
    private int lastBadgeCount = -1;

    public void Init(Image bg, Image frame, Image icon, GameObject activeBar, GameObject label, GameObject panel)
    {
        this.bg = bg; this.frame = frame; this.icon = icon;
        this.activeBar = activeBar; this.label = label; this.panel = panel;
        initialized = true;
        Redraw(true);
    }

    public void InitBadge(NewContentTracker.Kind kind, GameObject badge, TMPro.TMP_Text badgeText)
    {
        hasBadge = badge != null;
        badgeKind = kind;
        this.badge = badge;
        this.badgeText = badgeText;
        lastBadgeCount = -1;
        UpdateBadge();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Redraw(false); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Redraw(false); }

    private void OnDisable() { hovered = false; }

    private void Update()
    {
        // 패널은 이 버튼 말고도(Esc, 다른 버튼 등) 열리고 닫히므로 매 프레임 확인한다.
        Redraw(false);
        UpdateBadge();
    }

    // 창이 열려 있으면 숫자를 0으로(= 봤음), 아니면 새로 생긴 개수를 보여준다.
    private void UpdateBadge()
    {
        if (!hasBadge) return;
        if (panel != null && panel.activeInHierarchy) NewContentTracker.ClearBadge(badgeKind);

        int count = NewContentTracker.BadgeCount(badgeKind);
        if (count == lastBadgeCount) return;
        lastBadgeCount = count;
        badge.SetActive(count > 0);
        if (badgeText != null) badgeText.text = count > 99 ? "99+" : count.ToString();
    }

    private void Redraw(bool force)
    {
        if (!initialized) return;
        bool active = panel != null && panel.activeInHierarchy;
        if (!force && hovered == lastHovered && active == lastActive) return;
        lastHovered = hovered;
        lastActive = active;

        bool lit = hovered || active;
        if (bg != null) bg.color = lit ? QuickBarStyler.BoxHoverColor : QuickBarStyler.BoxColor;
        if (frame != null) frame.color = lit ? QuickBarStyler.StrokeHoverColor : QuickBarStyler.StrokeColor;
        if (icon != null) icon.color = lit ? QuickBarStyler.IconHoverColor : QuickBarStyler.IconColor;
        if (activeBar != null) activeBar.SetActive(active);
        if (label != null) label.SetActive(hovered);
    }
}
