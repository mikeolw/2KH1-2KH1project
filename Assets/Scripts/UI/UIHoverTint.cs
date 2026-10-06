using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 마우스를 올리면 글자/아이콘 색을 바꾸는 작은 도우미
// =====================================================================================
// Button의 색 변화(ColorTint)는 "배경 그림 하나"에만 걸리므로, 버튼 안의 글자와 아이콘까지
// 같이 밝게 하려면 이걸 붙인다. 배경 그림 교체는 Button의 SpriteSwap으로 따로 한다.
public class UIHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Graphic[] targets;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.white;

    public void OnPointerEnter(PointerEventData eventData) => Apply(hoverColor);
    public void OnPointerExit(PointerEventData eventData) => Apply(normalColor);

    // 버튼이 꺼졌다 켜지면(조사 화면을 다시 열 때 등) 마우스가 올라가 있던 색이 남지 않게.
    private void OnDisable() => Apply(normalColor);

    private void Apply(Color color)
    {
        if (targets == null) return;
        foreach (var g in targets) if (g != null) g.color = color;
    }
}
