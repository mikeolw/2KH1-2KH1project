using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 특정 UI를 "항상 맨 앞"에 그리게 하는 도우미
// =====================================================================================
// 화면 UI는 대부분 같은 Canvas 밑 형제라서, 목록에서 나중에 있는 것이 위에 그려진다.
// 그래서 SetAsLastSibling으로 맨 뒤로 보내도, 그 뒤에 새로 만들어진 UI(퀵바, 알림 등)가
// 다시 위로 올라와 버린다.
//
// 여기서는 대상에 Canvas를 하나 더 붙이고 overrideSorting으로 정렬 순서를 따로 준다.
// 이렇게 하면 형제 순서와 상관없이 sortingOrder가 높은 쪽이 항상 위에 그려진다.
// 클릭을 받으려면 GraphicRaycaster도 같이 붙어야 한다.
public static class UITopLayer
{
    // 조사 상세(자료 뷰어 / 아이템 팝업) - 다른 모달보다 항상 위
    public const int InvestigationDetailOrder = 100;

    public static void MakeTopmost(GameObject go, int sortingOrder)
    {
        if (go == null) return;

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        // 부모 Canvas 밑에 들어간 뒤에 켜야 값이 유지된다(그래서 열 때마다 다시 불러도 된다).
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
    }
}
