using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 캐릭터 스탠딩(서 있는 그림) 한 자리를 담당하는 컴포넌트 (StageController.cs와 함께 동작)
// =====================================================================================
// 화면에는 캐릭터가 최대 3명까지 동시에 나올 수 있고(왼쪽/가운데/오른쪽), 그 자리 하나하나에
// 이 컴포넌트가 붙은 GameObject(Image)가 놓인다. 자리 자체는 StageController가 만들거나
// 씬에 미리 배치해둔 것을 쓴다.
//
// 이 컴포넌트가 하는 일은 딱 두 가지다:
//   1) 지정된 스탠딩 그림을 화면에 띄우고 원본 크기대로 맞춘다.
//   2) 대사가 타이핑되는 동안 "입 벌린 그림"과 번갈아 보여줘서 말하는 것처럼 보이게 한다(립싱크).
//
// ===== 립싱크(입 뻐끔)에 대해 =====
// 아트 파일 중에는 이름 뒤에 "_OpenMouse"가 붙은 짝이 있다. 예를 들어
//   STD_Past01_Hansung_Default.png          <- 입 다문 그림 (기본)
//   STD_Past01_Hansung_Default_OpenMouse.png <- 입 벌린 그림
// 이렇게 두 장이 한 쌍이다. 대사가 한 글자씩 찍히는 동안 이 두 장을 빠르게 번갈아 보여주면
// 캐릭터가 말하는 것처럼 보인다. 짝이 되는 "_OpenMouse" 파일이 없는 캐릭터는 그냥 입을
// 움직이지 않는다(에러가 아니라 정상 동작이다 - 예: 표정 그림만 있고 입 벌린 버전이 없는 경우).
//
// 파일 이름의 "Mouse"는 원래 "Mouth(입)"의 오타지만, 이미 아트 파일 전체가 그 이름으로
// 만들어져 있으므로 코드도 파일 이름을 그대로 따른다. (파일 이름을 바꾸면 드라이브에 있는
// 원본까지 전부 바꿔야 해서 더 위험하다.)
[RequireComponent(typeof(Image))]
public class StandingSlot : MonoBehaviour
{
    [Header("립싱크 속도")]
    [Tooltip("입을 벌렸다 다물었다 하는 간격(초). 작을수록 빠르게 움직인다.")]
    public float mouthFlapInterval = 0.12f;

    // 이 자리에 지금 올라와 있는 스탠딩의 파일 이름. CSV에서 같은 이름이 또 지정되면
    // 굳이 그림을 다시 세팅하지 않기 위해 기억해둔다(깜빡임 방지).
    public string CurrentFileName { get; private set; }

    private Image image;

    // 입 다문 그림 / 입 벌린 그림. openMouthSprite가 null이면 립싱크를 하지 않는다.
    private Sprite closedMouthSprite;
    private Sprite openMouthSprite;

    // 지금 돌아가고 있는 립싱크 코루틴. 중복 실행되지 않도록 들고 있다가 멈출 때 쓴다.
    private Coroutine flapRoutine;

    private void Awake()
    {
        image = GetComponent<Image>();

        // 스탠딩은 클릭 대상이 아니다(조사 오브젝트와 달리 상호작용이 없다).
        // raycastTarget을 꺼두지 않으면 스탠딩 그림이 화면을 가려서 그 뒤에 있는
        // 대사창 클릭이나 조사 오브젝트 클릭을 가로채버린다.
        image.raycastTarget = false;

        // 그림이 아직 없는 상태에서 흰 네모가 보이지 않도록 처음엔 꺼둔다.
        image.enabled = false;
    }

    // 이 자리에 스탠딩 그림을 올린다. fileName은 확장자 없는 파일 이름
    // (예: "STD_Past01_Hansung_Default"). IllustLoader가 Resources/Illusts/Standings/에서 찾는다.
    public void Show(string fileName)
    {
        // 이미 같은 그림이 올라와 있으면 아무것도 하지 않는다.
        // (CSV에서 여러 줄 연속으로 같은 표정을 지정해도 깜빡이지 않게 하기 위함)
        if (CurrentFileName == fileName && image.sprite != null) return;

        Sprite sprite = IllustLoader.LoadStanding(fileName);
        if (sprite == null)
        {
            // 파일을 못 찾은 경우. IllustLoader가 이미 경고를 남겼으므로 여기서는 조용히 숨긴다.
            Hide();
            return;
        }

        StopFlap();

        CurrentFileName = fileName;
        closedMouthSprite = sprite;

        // 짝이 되는 "_OpenMouse" 그림을 찾아둔다. 없으면 null이고, 그 경우 립싱크를 건너뛴다.
        openMouthSprite = IllustLoader.LoadOpenMouthVariant(fileName);

        image.sprite = closedMouthSprite;
        image.enabled = true;

        // 그림마다 크기가 제각각이므로(예: 836x569, 797x1080) 원본 픽셀 크기 그대로 표시한다.
        // 배경이 1440x1080 캔버스에 딱 맞게 그려져 있고 스탠딩도 같은 기준으로 그려졌으므로,
        // 원본 크기로 놓는 것이 아트가 의도한 크기와 가장 가깝다.
        image.SetNativeSize();
    }

    // 이 자리를 비운다(캐릭터 퇴장).
    public void Hide()
    {
        StopFlap();
        CurrentFileName = null;
        closedMouthSprite = null;
        openMouthSprite = null;
        image.sprite = null;
        image.enabled = false;
    }

    // 대사 타이핑이 시작될 때 true, 끝날 때 false로 호출한다 (DialogueSystem이 호출).
    // true인 동안 입 벌린 그림과 번갈아 보여준다.
    public void SetTalking(bool talking)
    {
        if (talking)
        {
            // 그림이 없거나 입 벌린 짝이 없으면 립싱크할 수 없다.
            if (closedMouthSprite == null || openMouthSprite == null) return;
            if (flapRoutine != null) return; // 이미 돌고 있으면 그대로 둔다

            flapRoutine = StartCoroutine(FlapMouth());
        }
        else
        {
            StopFlap();
        }
    }

    // 입 벌린 그림 <-> 입 다문 그림을 mouthFlapInterval 간격으로 번갈아 보여주는 코루틴.
    private IEnumerator FlapMouth()
    {
        bool open = false;
        while (true)
        {
            open = !open;
            image.sprite = open ? openMouthSprite : closedMouthSprite;
            yield return new WaitForSeconds(mouthFlapInterval);
        }
    }

    // 립싱크를 멈추고 반드시 "입 다문" 상태로 되돌린다.
    // (멈출 때 입 벌린 그림에서 멈춰버리면 캐릭터가 계속 입을 벌리고 있게 된다.)
    private void StopFlap()
    {
        if (flapRoutine != null)
        {
            StopCoroutine(flapRoutine);
            flapRoutine = null;
        }
        if (closedMouthSprite != null) image.sprite = closedMouthSprite;
    }
}
