using UnityEngine;

// =====================================================================================
// 배경 위에 올라가는 "조사할 수 있는 오브젝트" 하나 (InvestigationController.cs와 함께 동작)
// =====================================================================================
// 이 게임은 배경 그림 위에 놓인 오브젝트를 직접 클릭해서 조사한다. 그 오브젝트 하나하나에
// 이 컴포넌트가 붙는다.
//
// ===== 이 오브젝트들은 씬에 미리 만들어두지 않는다 =====
// InvestigationController가 InvestigationData.csv를 읽어서 게임 도중에 만들어낸다.
// 그래서 조사 화면을 늘리거나 오브젝트를 추가할 때 유니티를 열 필요가 없고, CSV에 줄만
// 추가하면 된다.
//
// ===== 화면 위치는 어떻게 정해지나 =====
// 그림이 1440x1080(여백까지 포함해 내보낸 것)이면 화면에 그대로 깔기만 하면 원래 그려진
// 자리에 나타난다. 여백이 잘려 있는 그림은 IllustLayout.csv에 적어둔 좌표를 따른다.
// (자세한 내용은 IllustLayout.cs 상단 주석, 위치 조정은 [2KH1] > [일러스트 배치 도구])
//
// 투명한 부분은 클릭이 통과하므로, 그림이 실제로 그려진 곳만 눌린다. 덕분에 오브젝트가
// 서로 겹쳐 있어도 원하는 것을 정확히 고를 수 있다 (ApplyIllust 참고).
//
// 조사했을 때 어떤 반응을 보일지는 type으로 나뉜다:
//   - Item        : 가방에 넣고 조사 내용을 대화창에 출력한다 (예: 메모장, 카메라, 서랍)
//   - Description : 가방에 넣지 않고 조사 내용만 대화창에 출력한다 (예: 창문, 캐비닛)
//   - Talk        : 화자 이름과 함께 대사를 대화창에 출력한다 (예: 회사 동료, 동네 주민)
// 셋 다 서류/사진처럼 자료 자체를 읽어야 하는 아이템이면(ItemData.csv의 ViewerType)
// 대화창 대신 전체화면 자료 뷰어가 열린다.
//
// 실제 반응 처리는 InvestigationController.Inspect()가 전담한다 - 이 클래스는
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

    [Header("일러스트 (CSV의 Sprite 칸에서 자동으로 채워진다)")]
    // Resources/Illusts/Objects/ 안의 파일 이름 (확장자 제외, 예: OBJ_01_Notepad).
    // InvestigationController.Enter() 시점에 CSV를 읽어 이 값을 채우고, 그 그림을 이
    // 오브젝트의 Image에 올린다. 인스펙터에서 Sprite를 직접 꽂지 않는 이유는
    // IllustLoader.cs 상단 주석 참고(Resources 폴더가 git으로 공유되지 않아 GUID가 깨지기 때문).
    public string spriteName;

    // 투명한 부분은 클릭이 통과하고, 그림이 그려진 부분만 클릭되게 할지 여부.
    // 조사 오브젝트는 대부분 투명 배경 PNG라서 이걸 켜두지 않으면 네모난 판때기처럼
    // 클릭되어, 뒤에 겹쳐 있는 다른 오브젝트를 누를 수 없게 된다.
    [Tooltip("체크하면 그림의 불투명한 부분만 클릭된다. 투명 배경 PNG라면 켜두는 것을 권장.")]
    public bool usePixelPerfectClick = true;

    [Tooltip("이 값보다 알파(불투명도)가 낮은 픽셀은 클릭이 통과한다. 0.1이면 10% 미만.")]
    [Range(0.01f, 1f)]
    public float alphaClickThreshold = 0.1f;

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

    // =================================================================================
    // 일러스트 적용 + 투명 픽셀 클릭 무시
    // =================================================================================
    // InvestigationController.ApplyHotspotData()가 조사 화면을 열기 직전에 호출한다.
    // CSV에 적힌 그림 이름으로 실제 Sprite를 불러와 이 오브젝트의 Image에 올리고,
    // 투명한 부분은 클릭이 통과하도록 설정한다.
    //
    // ===== 투명 픽셀 클릭 무시가 동작하는 조건 (중요) =====
    // Image.alphaHitTestMinimumThreshold는 그림의 픽셀을 코드에서 읽을 수 있어야 동작한다.
    // 그래서 텍스처 임포트 설정에서 "Read/Write Enabled"가 켜져 있고 Mesh Type이
    // Full Rect여야 하는데, 이건 Assets/Editor/IllustTextureImporter.cs가 자동으로
    // 맞춰주므로 따로 신경 쓸 필요 없다. (설정이 안 맞으면 클릭 시 예외가 나므로
    // 아래에서 try-catch로 감싸 안전하게 처리한다.)
    public void ApplyIllust()
    {
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image == null) return;

        // CSV에 그림 이름이 없으면 씬에 미리 넣어둔 placeholder 그림을 그대로 쓴다.
        if (!string.IsNullOrWhiteSpace(spriteName))
        {
            Sprite sprite = IllustLoader.LoadObject(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;

                // placeholder였을 때 들어가 있던 반투명 색을 원래대로(흰색 = 그림 그대로) 되돌린다.
                image.color = Color.white;

                // ===== 화면 위치 잡기 =====
                // 조사 오브젝트는 배경 그림 위의 특정 자리에 딱 맞게 그려진 조각이다.
                // 그림이 1440x1080(여백 포함)이면 화면에 꽉 채우기만 하면 제자리에 나타나고,
                // 여백이 잘려 있으면 IllustLayout.csv에 적어둔 좌표대로 놓는다.
                // 자세한 이유는 IllustLayout.cs 상단 주석 참고.
                IllustLayout.Apply(image.rectTransform, sprite, spriteName);
            }
        }

        // 투명 픽셀 클릭 무시 설정
        if (usePixelPerfectClick)
        {
            try
            {
                image.alphaHitTestMinimumThreshold = alphaClickThreshold;
            }
            catch (System.Exception e)
            {
                // 텍스처의 Read/Write Enabled가 꺼져 있으면 여기서 예외가 난다.
                // 게임을 멈추는 대신 경고만 남기고 "네모 전체가 클릭되는" 기본 동작으로 둔다.
                Debug.LogWarning(
                    $"[InvestigatableObject] '{gameObject.name}'의 투명 픽셀 클릭 설정에 실패했습니다. " +
                    $"유니티 상단 메뉴 [2KH1] > [Illusts 폴더 그림 임포트 설정 다시 적용]을 실행해 주세요.\n{e.Message}");
                image.alphaHitTestMinimumThreshold = 0f;
            }
        }
        else
        {
            image.alphaHitTestMinimumThreshold = 0f;
        }
    }
}
