using UnityEngine;

// =====================================================================================
// 화면 색 (청회색 테마) - 모든 UI가 같이 쓰는 색은 여기 한 곳에만 적는다
// =====================================================================================
// 예전에는 같은 색 값(예: 강조색 8FA4B7)이 화면 스크립트마다 따로 적혀 있어서, 색을 바꾸려면
// 파일 8~9개를 다 찾아 고쳐야 했다. 이제 여기 값을 바꾸면 전부 같이 바뀐다.
//
// 이름은 "어디에 쓰는 색인가"로 붙였다. 새 화면을 만들 때 아래에서 골라 쓰면 된다.
//
//   글자    Title(제목) > Active(고른 것·강조 글자) > Body(본문) > Label(항목 이름) >
//           ButtonText(버튼 글자·아이콘) > Narration(지문) > Hint(안내) > Muted(흐린 글자) > Kicker(머리말)
//   강조    Accent(포인트 선·막대·마우스 올림 테두리)
//   선      Divider(구분선) < Line(테두리) < LineStrong(진한 테두리) < LineHover(마우스 올림 테두리)
//   바탕    Panel(창 바탕) / Surface(칸·슬롯 바탕) / SurfaceHover(마우스 올림·선택된 칸) / OnFill(켜진 토글)
//   막      ScreenDim(퀵바 창·설정 등 뒤 화면을 가리는 검은 막 97%)
public static class UITheme
{
    // ----- 글자 -----
    public static readonly Color Title      = Hex(0xE4EAEF);
    public static readonly Color Active     = Hex(0xD9E3EB);
    public static readonly Color Body       = Hex(0xD2DAE1);
    public static readonly Color Label      = Hex(0xC9D5E0);
    public static readonly Color ButtonText = Hex(0xA9BAC9);
    public static readonly Color Narration  = Hex(0x93A1AE);
    public static readonly Color Hint       = Hex(0x7B8A98);
    public static readonly Color Muted      = Hex(0x6C7C8B);
    public static readonly Color Kicker     = Hex(0x6F8496);
    public static readonly Color PrimaryText = Hex(0x0F151B);   // 밝은 주 버튼 위의 어두운 글자

    // ----- 강조 -----
    public static readonly Color Accent = Hex(0x8FA4B7);

    // ----- 선 -----
    public static readonly Color Divider    = Hex(0x2A3540);
    public static readonly Color Line       = Hex(0x34414D);
    public static readonly Color LineStrong = Hex(0x3A4855);
    public static readonly Color LineHover  = Hex(0x5A6B7B);

    // ----- 바탕 -----
    public static readonly Color Panel        = Hex(0x10161D, 0.95f);
    public static readonly Color Surface      = Hex(0x0B1015);
    public static readonly Color SurfaceHover = Hex(0x18222D);
    public static readonly Color OnFill       = Hex(0x243140);

    // ----- 막 -----
    public static readonly Color ScreenDim = new Color(0f, 0f, 0f, 0.97f);

    // 0xRRGGBB → Color. Figma 색 값을 그대로 옮겨 적을 수 있게.
    public static Color Hex(int rgb, float alpha = 1f) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    // 같은 색에 투명도만 바꾼 것
    public static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
}
