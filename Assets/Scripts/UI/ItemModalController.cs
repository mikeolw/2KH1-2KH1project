using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사 모드 아이템/설명 모달 (InvestigationController.cs, InvestigatableObject.cs와 함께 동작)
// =====================================================================================
// 조사 화면에서 Item 또는 Description 타입 오브젝트(InvestigatableObject)를 클릭했을 때
// 뜨는 팝업이다. 화면 전체를 덮는 반투명(50%) 검은 배경 위에 그림 + 이름 + 설명 텍스트를
// 보여준다. MinigameController.cs와 같은 이유로 씬에 미리 배치해둔 패널을 켜고 끄는
// 방식으로 구현했다 (동적 생성 없음 - 모달 구성 요소가 매번 똑같으므로 굳이 Instantiate할
// 필요가 없다).
//
// 지금은 아트가 없어서 modalImage 자리에 반투명 사각형 Sprite(placeholder)를 넣어두고,
// 나중에 정식 아트가 나오면 InvestigatableObject 쪽 modalImage 필드에 연결된 Sprite만
// 갈아끼우면 된다 (이 스크립트는 손댈 필요 없음).
public class ItemModalController : MonoBehaviour
{
    public static ItemModalController Instance;

    [Header("UI 연결")]
    public GameObject panel;              // 반투명 배경 + 아래 3개를 담고 있는 루트
    public Image itemImage;               // 아이템/오브젝트 그림
    public TMP_Text itemNameText;         // 이름
    public TMP_Text itemDescriptionText;  // 설명

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (panel != null) panel.SetActive(false);
    }

    // InvestigationController.Inspect()가 Item/Description 타입 오브젝트를 클릭했을 때 호출한다.
    public void Show(Sprite image, string itemName, string description)
    {
        if (itemImage != null) itemImage.sprite = image;
        if (itemNameText != null) itemNameText.text = itemName;
        if (itemDescriptionText != null) itemDescriptionText.text = description;

        panel.SetActive(true);
    }

    // 배경(반투명 검은 부분) 클릭이나 닫기 버튼의 OnClick에 연결한다.
    // 대사로 돌아가는 게 아니라 "조사 화면"으로만 복귀한다 - InvestigationController는
    // 이 모달이 떠 있는 동안에도 조사 화면 패널을 계속 활성 상태로 두고 있으므로,
    // 여기서는 모달만 닫으면 자연스럽게 조사 화면이 다시 보인다.
    public void Hide()
    {
        panel.SetActive(false);
    }
}
