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

    public void Init(Image bg, Image frame, Image icon, GameObject activeBar, GameObject label, GameObject panel)
    {
        this.bg = bg; this.frame = frame; this.icon = icon;
        this.activeBar = activeBar; this.label = label; this.panel = panel;
        initialized = true;
        Redraw(true);
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Redraw(false); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Redraw(false); }

    private void OnDisable() { hovered = false; }

    private void Update()
    {
        // 패널은 이 버튼 말고도(Esc, 다른 버튼 등) 열리고 닫히므로 매 프레임 확인한다.
        Redraw(false);
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
