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
// Assets/Resources/Dialogues/IllustLayout.csv
//   FileName : 그림 파일 이름 (확장자 제외). 예: OBJ_01_Notepad
//   X, Y     : 화면 한가운데를 (0,0)으로 봤을 때의 위치(픽셀). X는 오른쪽이 +, Y는 위쪽이 +.
//              그림의 한가운데가 이 좌표에 오도록 놓인다.
//   Scale    : 크기 배율. 비우면 1(원본 크기).
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

    private const string LayoutCsv = "Dialogues/IllustLayout";

    // 파일 이름 -> 배치 정보
    private static Dictionary<string, Placement> layouts;

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

            layouts[name] = new Placement { x = x, y = y, scale = scale };
        }

        Debug.Log($"[IllustLayout] 배치 정보 {layouts.Count}개를 읽었습니다.");
    }

    private static string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // 배치 정보를 찾는다. 표에 없으면 false를 반환한다.
    public static bool TryGet(string fileName, out Placement placement)
    {
        EnsureLoaded();
        placement = default;

        if (string.IsNullOrWhiteSpace(fileName)) return false;
        return layouts.TryGetValue(fileName.Trim(), out placement);
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
    //   2) 배치표에 있는 그림  -> 표에 적힌 X, Y, Scale대로 놓는다
    //   3) 둘 다 아닌 그림     -> fallbackPosition에 원본 크기로 놓는다 (임시 - 배치 도구로 잡아야 함)
    //
    // fallbackPosition: 배치표에 정보가 없을 때 쓸 위치. 캐릭터 스탠딩은 자기 자리(왼쪽/가운데/
    //   오른쪽)의 기본 좌표를 넘겨주면 최소한 세 명이 겹쳐 보이지는 않는다.
    //   생략하면 화면 한가운데(0,0)에 놓인다.
    public static void Apply(RectTransform rect, Sprite sprite, string fileName, Vector2 fallbackPosition = default)
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

        if (TryGet(fileName, out Placement p))
        {
            rect.anchoredPosition = p.Position;
            rect.localScale = new Vector3(p.scale, p.scale, 1f);
        }
        else
        {
            // 배치 정보가 아직 없는 그림. 넘겨받은 기본 위치에 원본 크기로 둔다.
            rect.anchoredPosition = fallbackPosition;
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
