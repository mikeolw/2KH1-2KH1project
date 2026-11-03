using UnityEngine;

// =====================================================================================
// "이 오브젝트가 켜져 있는 동안은 창이 열려 있는 것" 이라는 표시 (ModalStack.cs 참고)
// =====================================================================================
// 직접 붙이지 말고 ModalStack.Register()로 붙인다.
public class ModalWindow : MonoBehaviour
{
    // Esc를 눌렀을 때 할 일. 보통 "창 닫기". null이면 Esc를 무시한다(그리고 아래 창으로 넘기지도 않는다).
    public System.Action onEscape;

    // 이 창이 떠 있는 동안 대사 넘기기/자동 진행을 멈출지.
    public bool blocksDialogue = true;

    private void OnEnable() => ModalStack.Push(this);
    private void OnDisable() => ModalStack.Remove(this);
}
