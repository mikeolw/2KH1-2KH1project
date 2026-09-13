using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 장면 구성표 - 장면 ID 하나가 "어떤 배경 위에 어떤 소품이 놓이는지"를 담는다
// =====================================================================================
// ===== 왜 따로 떼어냈나? =====
// 예전에는 시나리오 CSV의 Background / Props 칸에 파일 이름을 직접 적었다. 그런데 시나리오
// CSV에는 대사가 들어 있어서 공개 저장소(git)에 올릴 수 없고 드라이브로만 주고받는다.
// 그래서 일러스트 배치 도구로 장면의 배경이나 소품을 고칠 때마다 시나리오 파일을 드라이브에
// 다시 올려야 했다.
//
// 이제 "그 장면에 무엇이 놓이는지"는 스토리가 없는 이 표(git으로 공유)에 두고,
// 시나리오 CSV는 Scene 칸에 장면 ID만 적는다.
//   시나리오 CSV (드라이브) : ..., Scene=S_01_Office_A, Standing=STD_0107_Conductor_Angry1, ...
//   SceneStage.csv (git)    : S_01_Office_A, BG_01_Office, (소품 목록)
// 배치 도구는 이 표만 고치므로 시나리오 파일을 건드리지 않는다.
//
// ===== 캐릭터 스탠딩은 여기 없다 =====
// 누가 서 있고 어떤 표정인지는 대사마다 바뀌는 연출이라 작가가 시나리오 CSV의 Standing 칸에서
// 직접 관리한다. 따로 떼어내면 두 파일을 대조해야 해서 오히려 번거롭기 때문이다.
// 장면 도중에 인물이 들어오거나 나가는 것도 Standing 칸만 바꾸면 되고, 새 장면 ID는 필요 없다.
//
// ===== 파일 =====
// Assets/StreamingAssets/Stage/SceneStage.csv   (git으로 공유 - 스토리 없음)
//   SceneId    : 장면 ID. 시나리오 CSV의 Scene 칸과 글자까지 똑같아야 한다.
//                내용이나 순서가 드러나지 않게 "S_배경이름_알파벳"으로 짓는다 (예: S_01_Office_A).
//   Background : 배경 파일 이름 (Resources/Illusts/Backgrounds/ 기준)
//   Props      : 소품 파일 이름. 여러 개면 세로줄(|)로 구분. 없으면 비워둔다.
//
// ===== 시나리오 CSV의 Scene 칸 =====
//   (빈칸)  : 이전 장면 그대로 유지
//   none    : 배경과 소품을 지운다 (검은 화면)
//   장면 ID : 그 장면의 배경과 소품으로 바꾼다
public static class SceneStage
{
    public struct Composition
    {
        public string background;
        public string props;   // "OBJ_A|OBJ_B" 형태. 소품이 없으면 ""
    }

    private const string SceneStageCsv = "Stage/SceneStage";

    private static Dictionary<string, Composition> scenes;

    // 같은 경고를 대사마다 반복해서 찍지 않도록 한 번 알린 ID를 기억한다.
    private static readonly HashSet<string> warnedMissing = new HashSet<string>();

    private static void EnsureLoaded()
    {
        if (scenes != null) return;
        scenes = new Dictionary<string, Composition>();

        var rows = CSVReader.Read(SceneStageCsv);
        if (rows == null || rows.Count == 0) return;   // 파일이 없으면 CSVReader가 이미 오류를 남겼다

        foreach (var row in rows)
        {
            string id = GetField(row, "SceneId").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            if (scenes.ContainsKey(id))
            {
                Debug.LogWarning($"[SceneStage] 장면 ID '{id}'가 두 번 적혀 있습니다. 뒤에 적힌 줄을 씁니다.");
            }

            scenes[id] = new Composition
            {
                background = GetField(row, "Background").Trim(),
                props = GetField(row, "Props").Trim()
            };
        }
    }

    private static string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // 장면 ID로 구성을 찾는다. 없으면 false (한 번만 경고한다).
    public static bool TryGet(string sceneId, out Composition composition)
    {
        EnsureLoaded();
        composition = default;

        if (string.IsNullOrWhiteSpace(sceneId)) return false;
        string id = sceneId.Trim();

        if (scenes.TryGetValue(id, out composition)) return true;

        if (warnedMissing.Add(id))
        {
            Debug.LogWarning($"[SceneStage] 장면 ID '{id}'를 {SceneStageCsv}.csv에서 찾지 못했습니다. " +
                             "시나리오 CSV의 Scene 칸 철자를 확인하거나, 일러스트 배치 도구로 그 장면을 만들어주세요. " +
                             "(이 줄은 이전 장면을 그대로 유지합니다)");
        }
        return false;
    }

    // 배치 도구에서 표를 고친 뒤 게임을 다시 시작하지 않고 반영하고 싶을 때 쓴다.
    public static void Reload()
    {
        scenes = null;
        warnedMissing.Clear();
        EnsureLoaded();
    }
}
