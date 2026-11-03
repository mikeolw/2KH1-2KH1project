using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 이 오브젝트를 정해진 UI 층에 그린다 (UILayers.cs 참고)
// =====================================================================================
// 직접 붙이지 말고 UILayers.Place()로 붙인다.
//
// Canvas를 하나 더 붙이고 overrideSorting으로 정렬 순서를 따로 준다. 클릭을 받으려면
// GraphicRaycaster도 같이 있어야 한다.
// overrideSorting은 꺼진 상태에서 켜면 값이 풀리는 경우가 있어서, 켜질 때마다 다시 건다.
public class UILayerMember : MonoBehaviour
{
    public UILayer layer = UILayer.Panel;

    private void OnEnable() => Apply();

    public void Apply()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)layer;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }
}
