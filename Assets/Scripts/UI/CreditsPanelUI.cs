using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static UIKit;   // CreateWindow, CreateHeader, CreateCloseButton, CreateFootHint, PlaceTopLeft ... (UIKit.cs)

// =====================================================================================
// 크레딧 화면 - 만든 사람들 + 글꼴 라이선스
// =====================================================================================
// ===== 왜 따로 만드나 =====
// 환경설정([정보] 탭 > 크레딧 [보기])에서 열린다. 크레딧은 "설정값"이 아니라 읽는 글이라
// 설정 항목 줄(이름 + 조작 칸) 형태에 맞지 않고, 글이 길어 스크롤이 필요하다.
// 그래서 설정 창 위에 겹쳐 뜨는 별도 창으로 만든다.
//   - UILayer.Detail(30)에 놓으므로 설정 창(Panel=20)보다 위에 그려진다.
//   - ModalStack에 나중에 등록되므로 Esc를 누르면 크레딧만 닫히고 설정 창은 그대로 남는다.
//
// ===== 글꼴 라이선스를 같이 넣는 이유 =====
// 이 게임의 메인 글꼴 Neo둥근모는 SIL Open Font License 1.1 이다. OFL 제2항은
// "글꼴을 재배포(게임에 넣어 배포하는 것도 포함)할 때 저작권 표시와 라이선스 전문을
// 함께 담아야 한다"고 요구한다. 지키지 않으면 TERMINATION 조항에 따라 사용 권한이
// 사라진다. 그래서 별도 텍스트 파일을 같이 배포하는 대신 게임 안에서 전문을 볼 수 있게
// 했다. 이러면 빌드 한 덩어리만 넘겨도 조건이 충족된다.
//
// ===== 글자 자리를 왜 레이아웃 그룹에 맡기나 =====
// 환경설정의 글씨 크기(0.7~1.6배)에 따라 글자가 커지고 줄 수가 달라진다. 자리를 코드가
// 한 번 재서 박아 두면 글씨를 키웠을 때 글이 잘리거나 겹친다(탭에서 실제로 겪은 문제).
// 그래서 세로 줄 세우기는 VerticalLayoutGroup, 전체 높이는 ContentSizeFitter에 맡긴다.
// 글자 크기가 바뀌면 TextMeshPro가 스스로 "다시 재 달라"고 알려서 자동으로 맞춰진다.
//
// ===== 명단 고치는 법 =====
// 아래 Credits 표 한 줄만 고치면 된다. Section을 빈 문자열("")로 두면 위 줄과 같은
// 묶음으로 이어 붙는다.
public class CreditsPanelUI : MonoBehaviour
{
    public static CreditsPanelUI Instance;

    // ===== 명단 =====
    // (묶음 제목, 역할, 이름). 묶음 제목이 ""이면 위 줄의 묶음에 이어진다.
    // 역할 이름은 업계 표기(영문)를 쓰고, 묶음 제목도 영문 대문자로 통일한다.
    private static readonly (string Section, string Role, string Names)[] Credits =
    {
        ("DIRECTION",   "Director",             "흑성게"),
        ("",            "Producer",             "M"),

        ("SCENARIO",    "Scenario",             "흑성게"),

        ("GAME DESIGN", "System Design",        "흑성게, M, UTF-8"),
        ("",            "UI / UX Design",       "M"),

        ("ART",         "Art Director",         "흑성게"),
        ("",            "Illustration",         "흑성게, M"),

        ("AUDIO",       "Music & Sound Design", "UTF-8"),

        ("PROGRAMMING", "Programming",          "흑성게, M, UTF-8"),
    };

    // ===== 글꼴 라이선스 =====
    // 전문은 Assets/TextMesh Pro/Resources/Fonts & Materials/NeoDunggeunmo-OFL.txt 에 있다.
    // 그 폴더가 Resources 폴더라서 이 경로로 런타임에 읽히고 빌드에도 들어간다.
    // (글꼴 에셋 NeoDunggeunmo.asset 과 같은 자리에 둬서 짝이 흩어지지 않게 했다.)
    private const string LicenseResourcePath = "Fonts & Materials/NeoDunggeunmo-OFL";

    private const string FontTitle = "Neo둥근모 (NeoDunggeunmo)";
    private const string FontNotice =
        "Copyright (c) 2017-2021, Eunbin Jeong (Dalgona.)\n" +
        "Licensed under the SIL Open Font License, Version 1.1";

    // 전문 파일을 못 읽었을 때 최소한 이것만이라도 보여준다.
    // (아무것도 안 보이면 라이선스 조건을 못 지킨 상태가 되므로 조용히 비워두지 않는다.)
    private const string LicenseFallback =
        "라이선스 전문을 불러오지 못했습니다.\n" +
        "전문은 https://openfontlicense.org 또는 게임 폴더의 NeoDunggeunmo-OFL.txt 에서 볼 수 있습니다.";

    // ===== 화면 크기 기준값 (환경설정과 같은 틀) =====
    private const float BoxWidth = 960f;
    private const float BoxHeight = 800f;
    private const float Pad = 64f;
    private const float ScrollTop = 170f;      // 머리말 아래
    private const float ScrollBottom = 110f;   // 꼬리말 위
    private const float RoleColumnRatio = 0.42f;   // 역할 칸이 차지하는 가로 비율
    private const float ColumnGap = 20f;

    private GameObject panel;
    private ScrollRect scroll;

    // 다른 스크립트가 "지금 크레딧이 열려 있나?"를 확인할 때 쓴다.
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        Build();
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 여닫기
    // ---------------------------------------------------------------------------------

    // 어디서든 이 한 줄로 띄운다. 아직 없으면 알아서 만든다.
    public static void Show()
    {
        if (Instance == null) new GameObject("CreditsPanel").AddComponent<CreditsPanelUI>();
        if (Instance != null) Instance.Open();
    }

    public void Open()
    {
        if (panel == null) return;

        panel.SetActive(true);

        // 다시 열었을 때 지난번에 내려둔 자리가 아니라 맨 위에서 시작하게 한다.
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 화면 만들기
    // ---------------------------------------------------------------------------------
    private void Build()
    {
        Canvas canvas = UILayers.RootCanvas();
        if (canvas == null)
        {
            Debug.LogError("[CreditsPanelUI] 씬에 Canvas가 없어 크레딧 화면을 만들 수 없습니다.");
            return;
        }

        // 설정 창(Panel=20) 위에 겹쳐 뜨는 창. Esc는 크레딧만 닫는다.
        panel = CreateWindow(canvas.transform, "CreditsPanel", UILayer.Detail, Close);

        // ----- 가운데 패널 -----
        // 환경설정과 같은 자리·같은 크기로 둬서 설정에서 넘어올 때 화면이 튀지 않게 한다.
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        boxImg.color = UITheme.Panel;
        boxImg.raycastTarget = false;

        AddBorder(boxRt, UITheme.Line);

        // ----- 머리말 / 닫기 -----
        CreateHeader(box.transform, Pad, 44f, "CREDITS", "만든 사람들",
                     out _, out _, BoxWidth - Pad * 2f - 80f);
        CreateCloseButton(box.transform, BoxWidth - Pad - 48f, 44f, Close);

        // ----- 스크롤 영역 -----
        float innerW = BoxWidth - Pad * 2f;
        float innerH = BoxHeight - ScrollTop - ScrollBottom;
        var content = BuildScroll(box.transform, Pad, ScrollTop, innerW, innerH);

        BuildCreditsSections(content, innerW);
        BuildLicenseSection(content, innerW);

        // ----- 꼬리말 -----
        CreateFootHint(box.transform, Pad, BoxHeight - ScrollBottom + 24f, innerW,
                       "마우스 휠 / 끌어서 넘기기 · Esc 닫기");

        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨진다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    // 스크롤 상자를 만들고, 글을 쌓을 Content를 돌려준다.
    // PhonePanelUI.BuildScroll과 같은 구성이지만, 높이를 ContentSizeFitter가 정한다는 점이 다르다.
    private RectTransform BuildScroll(Transform parent, float x, float y, float w, float h)
    {
        var viewGo = new GameObject("Scroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        viewGo.transform.SetParent(parent, false);
        var view = (RectTransform)viewGo.transform;
        PlaceTopLeft(view, x, y, w, h);

        // 빈 곳을 끌어도 스크롤되도록 투명 판을 깐다 (Image가 없으면 끌기가 안 먹는다).
        var catcher = new GameObject("Catcher", typeof(RectTransform), typeof(Image));
        catcher.transform.SetParent(view, false);
        Stretch((RectTransform)catcher.transform);
        var catcherImg = catcher.GetComponent<Image>();
        catcherImg.color = new Color(0f, 0f, 0f, 0f);
        catcherImg.raycastTarget = true;

        // Content: 위에서 아래로 쌓이고, 전체 높이는 내용에 맞춰 늘어난다.
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(view, false);
        var content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        var group = contentGo.AddComponent<VerticalLayoutGroup>();
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        group.childAlignment = TextAnchor.UpperLeft;
        group.spacing = 2f;
        group.padding = new RectOffset(0, 0, 0, 24);   // 맨 아래에 숨 쉴 자리

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 가로는 스크롤 폭에 맞춘다
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = viewGo.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        return content;
    }

    // ---------------------------------------------------------------------------------
    // 명단
    // ---------------------------------------------------------------------------------
    private void BuildCreditsSections(RectTransform content, float innerW)
    {
        for (int i = 0; i < Credits.Length; i++)
        {
            var (section, role, names) = Credits[i];

            // 묶음 제목이 있으면 먼저 놓는다. 첫 묶음만 위 여백을 주지 않는다.
            if (!string.IsNullOrEmpty(section))
                AddSectionTitle(content, section, topMargin: i == 0 ? 0f : 26f);

            AddCreditRow(content, role, names, innerW);
        }
    }

    // DIRECTION / SCENARIO ... 묶음 제목 (작은 영문 + 넓은 자간)
    private void AddSectionTitle(RectTransform content, string title, float topMargin)
    {
        var text = CreateLabel(content, $"Section_{title}", title, 14, FontStyles.Bold, UITheme.Kicker);
        text.characterSpacing = 20f;
        // TextMeshPro의 margin(왼,위,오른,아래)은 레이아웃 높이 계산에도 반영되므로
        // 빈 오브젝트로 간격을 만들지 않고 이것으로 묶음 사이를 띄운다.
        text.margin = new Vector4(0f, topMargin, 0f, 6f);
        text.overflowMode = TextOverflowModes.Overflow;
    }

    // "Director            흑성게" 한 줄.
    //
    // 한 덩어리 글자에 <pos=46%> 같은 태그로 두 칸을 만들면 글씨를 키웠을 때 왼쪽 글자가
    // 오른쪽 칸을 침범해 겹친다. 그래서 역할/이름을 각각 별도 글자로 두고 가로 레이아웃에
    // 맡긴다. 이러면 어느 쪽이 길어져도 그만큼 줄이 늘어날 뿐 겹치지 않는다.
    private void AddCreditRow(RectTransform content, string role, string names, float innerW)
    {
        var rowGo = new GameObject($"Row_{role}", typeof(RectTransform));
        rowGo.transform.SetParent(content, false);

        var row = rowGo.AddComponent<HorizontalLayoutGroup>();
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;   // 두 칸의 높이를 긴 쪽에 맞춘다
        row.childAlignment = TextAnchor.UpperLeft;
        row.spacing = ColumnGap;

        float roleW = Mathf.Floor((innerW - ColumnGap) * RoleColumnRatio);

        var roleText = MakeWrappedText(rowGo.transform, "Role", role, 19, FontStyles.Normal, UITheme.Muted);
        var roleElem = roleText.gameObject.AddComponent<LayoutElement>();
        roleElem.preferredWidth = roleW;
        roleElem.flexibleWidth = 0f;   // 역할 칸은 고정, 남는 자리는 이름 칸이 받는다

        var nameText = MakeWrappedText(rowGo.transform, "Names", names, 21, FontStyles.Bold, UITheme.Label);
        var nameElem = nameText.gameObject.AddComponent<LayoutElement>();
        nameElem.preferredWidth = innerW - ColumnGap - roleW;
        nameElem.flexibleWidth = 1f;
    }

    // ---------------------------------------------------------------------------------
    // 글꼴 라이선스
    // ---------------------------------------------------------------------------------
    private void BuildLicenseSection(RectTransform content, float innerW)
    {
        AddSectionTitle(content, "FONT", topMargin: 40f);

        var title = MakeWrappedText(content, "FontName", FontTitle, 21, FontStyles.Bold, UITheme.Label);
        title.margin = new Vector4(0f, 0f, 0f, 4f);

        var notice = MakeWrappedText(content, "FontNotice", FontNotice, 17, FontStyles.Normal, UITheme.Hint);
        notice.lineSpacing = 4f;
        notice.margin = new Vector4(0f, 0f, 0f, 14f);

        AddSectionTitle(content, "FONT LICENSE", topMargin: 20f);

        var body = MakeWrappedText(content, "LicenseBody", LoadLicenseText(), 14, FontStyles.Normal, UITheme.Hint);
        body.lineSpacing = 4f;
    }

    // 라이선스 전문을 읽어온다. 없으면 왜 없는지 로그로 알리고 최소 안내만 돌려준다.
    private static string LoadLicenseText()
    {
        var asset = Resources.Load<TextAsset>(LicenseResourcePath);
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) return asset.text.Replace("\r\n", "\n");

        Debug.LogWarning(
            "[CreditsPanelUI] 글꼴 라이선스 전문을 찾지 못했습니다. OFL 1.1 제2항(사본에 라이선스 전문 첨부)을 " +
            "지키려면 이 파일이 빌드에 들어가야 합니다.\n" +
            $"  찾은 경로: Resources/{LicenseResourcePath}\n" +
            "  실제 파일: Assets/TextMesh Pro/Resources/Fonts & Materials/NeoDunggeunmo-OFL.txt");
        return LicenseFallback;
    }

    // ---------------------------------------------------------------------------------
    // 도우미
    // ---------------------------------------------------------------------------------

    // 줄바꿈되는 글자. UIKit.CreateLabel은 한 줄 + "…"로 자르므로 긴 글에는 못 쓴다.
    private static TMP_Text MakeWrappedText(Transform parent, string name, string text,
                                            float size, FontStyles style, Color color)
    {
        var tmp = CreateText(parent, name, text, size, style, color);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;   // 잘리지 않고 아래로 늘어나게
        return tmp;
    }
}
