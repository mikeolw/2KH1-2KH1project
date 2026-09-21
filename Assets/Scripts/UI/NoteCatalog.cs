using System;
using System.Collections.Generic;

// =====================================================================================
// 수첩 메모를 "어느 탭에 넣을지 / 목록에 뭐라고 띄울지" 정하는 규칙 모음
// =====================================================================================
// NoteManager는 메모를 담아두기만 하고, NotePanelUI는 그리기만 한다. 그 사이에서
// "이 메모는 증언인가 증거인가", "목록에 보여줄 짧은 제목은 뭔가"를 판정하는 것이 여기다.
//
// MonoBehaviour가 아닌 순수 클래스로 둔 이유: 입력을 넣으면 답이 나오는 규칙 덩어리라
// 씬이나 화면과 아무 상관이 없고, 나중에 탭을 늘리거나 규칙을 고칠 때 UI를 건드리지
// 않아도 되기 때문이다. 에디터 점검 도구도 게임을 실행하지 않고 이 규칙을 그대로 쓴다.
public static class NoteCatalog
{
    public const string CategoryProgress = "사건경과";
    public const string CategoryEvidence = "증거";
    public const string CategoryTestimony = "증언";
    public const string CategoryWorkNote = "업무수첩";

    // 수첩 왼쪽에 세로로 놓이는 탭의 순서.
    public static readonly string[] Tabs =
    {
        CategoryProgress, CategoryEvidence, CategoryTestimony, CategoryWorkNote
    };

    // 본문 안에서 "제목 : 내용" 형태로 제목을 구분하는 표시.
    // 자동 메모(NoteManager.AddAutoEntry)가 항상 이 형태로 만들고, CSV에 손으로 적은
    // 증언 메모("화자 : ...")도 같은 형태를 쓰고 있어서 그대로 규칙으로 삼았다.
    private const string TitleSeparator = " : ";

    // 제목을 만들 길이 없을 때 본문 앞을 잘라 쓰는 길이.
    private const int AutoTitleMaxLength = 12;

    // ---------------------------------------------------------------------------------
    // 카테고리(탭) 판정
    // ---------------------------------------------------------------------------------
    // 위에서부터 보고 걸리는 즉시 확정한다.
    //   1) Category 칸이 채워져 있으면 그 값 (기획자가 직접 지정한 것이 항상 이긴다)
    //   2) TriggerType이 Initial이면 업무수첩 (게임 시작부터 적혀 있던 회사 메모)
    //   3) 화자 정보가 있으면 증언
    //   4) 아이템 정보가 있으면 증거
    //   5) 나머지는 전부 사건경과
    public static string CategoryOf(NoteManager.NoteEntry entry)
    {
        if (entry == null) return CategoryProgress;

        if (!string.IsNullOrWhiteSpace(entry.category)) return entry.category.Trim();
        if (IsType(entry, "Initial")) return CategoryWorkNote;
        if (HasSpeaker(entry)) return CategoryTestimony;
        if (HasItem(entry)) return CategoryEvidence;

        return CategoryProgress;
    }

    // "화자 정보가 있다"의 뜻이 메모 종류마다 다르다.
    //   자동 메모 : speaker 필드가 채워져 있는가 (InvestigationController가 넘겨준다)
    //   CSV 메모  : speaker 필드가 항상 비어 있으므로, Hotspot(오브젝트 조사)이면서
    //               본문이 "누가 : 무슨 말" 형태인지로 가른다.
    //               현재 Hotspot 4줄이 이 규칙으로 정확히 갈린다 -
    //               화자가 있는 2줄은 증언, 물건·현장을 본 2줄은 사건경과.
    private static bool HasSpeaker(NoteManager.NoteEntry entry)
    {
        if (IsType(entry, "Auto")) return !string.IsNullOrWhiteSpace(entry.speaker);

        return IsType(entry, "Hotspot")
            && !string.IsNullOrEmpty(entry.text)
            && entry.text.IndexOf(TitleSeparator, StringComparison.Ordinal) > 0;
    }

    // "아이템 정보가 있다"도 마찬가지로 종류마다 다르다.
    //   자동 메모 : itemId 필드가 채워져 있는가
    //   CSV 메모  : TriggerType이 Item인가 (이때 TriggerKey가 곧 ItemId다)
    private static bool HasItem(NoteManager.NoteEntry entry)
    {
        if (IsType(entry, "Auto")) return !string.IsNullOrWhiteSpace(entry.itemId);
        return IsType(entry, "Item");
    }

    private static bool IsType(NoteManager.NoteEntry entry, string triggerType)
    {
        return entry != null
            && string.Equals(entry.triggerType, triggerType, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------
    // 제목 / 본문 나누기
    // ---------------------------------------------------------------------------------
    // 목록에는 짧은 제목이, 오른쪽 페이지에는 본문이 필요하다. 그런데 메모에는 원래
    // 본문 한 덩어리밖에 없어서, 아래 순서로 제목을 만들어낸다.
    //   1) Title 칸이 채워져 있으면 그 값
    //   2) 본문에 " : "가 있으면 맨 처음 것 하나를 기준으로 앞토막
    //   3) 아이템에서 얻은 메모면 그 아이템 이름
    //   4) 본문 앞 12자
    public static string TitleOf(NoteManager.NoteEntry entry)
    {
        if (entry == null) return "";

        if (!string.IsNullOrWhiteSpace(entry.title)) return entry.title.Trim();

        string body = entry.text ?? "";
        int cut = body.IndexOf(TitleSeparator, StringComparison.Ordinal);
        if (cut > 0) return body.Substring(0, cut).Trim();

        if (IsType(entry, "Item") && !string.IsNullOrWhiteSpace(entry.triggerKey))
        {
            return ItemDatabase.GetDisplayName(entry.triggerKey.Trim());
        }

        body = body.Trim();
        return body.Length > AutoTitleMaxLength
            ? body.Substring(0, AutoTitleMaxLength) + "…"
            : body;
    }

    // 오른쪽 페이지에 펼칠 본문.
    // 제목을 본문 앞토막에서 뽑아 쓴 경우(위 규칙 2)에만 그 앞토막을 떼어낸다.
    // 그래야 제목과 본문에 같은 말이 두 번 나오지 않는다.
    //
    // 주의: 여기서 잘라낸 결과를 세이브에 넣으면 안 된다. 저장은 항상 원본(entry.text)
    // 그대로 하고, 자르는 것은 화면에 그릴 때만 한다. 잘린 본문을 저장했다가 다시
    // 불러오면 제목이 사라진다.
    public static string BodyOf(NoteManager.NoteEntry entry)
    {
        if (entry == null) return "";

        string body = entry.text ?? "";

        // Title 칸을 직접 쓴 경우엔 본문을 건드리지 않는다.
        if (!string.IsNullOrWhiteSpace(entry.title)) return body.Trim();

        int cut = body.IndexOf(TitleSeparator, StringComparison.Ordinal);
        if (cut > 0) return body.Substring(cut + TitleSeparator.Length).Trim();

        return body.Trim();
    }

    // ---------------------------------------------------------------------------------
    // 화면이 쓰는 묶음 만들기
    // ---------------------------------------------------------------------------------
    // 주어진 메모들 중 이 탭에 속하는 것만 골라, 들어온 순서를 유지한 채 돌려준다.
    // (NoteManager.GetRecordedEntriesSorted()가 이미 챕터 -> Order 순으로 정렬해 주므로
    //  여기서 다시 정렬하지 않는다.)
    public static List<NoteManager.NoteEntry> EntriesIn(
        List<NoteManager.NoteEntry> entries, string category)
    {
        var result = new List<NoteManager.NoteEntry>();
        if (entries == null) return result;

        foreach (var entry in entries)
        {
            if (CategoryOf(entry) == category) result.Add(entry);
        }

        return result;
    }
}
