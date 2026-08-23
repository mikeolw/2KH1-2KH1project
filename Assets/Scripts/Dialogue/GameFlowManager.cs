using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 엔딩 발동 테스트 함수
    public void TriggerEnding(EndingType ending)
    {
        Debug.Log($"[엔딩 발생] : {ending}");
    }
}