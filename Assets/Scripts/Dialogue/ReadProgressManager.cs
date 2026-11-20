using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// "이미 읽은 대사" 기록 - 스킵(already) 기능이 어디까지가 처음 보는 줄인지 판단하는 데 쓴다
// =====================================================================================
// ===== 왜 필요한가? =====
// SavePointManager는 "마지막으로 지나온 세이브포인트"만 기억하고, 세이브 슬롯 안의
// SaveData(scenarioCsv/lineIndex)도 "그 슬롯을 저장한 시점의 위치" 하나만 가리킨다.
// 즉 "이 CSV의 이 줄을 예전에 본 적이 있는가"를 답해주는 기록이 어디에도 없다.
//
// ===== 저장 단위 =====
// 세이브 슬롯 구분 없이 기기 전체(플레이어 1명) 기준으로, "CSV 파일명 + 줄 번호" 쌍을
// 방문한 줄의 집합으로 기록한다. "가장 멀리 진행한 줄 번호"만 기억하면 선택지로 분기된
// 다른 줄들이 실제로는 안 읽었는데도 "읽은 것"으로 잘못 판정되므로, 반드시 방문한 줄
// 하나하나를 기록해야 한다.
//
// ===== 저장 방식 =====
// SettingsManager와 동일하게 PlayerPrefs에 JSON 문자열 하나로 저장한다(슬롯 구분 없는
// "기기당 값"이라는 점이 세이브 데이터와 다르고 설정값과 같기 때문). JsonUtility는
// HashSet/Dictionary를 직렬화하지 못하므로(SaveData.cs, InventoryManager.cs가 쓰는 것과
// 같은 우회법) List<string>으로 감싼 래퍼 클래스를 쓴다.
//
// ===== 씬 배치 =====
// SettingsManager/SaveManager/SavePointManager와 마찬가지로 Title.unity에 빈 GameObject를
// 만들고 이 스크립트를 붙여두면 된다 (DontDestroyOnLoad라서 씬이 바뀌어도 살아남는다).
[Serializable]
internal class ReadProgressData
{
    public List<string> keys = new List<string>();
}

public class ReadProgressManager : MonoBehaviour
{
    public static ReadProgressManager Instance;

    // 필드 구조를 크게 바꾸면 버전을 올려서(_v2 등) 기존 데이터와 충돌하지 않게 한다
    // (GameSettings.cs와 동일한 관례).
    // v2: 줄 번호 대신 줄 확인 숫자로 기록한다 (아래 MakeKey 참고). v1 기록은 번호가 밀린
    // 시나리오에서는 맞지 않아서 옮겨 오지 않는다 - 한 번씩만 다시 읽으면 된다.
    private const string PrefsKey = "ReadProgress_v2";

    // 방문한 "csv|lineIndex" 키의 집합. 실제 대소문자/공백 없는 CSV 파일명만 들어오므로
    // '|' 구분자가 겹칠 일은 없다.
    private readonly HashSet<string> readKeys = new HashSet<string>();

    // 마지막 저장 이후 readKeys가 바뀌었는지. 매 줄마다 PlayerPrefs.Save()로 디스크에
    // 쓰면 스킵(already)처럼 한 프레임에 여러 줄을 처리할 때 느려지므로, 실제 디스크
    // 반영은 Flush()가 불릴 때만 한다.
    private bool dirty;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit() => Flush();
    private void OnApplicationPause(bool pause) { if (pause) Flush(); }

    // ===== 왜 줄 번호가 아니라 확인 숫자인가 =====
    // 예전에는 "CSV|줄 번호"로 기록했다. 시나리오를 고쳐 줄이 빠지거나 끼면 번호가 밀려서,
    // 스킵(already)이 안 읽은 새 대사를 건너뛰거나 읽은 대사에서 멈췄다.
    // 이제는 "CSV|#줄 확인 숫자"(DialogueSystem.LineFingerprint)로 기록한다. 대사가 그대로면
    // 번호가 밀려도 읽음이 유지되고, 대사가 고쳐진 줄은 새로 읽은 것으로 친다.
    // (같은 CSV 안에 똑같은 대사가 두 번 나오면 한쪽을 읽으면 둘 다 읽은 것이 된다 - 스킵에는 문제없다)
    private static string MakeKey(string csv, string lineCheck) => csv + "|#" + lineCheck;

    // 이 CSV의 이 줄을 예전에(혹은 방금 전에) 본 적이 있는지. lineCheck = DialogueSystem.LineCheckAt(줄 번호)
    public bool IsRead(string csv, string lineCheck)
    {
        return !string.IsNullOrEmpty(csv) && !string.IsNullOrEmpty(lineCheck) &&
               readKeys.Contains(MakeKey(csv, lineCheck));
    }

    // 이 줄을 지금 봤다고 기록한다. 처음 보는 줄일 때만 실제로 값이 바뀌고(dirty=true),
    // 이미 기록된 줄이면 아무 일도 하지 않는다 - 스킵(already)이 읽은 구간을 빠르게
    // 지나갈 때 매 프레임 문자열을 다시 직렬화하지 않게 하기 위해서다.
    public void MarkRead(string csv, string lineCheck)
    {
        if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(lineCheck)) return;
        if (readKeys.Add(MakeKey(csv, lineCheck)))
        {
            dirty = true;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new ReadProgressData { keys = new List<string>(readKeys) }));
        }
    }

    // 실제로 디스크에 반영한다(PlayerPrefs.Save()). 앱 종료/일시정지 시점과,
    // SavePointManager.ReachSavePoint()(세이브포인트 도달 시점)에서 불러준다.
    public void Flush()
    {
        if (!dirty) return;
        PlayerPrefs.Save();
        dirty = false;
    }

    private void Load()
    {
        readKeys.Clear();
        if (!PlayerPrefs.HasKey(PrefsKey)) return;

        var data = JsonUtility.FromJson<ReadProgressData>(PlayerPrefs.GetString(PrefsKey));
        if (data?.keys == null) return;

        foreach (var key in data.keys)
        {
            if (!string.IsNullOrEmpty(key)) readKeys.Add(key);
        }
    }
}
