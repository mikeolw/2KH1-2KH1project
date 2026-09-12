using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 일러스트 배치표 - 각 그림이 1440x1080 화면의 "어디에" 놓여야 하는지를 담는다
// =====================================================================================
// ===== 왜 이런 게 필요한가? (핵심) =====
// 배경(BG_*)은 전부 1440x1080이라 화면에 꽉 채워 깔면 끝이다. 그런데 조사 오브젝트(OBJ_*)와
// 캐릭터 스탠딩(STD_*)은 그림 주변의 투명한 여백이 잘려나간 상태로 저장되어 있다.
// 예를 들어 OBJ_01_Cabinet.png는 534x91짜리 가늘고 긴 조각이다.
//
// 원래 이 조각들은 1440x1080 화면의 특정 위치에 딱 맞게 그려진 것인데, 여백을 잘라내면서
// "화면 어디에 있었는지"라는 정보가 파일에서 사라져 버렸다. 그래서 코드가 알아서 놓을 방법이
// 없고, 그냥 화면 가운데에 놓으면 배경과 전혀 맞지 않는다.
//
// 이 배치표는 그 잃어버린 위치 정보를 따로 적어두는 곳이다.
//
// ===== 두 가지 해결 방법 =====
//  방법 1 (권장) : 아트 담당자가 그림을 "캔버스 크기 그대로"(1440x1080, 여백 투명) 내보낸다.
//                  그러면 위치 정보가 그림 안에 그대로 남아 있으므로 배치표가 필요 없다.
//                  이 코드는 1440x1080짜리 그림을 발견하면 자동으로 화면에 꽉 채워 깔아서
//                  원래 위치 그대로 보이게 한다. (아래 IsFullCanvas 참고)
//  방법 2        : 이미 잘려 있는 그림은 이 배치표에 X, Y를 적어준다.
//                  유니티 상단 메뉴 [2KH1] > [일러스트 배치 도구]를 열면 배경 위에서
//                  마우스로 끌어 위치를 맞추고 저장할 수 있다(IllustPlacementWindow.cs).
//
// ===== 파일 =====
// Assets/StreamingAssets/Dialogues/IllustLayout.csv  (git으로 공유 - 드라이브에 올릴 필요 없음)
//   FileName : 그림 파일 이름 (확장자 제외). 예: OBJ_01_Notepad
//   X, Y     : 화면 한가운데를 (0,0)으로 봤을 때의 위치(픽셀). X는 오른쪽이 +, Y는 위쪽이 +.
//              그림의 한가운데가 이 좌표에 오도록 놓인다.
//   Scale    : 크기 배율. 비우면 1(원본 크기).
//   Screen   : (선택) 이 좌표를 적용할 화면. 배경 파일 이름(=조사 화면 이름)을 적는다.
//              비워두면 "모든 화면 공통 기본값"이 된다. 아래 [화면별 좌표] 참고.
//
// =====================================================================================
// ===== 새로 추가된 기능 1 : 화면별 좌표 (Screen 열) =====
// =====================================================================================
// 예전에는 그림 파일 하나당 좌표가 딱 하나뿐이었다. 그래서 같은 오브젝트(예: OBJ_01_Camera)가
// 사무실 배경과 집 배경 양쪽에 나오면 두 화면에서 같은 자리에만 놓을 수 있었다.
//
// 이제 Screen 열에 배경 이름을 적으면 그 화면에서만 쓰는 좌표를 따로 둘 수 있다.
//
//   FileName,X,Y,Scale,Screen
//   OBJ_01_Camera,100,-200,1,              ← 기본값 (Screen이 비었으므로 모든 화면 공통)
//   OBJ_01_Camera,-300,50,1,BG_04_Home     ← 집 배경에서만 이 좌표를 쓴다
//
// Screen을 아예 안 쓰면 예전과 100% 똑같이 동작한다(기존 CSV 그대로 호환).
//
// =====================================================================================
// ===== 새로 추가된 기능 2 : 표정 상속 (이름 계단식 찾기) =====
// =====================================================================================
// 캐릭터 스탠딩은 표정만 다른 그림이 아주 많다. 예를 들어 한성이 하나만 해도
//   STD_Past05_Hansung_Default
//   STD_Past05_Hansung_Default_OpenMouse   (입 벌린 립싱크용)
//   STD_Past05_Hansung_Angry
//   STD_Past05_Hansung_Angry_OpenMouse
//   STD_Past05_Hansung_Sorry ... 이런 식으로 10장이 넘는다.
// 이것들은 전부 "같은 캐릭터가 같은 자리에 서 있고 얼굴만 다른" 그림이라 좌표가 똑같다.
// 그런데 예전에는 파일 이름이 다르면 남남이라, 표정 하나하나를 전부 따로 배치해야 했다.
// 스탠딩 58장을 전부 손으로 잡아야 하는 셈이라 너무 번거로웠다.
//
// 이제는 정확한 이름으로 못 찾으면 이름을 뒤에서부터 한 토막씩 잘라가며 다시 찾는다.
//
//   STD_Past05_Hansung_Angry_OpenMouse   ← 이 그림을 찾는다면
//   → STD_Past05_Hansung_Angry           ← 없으면 _OpenMouse를 떼고
//   → STD_Past05_Hansung                 ← 그래도 없으면 표정을 떼고 (여기까지가 끝)
//
// 즉 **캐릭터 한 명당 STD_Past05_Hansung 한 줄만 잡아두면 그 캐릭터의 모든 표정이
// 자동으로 같은 자리에 선다.** 특정 표정만 자세가 달라서 좌표를 따로 주고 싶으면
// 그 표정 이름으로 한 줄 더 적으면 되고, 그게 기본값보다 우선한다.
//
// 자르는 것은 "STD_장면_캐릭터" 세 토막까지만 한다. 그보다 더 자르면 다른 캐릭터끼리
// 좌표를 잘못 공유하게 되기 때문이다(예: STD_Past05 까지 자르면 한성과 재훈이 섞인다).
// OBJ_01_Cabinet처럼 원래 세 토막인 이름은 자를 것이 없으므로 예전과 똑같이 동작한다.
//
// ===== 찾는 순서 (우선순위) =====
//   1) 이 화면 + 정확한 이름     ← 가장 구체적
//   2) 공통      + 정확한 이름
//   3) 이 화면 + 잘라낸 이름
//   4) 공통      + 잘라낸 이름   ← 가장 일반적
// "정확히 그 파일 이름을 적어둔 줄"이 "표정을 잘라서 찾은 줄"보다 항상 우선한다.
// 파일 이름을 콕 집어 적었다면 그건 의도적으로 그 그림만 다르게 놓겠다는 뜻이기 때문이다.
public static class IllustLayout
{
    // 배치 정보 하나.
    public struct Placement
    {
        public float x;
        public float y;
        public float scale;

        public Vector2 Position => new Vector2(x, y);
    }

    // 게임 화면의 기준 크기. 배경 그림이 이 크기로 그려져 있다.
    public const float CanvasWidth = 1440f;
    public const float CanvasHeight = 1080f;

    // 이름을 뒤에서부터 자를 때 "여기까지만 자른다"는 최소 토막 수.
    // STD_Past05_Hansung = 3토막. 이보다 더 자르면 캐릭터가 섞이므로 멈춘다.
    private const int MinimumNameTokens = 3;

    // ===== 예외: "_Stand"가 붙은 것은 별개의 스탠딩으로 본다 =====
    // STD_Past05_Hansung_Stand_Default 처럼 캐릭터 이름 바로 뒤에 Stand가 오는 그림은
    // "같은 캐릭터지만 자세(서 있는 포즈)가 아예 다른 그림"이다. 얼굴만 다른 표정들과 달리
    // 화면에서 서는 자리도 크기도 다르므로, STD_Past05_Hansung 의 좌표를 물려받으면 안 된다.
    // 그래서 이런 이름은 4토막(STD_장면_캐릭터_Stand)까지만 자르고 멈춰서,
    // _Stand 계열끼리만 좌표를 공유하고 일반 표정들과는 완전히 분리한다.
    //
    //   STD_Past05_Hansung_Stand_Default          ┐ 이 둘은 STD_Past05_Hansung_Stand 를 공유
    //   STD_Past05_Hansung_Stand_Default_OpenMouse ┘
    //   STD_Past05_Hansung_Default                ┐ 이쪽은 STD_Past05_Hansung 를 공유
    //   STD_Past05_Hansung_Angry                  ┘  (위 _Stand 계열과 서로 영향 없음)
    private const string StandToken = "Stand";

    private const string LayoutCsv = "Dialogues/IllustLayout";

    // "화면\0파일이름" -> 배치 정보. 화면이 비어 있으면(공통 기본값) 앞부분이 빈 문자열이다.
    // Dictionary 키에 두 값을 합쳐 넣기 위해 파일 이름에 절대 나오지 않는 '\0'으로 이어 붙인다.
    private static Dictionary<string, Placement> layouts;

    private static string MakeKey(string screen, string fileName)
    {
        return (screen ?? "") + "\0" + (fileName ?? "");
    }

    private static void EnsureLoaded()
    {
        if (layouts != null) return;
        layouts = new Dictionary<string, Placement>();

        var rows = CSVReader.Read(LayoutCsv);
        if (rows == null || rows.Count == 0)
        {
            // 배치표가 없어도 게임은 돌아간다(그림이 화면 가운데에 놓일 뿐).
            // 아직 배치를 안 잡은 초기 상태에서도 실행은 되어야 하므로 경고 수준을 낮춘다.
            Debug.Log($"[IllustLayout] {LayoutCsv}.csv가 없습니다. " +
                      "잘려 있는 그림은 화면 가운데에 놓입니다. " +
                      "유니티 상단 메뉴 [2KH1] > [일러스트 배치 도구]로 위치를 잡을 수 있습니다.");
            return;
        }

        foreach (var row in rows)
        {
            string name = GetField(row, "FileName").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            float.TryParse(GetField(row, "X").Trim(), out float x);
            float.TryParse(GetField(row, "Y").Trim(), out float y);

            // Scale은 비어 있으면 1(원본 크기)로 본다.
            if (!float.TryParse(GetField(row, "Scale").Trim(), out float scale) || scale <= 0f)
            {
                scale = 1f;
            }

            // Screen 열은 나중에 추가된 것이라, 없는 CSV(예전 파일)에서는 ""가 된다.
            // ""는 "모든 화면 공통"이라는 뜻이므로 예전 CSV가 그대로 동작한다.
            string screen = GetField(row, "Screen").Trim();

            layouts[MakeKey(screen, name)] = new Placement { x = x, y = y, scale = scale };
        }

        Debug.Log($"[IllustLayout] 배치 정보 {layouts.Count}개를 읽었습니다.");
    }

    private static string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // 이름 계단식 후보 만들기 (표정 상속)
    // ---------------------------------------------------------------------------------
    // "STD_Past05_Hansung_Angry_OpenMouse"를 넣으면
    //   [STD_Past05_Hansung_Angry_OpenMouse, STD_Past05_Hansung_Angry, STD_Past05_Hansung]
    // 순서로 돌려준다. 앞엣것부터 찾아보라는 뜻이다.
    //
    // 이 함수는 배치 도구(IllustPlacementWindow)에서도 "이 그림이 어느 줄을 물려받는지"를
    // 보여주는 데 쓰므로 public으로 열어둔다.
    public static List<string> NameCandidates(string fileName)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(fileName)) return candidates;

        string name = fileName.Trim();
        candidates.Add(name);

        // 립싱크용 "_OpenMouse"는 입만 벌린 같은 그림이라 항상 짝(입 다문 쪽)을 물려받아야 한다.
        // 그래서 다른 것보다 먼저 떼어본다.
        const string OpenMouthSuffix = "_OpenMouse";
        if (name.EndsWith(OpenMouthSuffix, System.StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - OpenMouthSuffix.Length);
            if (!candidates.Contains(name)) candidates.Add(name);
        }

        // 남은 이름을 뒤에서부터 한 토막씩(밑줄 기준) 잘라가며 후보에 넣는다.
        var tokens = new List<string>(name.Split('_'));

        // 어디까지 자를지 정한다. 기본은 3토막(STD_장면_캐릭터)이지만,
        // 네 번째 토막이 정확히 "Stand"면 4토막(STD_장면_캐릭터_Stand)에서 멈춘다.
        // (위 StandToken 주석 참고. "DefaultStand"처럼 다른 글자가 붙은 것은 Stand가 아니다.)
        int floorTokens = MinimumNameTokens;
        if (tokens.Count > MinimumNameTokens &&
            string.Equals(tokens[MinimumNameTokens], StandToken, System.StringComparison.OrdinalIgnoreCase))
        {
            floorTokens = MinimumNameTokens + 1;
        }

        while (tokens.Count > floorTokens)
        {
            tokens.RemoveAt(tokens.Count - 1);
            string shorter = string.Join("_", tokens);
            if (!candidates.Contains(shorter)) candidates.Add(shorter);
        }

        return candidates;
    }

    // ---------------------------------------------------------------------------------
    // 배치 정보 찾기
    // ---------------------------------------------------------------------------------

    // 배치 정보를 찾는다. 표에 없으면 false를 반환한다.
    //   fileName : 그림 파일 이름 (확장자 제외)
    //   screen   : 지금 화면(배경/조사화면 이름). 비워두면 공통 기본값만 본다.
    //
    // 찾는 순서는 이 파일 상단 주석의 [찾는 순서] 참고.
    public static bool TryGet(string fileName, string screen, out Placement placement)
    {
        EnsureLoaded();
        placement = default;

        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var candidates = NameCandidates(fileName);
        string screenKey = string.IsNullOrWhiteSpace(screen) ? "" : screen.Trim();

        // 1) 이 화면 + 정확한 이름  /  2) 공통 + 정확한 이름
        //    (candidates[0]이 원래 이름 그대로다)
        if (!string.IsNullOrEmpty(screenKey) &&
            layouts.TryGetValue(MakeKey(screenKey, candidates[0]), out placement)) return true;
        if (layouts.TryGetValue(MakeKey("", candidates[0]), out placement)) return true;

        // 3) 이 화면 + 잘라낸 이름  /  4) 공통 + 잘라낸 이름
        for (int i = 1; i < candidates.Count; i++)
        {
            if (!string.IsNullOrEmpty(screenKey) &&
                layouts.TryGetValue(MakeKey(screenKey, candidates[i]), out placement)) return true;
            if (layouts.TryGetValue(MakeKey("", candidates[i]), out placement)) return true;
        }

        return false;
    }

    // 화면을 따지지 않는 예전 방식. 공통 기본값만 본다.
    // (예전 코드가 그대로 컴파일되도록 남겨둔 것 - 새로 쓰는 코드는 위쪽 3개짜리를 쓸 것)
    public static bool TryGet(string fileName, out Placement placement)
    {
        return TryGet(fileName, null, out placement);
    }

    // 이 그림이 "캔버스 크기 그대로" 내보낸 것인지 확인한다.
    // 1440x1080이면 여백까지 포함된 그림이므로, 화면에 꽉 채워 깔기만 하면
    // 그림 안의 내용이 원래 위치에 정확히 나타난다(배치 정보가 필요 없다).
    public static bool IsFullCanvas(Sprite sprite)
    {
        if (sprite == null) return false;

        // 소수점 오차를 감안해 1픽셀까지는 같은 것으로 본다.
        return Mathf.Abs(sprite.rect.width - CanvasWidth) <= 1f
            && Mathf.Abs(sprite.rect.height - CanvasHeight) <= 1f;
    }

    // ===== 그림 하나를 RectTransform에 적용하는 공통 처리 =====
    // 조사 오브젝트(InvestigatableObject)와 캐릭터 스탠딩(StandingSlot)이 똑같은 규칙으로
    // 놓여야 해서 여기 한 곳에 모아두었다.
    //
    // 규칙:
    //   1) 1440x1080짜리 그림  -> 화면에 꽉 채운다 (여백 포함 그림이므로 위치가 이미 맞다)
    //   2) 배치표에 있는 그림  -> 표에 적힌 X, Y, Scale대로 놓는다 (표정 상속/화면별 좌표 포함)
    //   3) 둘 다 아닌 그림     -> fallbackPosition에 원본 크기로 놓는다 (임시 - 배치 도구로 잡아야 함)
    //
    // fallbackPosition: 배치표에 정보가 없을 때 쓸 위치. 캐릭터 스탠딩은 자기 자리(왼쪽/가운데/
    //   오른쪽)의 기본 좌표를 넘겨주면 최소한 세 명이 겹쳐 보이지는 않는다.
    //   생략하면 화면 한가운데(0,0)에 놓인다.
    // screen: 지금 화면(배경 이름). 넘겨주면 그 화면 전용 좌표를 먼저 찾는다.
    public static void Apply(RectTransform rect, Sprite sprite, string fileName,
                             Vector2 fallbackPosition = default, string screen = null)
    {
        if (rect == null || sprite == null) return;

        // 1) 캔버스 크기 그대로 내보낸 그림: 화면 전체에 늘려 깐다.
        if (IsFullCanvas(sprite))
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return;
        }

        // 2), 3) 잘려 있는 그림: 화면 한가운데를 기준으로 좌표를 잡는다.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

        if (TryGet(fileName, screen, out Placement p))
        {
            // ===== 왜 반올림하나? (그림이 흐려지는 것을 막는다) =====
            // 화면의 픽셀은 정수 단위인데 그림을 x=-448.89 같은 소수 자리에 놓으면, 그래픽카드가
            // 인접한 픽셀들을 섞어서(바이리니어 보간) 그리기 때문에 선이 한 픽셀 번져 보인다.
            // 특히 선화(線畵)는 이 번짐이 바로 눈에 띄어 "원화보다 흐리다"고 느끼게 된다.
            // 배치 도구가 미리보기 배율로 나눠 좌표를 만들다 보니 소수가 섞이는데, 여기서
            // 정수로 맞춰주면 예전에 저장해둔 CSV도 고칠 필요 없이 전부 선명해진다.
            rect.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            rect.localScale = new Vector3(p.scale, p.scale, 1f);
        }
        else
        {
            // 배치 정보가 아직 없는 그림. 넘겨받은 기본 위치에 원본 크기로 둔다.
            rect.anchoredPosition = new Vector2(Mathf.Round(fallbackPosition.x), Mathf.Round(fallbackPosition.y));
            rect.localScale = Vector3.one;
        }
    }

    // 배치표를 고친 뒤 게임을 다시 시작하지 않고 반영하고 싶을 때 (에디터 도구가 호출한다).
    public static void Reload()
    {
        layouts = null;
        EnsureLoaded();
    }
}
