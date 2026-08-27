using UnityEngine;

// =====================================================================================
// 조사 가능한 위치에 올려두는 반투명 사각형 Placeholder에 붙는 컴포넌트
// (InvestigationController.cs, ItemModalController.cs와 함께 동작)
// =====================================================================================
// 정식 아트가 나오기 전까지는 이 컴포넌트가 붙은 GameObject에 Image(반투명 빨강/파랑 등)와
// Button을 붙여서 조사 위치를 표시한다. 나중에 아트가 나오면 이 오브젝트의 Image만
// 정식 그림으로 갈아끼우면 되고, 이 스크립트나 InvestigationController 쪽은 손댈 필요 없다.
//
// ===== 지금 배치해둔 좌표/크기는 "임시"이지, 미리 확정한 기획이 아니다 =====
// InvestigationScreen_Office 안의 핫스팟 6개(메모장/카메라/서랍/핸드폰/창문/회사 동료)는
// 지금 3x2 격자로 대충 흩어놓은 것뿐이다. 정식 배경 아트가 들어오면 책상이 어디 있고
// 창문이 어디 있는지가 그림에 따라 정해지므로, 그때 이 오브젝트들의 RectTransform
// (위치/크기)만 그림에 맞게 Inspector에서 옮기고 늘리면 된다 - 숫자 몇 개 바꾸는
// 값싼 작업이라 지금 정확히 맞춰둘 필요가 없다.
//   반면 실제로 손이 많이 가는/재사용해야 하는 부분은 "각 핫스팟이 어떤 type인지,
// 클릭하면 무슨 데이터(objectName/description/itemId/talkSentence 등)를 보여주는지"
// 하는 이 컴포넌트의 필드값들과, InvestigationController.screens에 등록해둔
// investigationId 매칭, Button.onClick -> OnClickInspect() 연결이다. 이 배관은
// 위치가 어떻게 바뀌든 그대로 살아남는다. 즉 "지금 만들어둔 게 나중에 다 갈아엎힐
// 낭비 아닌가?"에 대한 답은: 갈아엎이는 건 좌표뿐이고, 값나가는 배관/데이터는 그대로
// 재사용된다 - 그래서 지금 미리 짜두는 게 손해가 아니다.
//
// 조사했을 때 어떤 반응을 보일지는 type으로 나뉜다:
//   - Item        : 모달(그림/이름/설명)을 띄우고 인벤토리에도 등록한다. (예: 메모장, 카메라, 서랍)
//   - Description : 모달은 뜨지만 인벤토리에는 등록되지 않는다. (예: 창문)
//   - Talk        : 모달이 아니라 기존 대사창(speakerText/sentenceText)에 정해둔 대사를
//                   그대로 보여준다. (예: 회사 동료 - "말을 걸면 ~라는 말을 듣는다")
//
// 어떤 타입이든 실제 반응 처리는 InvestigationController.Inspect()가 전담한다 - 이 클래스는
// "조사 대상 하나에 대한 데이터 + 클릭했다는 신호"만 들고 있는 순수한 데이터/트리거 역할이다.
public enum HotspotType
{
    Item,
    Description,
    Talk
}

public class InvestigatableObject : MonoBehaviour
{
    [Header("조사 반응 타입")]
    public HotspotType type = HotspotType.Description;

    [Header("공통 (Item / Description 타입에서 사용)")]
    public string objectName;          // 모달 상단에 표시할 이름 (예: "메모장")
    [TextArea(2, 5)]
    public string description;         // 모달에 표시할 설명 문구
    public Sprite modalImage;          // 모달에 표시할 그림 (아트 없으면 비워둬도 됨)

    [Header("Item 타입 전용")]
    // InventoryManager.AddItem()에 넘길 식별자. 수첩의 InventorySlotUI.itemId와
    // 반드시 똑같은 문자열로 맞춰야 그 슬롯이 켜진다.
    public string itemId;

    [Header("Talk 타입 전용")]
    public string talkSpeaker;         // 대사창에 표시할 화자 이름
    [TextArea(2, 5)]
    public string talkSentence;        // 대사창에 표시할 대사 내용

    // 이 오브젝트의 Button 컴포넌트 OnClick()에 연결해서 쓴다.
    public void OnClickInspect()
    {
        InvestigationController.Instance.Inspect(this);
    }
}
