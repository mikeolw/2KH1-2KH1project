using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 일러스트(PNG) 로더 - Resources/Illusts/ 아래의 그림을 "파일 이름"으로 불러온다
// =====================================================================================
// ===== 왜 이런 방식인가? (중요) =====
// 이 프로젝트의 그림/사운드/CSV는 전부 Assets/Resources/ 폴더 아래에 들어가는데, 이 폴더는
// .gitignore에 등록되어 있어서 git으로 공유되지 않는다 (구글 드라이브로 따로 주고받는다).
// 그래서 "인스펙터에서 Sprite를 드래그해서 꽂아두는" 일반적인 유니티 방식을 쓸 수 없다.
//
//   왜 못 쓰냐면: 유니티는 인스펙터로 연결한 그림을 "파일 경로"가 아니라 .meta 파일 안의
//   GUID(고유 번호)로 기억한다. 그런데 Resources 폴더가 git에 안 올라가니 .meta도 공유가
//   안 되고, 팀원이 드라이브에서 같은 PNG를 새로 받아 넣으면 유니티가 그 사람 PC에서
//   "새로운 GUID"를 만들어버린다. 결과적으로 A가 씬에 연결해둔 그림이 B의 PC에서는
//   Missing(빠짐)으로 깨진다.
//
// 그래서 그림도 BGM/효과음(DialogueSystem이 Resources.Load<AudioClip>로 부르는 방식)과
// 똑같이 "파일 이름 문자열"로 불러온다. 문자열은 CSV에 적혀 있고, CSV는 텍스트라
// GUID 문제가 아예 없다.
//
// ===== 폴더 규칙 =====
//   Assets/Resources/Illusts/Backgrounds/BG_01_Office.png      -> "BG_01_Office"
//   Assets/Resources/Illusts/Objects/OBJ_01_Notepad.png        -> "OBJ_01_Notepad"
//   Assets/Resources/Illusts/Standings/STD_Past01_Hansung_Default.png -> "STD_Past01_Hansung_Default"
// CSV에는 확장자(.png)와 폴더 경로를 빼고 파일 이름만 적으면 된다.
// (BGM/SFX를 CSV에 적을 때와 똑같은 규칙이라 헷갈릴 일이 없다.)
//
// ===== 캐시(cache)를 두는 이유 =====
// Resources.Load()는 호출할 때마다 파일을 찾아보기 때문에, 대사 한 줄 넘길 때마다 매번
// 부르면 조금씩 느려진다. 한 번 불러온 그림은 아래 Dictionary에 기억해두고 두 번째부터는
// 즉시 꺼내 쓴다. (게임 하나 도는 동안 그림 개수가 140장 정도라 메모리 부담은 없다.)
public static class IllustLoader
{
    // Resources 폴더 안에서의 경로 접두사. 폴더 이름을 바꾸게 되면 여기만 고치면 된다.
    public const string BackgroundFolder = "Illusts/Backgrounds/";
    public const string ObjectFolder = "Illusts/Objects/";
    public const string StandingFolder = "Illusts/Standings/";

    // 이미 불러온 그림을 기억해두는 캐시. key는 "폴더+파일이름"(= Resources 기준 전체 경로).
    // 값이 null인 것도 그대로 기억한다 - "찾아봤지만 없더라"는 사실도 캐시해야 없는 파일을
    // 매 줄마다 반복해서 찾는 낭비를 막을 수 있기 때문이다.
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // 이미 "없다"고 경고를 띄운 경로. 같은 오타 때문에 콘솔이 수백 줄 도배되는 걸 막는다.
    private static readonly HashSet<string> warnedPaths = new HashSet<string>();

    public static Sprite LoadBackground(string fileName) => Load(BackgroundFolder, fileName);
    public static Sprite LoadObject(string fileName) => Load(ObjectFolder, fileName);
    public static Sprite LoadStanding(string fileName) => Load(StandingFolder, fileName);

    // 실제 로딩 담당. folder는 위 상수 중 하나, fileName은 확장자 없는 파일 이름.
    // 파일이 없으면 null을 반환하고 경고만 남긴다 - 그림 하나 없다고 게임이 멈추면
    // 곤란하므로(특히 아직 아트가 안 나온 부분), 예외를 던지지 않는다.
    public static Sprite Load(string folder, string fileName)
    {
        return Load(folder, fileName, warnIfMissing: true);
    }

    // warnIfMissing를 false로 주면 파일이 없어도 조용히 null만 돌려준다.
    // "있으면 좋고 없어도 그만"인 그림(예: 입 벌린 표정)을 찾을 때 쓴다.
    // 이걸 구분하지 않았더니 입 벌린 그림이 없는 캐릭터마다 경고가 쌓여서
    // 콘솔이 의미 없는 경고로 가득 찼다.
    public static Sprite Load(string folder, string fileName, bool warnIfMissing)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        // CSV에서 복사/붙여넣기 하다 보면 앞뒤에 공백이 섞이는 일이 잦아서 미리 잘라낸다.
        fileName = fileName.Trim();

        // 실수로 확장자까지 적은 경우(예: "BG_01_Office.png")도 알아서 받아준다.
        // Resources.Load는 확장자를 붙이면 못 찾기 때문에 여기서 떼어낸다.
        if (fileName.EndsWith(".png") || fileName.EndsWith(".jpg"))
        {
            fileName = fileName.Substring(0, fileName.LastIndexOf('.'));
        }

        string path = folder + fileName;

        if (cache.TryGetValue(path, out Sprite cached)) return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        cache[path] = sprite;

        if (sprite == null && warnIfMissing && warnedPaths.Add(path))
        {
            Debug.LogWarning(
                $"[IllustLoader] 그림을 찾을 수 없습니다: Assets/Resources/{path}.png\n" +
                "  - CSV에 적은 파일 이름의 철자/대소문자가 실제 파일과 같은지 확인하세요.\n" +
                "  - 구글 드라이브에서 받은 그림을 Assets/Resources/Illusts/ 아래 알맞은 폴더에 " +
                "넣었는지 확인하세요.");
        }

        return sprite;
    }

    // 같은 이름 뒤에 "_OpenMouse"가 붙은 "입 벌린" 그림이 있는지 찾아본다.
    // 캐릭터 스탠딩의 입 뻐끔(립싱크) 연출에 쓴다 - StandingSlot.cs 참고.
    // 없으면 null을 반환하며, 이 경우 그냥 입을 안 움직이는 캐릭터가 된다(에러 아님).
    public static Sprite LoadOpenMouthVariant(string standingFileName)
    {
        if (string.IsNullOrWhiteSpace(standingFileName)) return null;

        // 입 벌린 그림은 있는 캐릭터도 있고 없는 캐릭터도 있다. 없는 게 정상적인 경우이므로
        // 경고를 남기지 않는다 (warnIfMissing: false).
        return Load(StandingFolder, standingFileName.Trim() + "_OpenMouse", warnIfMissing: false);
    }

    // 씬을 완전히 새로 시작할 때처럼 메모리를 정리하고 싶을 때 호출한다.
    // (지금은 부르는 곳이 없지만, 나중에 챕터별로 씬을 나누게 되면 쓸 수 있다.)
    public static void ClearCache()
    {
        cache.Clear();
        warnedPaths.Clear();
    }
}
