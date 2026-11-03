using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 마우스를 올리면 테두리 선과 글자/아이콘을 밝게 (UIKit.AddHoverLine으로 붙인다)
// =====================================================================================
// 테두리만 있는 버튼(닫기 X, 돋보기 칸 등)은 Button의 기본 색 변화가 어울리지 않아서
// (투명 바탕에만 걸린다) 테두리와 글자 색을 직접 바꾼다.
public class UIHoverLine : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image[] border;
    private Graphic[] marks;
    private Color markNormal, markHover;

    public void Init(Image[] border, Graphic[] marks, Color markNormal, Color markHover)
    {
        this.border = border; this.marks = marks; this.markNormal = markNormal; this.markHover = markHover;
        Apply(false);
    }

    public void OnPointerEnter(PointerEventData e) => Apply(true);
    public void OnPointerExit(PointerEventData e) => Apply(false);
    private void OnDisable() => Apply(false);   // 닫았다 열 때 밝은 색이 남지 않게

    private void Apply(bool hover)
    {
        if (border != null) foreach (var l in border) if (l != null) l.color = hover ? UITheme.LineHover : UITheme.Line;
        if (marks != null) foreach (var m in marks) if (m != null) m.color = hover ? markHover : markNormal;
    }
}
