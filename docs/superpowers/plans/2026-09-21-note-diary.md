# 수첩 양면 다이어리 개편 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 수첩을 텍스트 덩어리 하나에서 성격별 탭이 달린 양면 다이어리로 바꾸고, 자동 생성 메모가 세이브에서 사라지는 버그를 고친다.

**Architecture:** 3층으로 나눈다. `NoteManager`는 메모를 담고 세이브를 처리하고, 새로 만드는 `NoteCatalog`는 "어느 탭에 넣을지·목록에 뭐라 띄울지"를 판정하는 순수 규칙이고, `NotePanelUI`는 그 결과를 양면 레이아웃으로 그린다. 분류 근거(화자·아이템)는 메모가 만들어질 때 `NoteEntry`에 실어둔다.

**Tech Stack:** Unity (C#), TextMeshPro, uGUI. UI는 아트 에셋 없이 코드로 도형을 그린다. 데이터는 `Resources/Dialogues/*.csv`.

**Spec:** `docs/superpowers/specs/2026-09-21-note-diary-design.md`

## Global Constraints

- **자동 테스트를 쓸 수 없다.** 이 프로젝트에는 Unity Test Framework 패키지도 `Assets/Tests`도 없다. 그래서 각 작업은 *테스트 코드* 대신 **에디터 컴파일 확인 + 명시된 수동 확인**으로 마무리한다. 임의로 테스트 프레임워크를 추가하지 않는다.
- 기준 해상도 **1440×1080 (4:3)** — 씬 `CanvasScaler`의 기준 해상도다. `ProjectSettings`의 기본 창 크기(1920×1080)가 아니라 이 값을 써야 한다. 그림이 전부 4:3이라 그렇게 맞춰져 있다(`AspectRatioKeeper.cs` 참고).
- `NoteEntries.csv`의 **기존 32줄을 수정하지 않는다.** `CSVReader`가 헤더와 값 중 짧은 쪽에 맞춰 읽으므로(`CSVReader.cs:89`) 헤더에만 칸을 추가하면 된다.
- 기존 세이브 파일이 계속 열려야 한다. `SaveData.noteEntryIds`는 **지우거나 이름을 바꾸지 않는다.**
- 건드리지 않는 것: 퀵바 버튼, `UIManager.ToggleNote()`, 씬의 `NotePanel`(여닫힘 스위치 역할), 토스트 알림(`OnNoteAdded`).
- 카테고리 문자열은 정확히 `사건경과` / `증거` / `증언` / `업무수첩` 네 개다.
- 에디터 메뉴는 기존 관례를 따라 `2KH1/` 아래에 만든다 (`Assets/Editor/IllustPlacementWindow.cs:157` 참고).
- 주석은 기존 파일들처럼 한국어로, "왜 이렇게 했는지"를 남긴다.

---

### Task 1: 분류 규칙(NoteCatalog)과 점검 도구

메모가 어느 탭에 속하고 목록에 뭐라고 뜰지 정하는 규칙을 만든다. 이 작업만 끝나도 에디터 메뉴로 "지금 CSV 32줄이 어떻게 갈리는지" 눈으로 확인할 수 있다.

**Files:**
- Modify: `Assets/Resources/Dialogues/NoteEntries.csv` (1번째 줄만)
- Modify: `Assets/Scripts/UI/NoteManager.cs` (`NoteEntry` 필드 추가, CSV 파싱을 static으로 분리)
- Create: `Assets/Scripts/UI/NoteCatalog.cs`
- Create: `Assets/Editor/NoteCategoryReport.cs`

**Interfaces:**
- Produces:
  - `NoteManager.NoteEntry`에 `category`, `title`, `speaker`, `itemId` (모두 `string`) 추가
  - `public static List<NoteManager.NoteEntry> NoteManager.ParseEntriesFromCsv()`
  - `NoteCatalog.Tabs` (`static readonly string[]`, 탭 표시 순서)
  - `NoteCatalog.CategoryOf(NoteManager.NoteEntry) → string`
  - `NoteCatalog.TitleOf(NoteManager.NoteEntry) → string`
  - `NoteCatalog.BodyOf(NoteManager.NoteEntry) → string`

- [ ] **Step 1: CSV 헤더에 칸 2개 추가**

`Assets/Resources/Dialogues/NoteEntries.csv`의 **1번째 줄만** 아래로 바꾼다. 2번째 줄부터 32줄은 그대로 둔다.

```
EntryId,TriggerType,TriggerKey,Chapter,Text,Order,Category,Title
```

파일 맨 앞의 BOM(`﻿`)은 그대로 유지한다. `CSVReader.cs:68`이 그것을 떼어내도록 되어 있다.

- [ ] **Step 2: `NoteEntry`에 필드 4개 추가**

`Assets/Scripts/UI/NoteManager.cs`의 `NoteEntry` 클래스(36~44번째 줄)를 아래로 바꾼다.

```csharp
    // 수첩 메모 한 줄의 정의(CSV에서 읽어온 것).
    public class NoteEntry
    {
        public string entryId;
        public string triggerType;   // Item / Investigate / Hotspot / Manual / Initial / Auto
        public string triggerKey;
        public string chapter;
        public string text;
        public int order;

        // ===== 아래 넷은 "어느 탭에 넣을지 / 목록에 뭐라고 띄울지"를 정하는 데 쓴다 =====
        // (판정 규칙 자체는 NoteCatalog.cs에 있다.)
        //   category : CSV의 Category 칸. 비어 있으면 NoteCatalog가 자동으로 정한다.
        //   title    : CSV의 Title 칸. 비어 있으면 NoteCatalog가 본문에서 뽑아낸다.
        //   speaker  : 이 메모가 "누구에게 들은 말"인지. 자동 메모(AddAutoEntry)만 채운다.
        //   itemId   : 이 메모가 "어떤 물건에서 나온 것"인지. 자동 메모만 채운다.
        // CSV로 적어둔 메모는 speaker/itemId가 항상 비어 있고, 대신 TriggerType으로 판정한다.
        public string category;
        public string title;
        public string speaker;
        public string itemId;
    }
```

- [ ] **Step 3: CSV 파싱을 static 메서드로 분리**

같은 파일의 `LoadEntries()`(106~135번째 줄)를 아래 두 메서드로 교체한다. 게임이 실행 중이 아닐 때도 파싱할 수 있어야 Step 5의 점검 도구가 동작한다.

```csharp
    private void LoadEntries()
    {
        allEntries = new Dictionary<string, NoteEntry>();

        foreach (var entry in ParseEntriesFromCsv())
        {
            allEntries[entry.entryId] = entry;
        }

        if (allEntries.Count == 0)
        {
            Debug.LogWarning(
                $"[NoteManager] {NoteCsv}.csv를 읽지 못했습니다. 조사기록(메모장)이 비어 있게 됩니다.");
        }
    }

    // NoteEntries.csv를 읽어 메모 정의 목록으로 돌려준다.
    // static으로 둔 이유: 에디터 점검 도구(Assets/Editor/NoteCategoryReport.cs)가 게임을
    // 실행하지 않은 상태에서도 "지금 CSV가 어떻게 분류되는지"를 확인할 수 있어야 하기 때문이다.
    public static List<NoteEntry> ParseEntriesFromCsv()
    {
        var result = new List<NoteEntry>();

        var rows = CSVReader.Read(NoteCsv);
        if (rows == null || rows.Count == 0) return result;

        foreach (var row in rows)
        {
            string id = GetFieldOf(row, "EntryId").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            int.TryParse(GetFieldOf(row, "Order").Trim(), out int order);

            result.Add(new NoteEntry
            {
                entryId = id,
                triggerType = GetFieldOf(row, "TriggerType").Trim(),
                triggerKey = GetFieldOf(row, "TriggerKey").Trim(),
                chapter = GetFieldOf(row, "Chapter").Trim(),
                text = GetFieldOf(row, "Text"),
                order = order,
                category = GetFieldOf(row, "Category").Trim(),
                title = GetFieldOf(row, "Title").Trim(),
                speaker = "",
                itemId = ""
            });
        }

        return result;
    }

    // CSVReader가 만든 행에서 값을 안전하게 꺼낸다. 칸 자체가 없으면(예전 CSV처럼) 빈 문자열.
    private static string GetFieldOf(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }
```

이어서 기존 인스턴스 메서드 `GetField`(137~140번째 줄)를 지우고, 이 파일 안에서 `GetField(...)`를 부르던 자리를 `GetFieldOf(...)`로 바꾼다. (현재 `GetField`를 쓰는 곳은 위에서 교체한 `LoadEntries()` 안뿐이므로, 지운 뒤 컴파일 오류가 없으면 정리가 끝난 것이다.)

- [ ] **Step 4: `NoteCatalog.cs` 작성**

`Assets/Scripts/UI/NoteCatalog.cs`를 새로 만든다.

```csharp
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
```

- [ ] **Step 5: 에디터 점검 도구 작성**

`Assets/Editor/NoteCategoryReport.cs`를 새로 만든다. 이 도구가 이 작업의 확인 수단이면서, 기획자가 CSV를 채울 때 "내가 쓴 메모가 어느 탭으로 갔는지" 확인하는 데도 쓰인다.

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 수첩 메모가 어느 탭으로 분류되는지 Console에 출력하는 점검 도구
// =====================================================================================
// 메뉴: 2KH1 > 수첩 분류 점검
//
// 게임을 실행하지 않았을 때 : NoteEntries.csv에 적힌 메모만 본다.
// 게임을 실행 중일 때       : 지금까지 실제로 수첩에 쌓인 메모를 본다(조사하며 자동으로
//                            만들어진 메모까지 포함).
//
// NoteEntries.csv에 Category/Title 칸을 채웠을 때 의도대로 갔는지 확인하는 용도다.
public static class NoteCategoryReport
{
    [MenuItem("2KH1/수첩 분류 점검")]
    public static void Report()
    {
        List<NoteManager.NoteEntry> entries;
        string source;

        if (Application.isPlaying && NoteManager.Instance != null)
        {
            entries = NoteManager.Instance.GetRecordedEntriesSorted();
            source = "실행 중인 게임에 실제로 쌓인 메모";
        }
        else
        {
            entries = NoteManager.ParseEntriesFromCsv();
            source = "NoteEntries.csv에 적힌 메모";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[수첩 분류 점검] 대상: {source} ({entries.Count}줄)");

        foreach (string tab in NoteCatalog.Tabs)
        {
            var inTab = NoteCatalog.EntriesIn(entries, tab);
            sb.AppendLine();
            sb.AppendLine($"───── {tab} ({inTab.Count}줄) ─────");

            foreach (var entry in inTab)
            {
                string body = NoteCatalog.BodyOf(entry);
                if (body.Length > 40) body = body.Substring(0, 40) + "…";
                sb.AppendLine($"  [{entry.chapter}] {NoteCatalog.TitleOf(entry)}  |  {body}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
```

- [ ] **Step 6: 컴파일 확인**

Unity 에디터로 전환해 자동 컴파일이 끝나기를 기다린다.
**기대 결과:** Console에 빨간 오류가 없다.

- [ ] **Step 7: 분류 결과 확인 (실행하지 않은 상태)**

메뉴 `2KH1 > 수첩 분류 점검`을 실행하고 Console 출력을 읽는다.

**기대 결과:** 아래와 정확히 일치해야 한다.

| 탭 | 줄 수 | 확인할 것 |
|---|---|---|
| 사건경과 | 7 | `Investigate` 5줄 + 화자가 없는 `Hotspot` 2줄 |
| 증거 | 16 | 전부 `Item` |
| 증언 | 2 | 제목이 화자 이름이고 본문에서 그 앞토막(`"화자 : "`)이 빠져 있다 |
| 업무수첩 | 7 | 전부 `Initial` |

합계 32줄. 어느 하나라도 어긋나면 `NoteCatalog.CategoryOf()`의 판정 순서를 다시 본다.

- [ ] **Step 8: 커밋**

**`NoteEntries.csv`는 커밋하지 않는다.** `Assets/Resources/`는 통째로 `.gitignore` 대상이다 —
이 저장소는 공개라서 시나리오·조사 대사가 올라가면 누구나 스토리를 미리 볼 수 있기 때문이고,
그림·소리·CSV는 구글 드라이브로 따로 주고받는다(`.gitignore` 66줄, CLAUDE.md 참고).
Step 1에서 고친 헤더는 **로컬에 남겨두고 드라이브로 팀원에게 전달한다.**
`git add -f`로 강제로 올리지 않는다.

```bash
git add Assets/Scripts/UI/NoteManager.cs Assets/Scripts/UI/NoteCatalog.cs Assets/Editor/NoteCategoryReport.cs
git commit -m "feat: 수첩 메모 분류/제목 규칙(NoteCatalog)과 점검 도구 추가

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

`.cs` 파일을 새로 만들면 Unity가 `.meta` 파일도 같이 만든다. `git status`에 `.meta`가 보이면 함께 커밋한다.

---

### Task 2: 자동 메모가 분류 근거를 싣게 하기

조사하다 즉석에서 만들어지는 메모(`auto_...`)가 "누구에게 들었는지 / 어떤 물건에서 나왔는지"를 함께 기록하게 한다. 이게 있어야 증언·증거 탭으로 갈 수 있다.

**Files:**
- Modify: `Assets/Scripts/UI/NoteManager.cs` (`AddAutoEntry`)
- Modify: `Assets/Scripts/Dialogue/InvestigationController.cs:1234` 부근

**Interfaces:**
- Consumes: Task 1의 `NoteEntry.speaker`, `NoteEntry.itemId`
- Produces: `NoteManager.AddAutoEntry(string investigationId, string hotspotKey, string objectName, string text, string speaker = "", string itemId = "")`

- [ ] **Step 1: `AddAutoEntry`가 화자·아이템을 받게 고치기**

`Assets/Scripts/UI/NoteManager.cs`의 `AddAutoEntry`(190~214번째 줄)를 아래로 바꾼다.

```csharp
    // 조사한 내용을 자동으로 수첩에 적는다.
    //   investigationId : 어느 조사 화면이었는지 (챕터 이름을 여기서 뽑아낸다)
    //   hotspotKey      : 어떤 오브젝트였는지 (같은 것을 두 번 적지 않기 위한 구분용)
    //   objectName      : 화면에 표시된 이름 (예: 메모장)
    //   text            : 조사했을 때 나온 문장
    //   speaker         : 사람에게 들은 말이면 그 사람 이름. 아니면 빈 문자열.
    //   itemId          : 이 조사로 물건을 얻었으면 그 ItemId. 아니면 빈 문자열.
    //
    // speaker/itemId는 수첩의 탭을 가르는 데만 쓴다(NoteCatalog.cs 참고). 본문에는 영향이 없다.
    public void AddAutoEntry(string investigationId, string hotspotKey, string objectName, string text,
                             string speaker = "", string itemId = "")
    {
        if (allEntries == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        // 같은 오브젝트를 여러 번 조사해도 한 번만 적히도록 고유한 id를 만든다.
        string entryId = $"auto_{investigationId}_{hotspotKey}";
        if (allEntries.ContainsKey(entryId)) return;   // 이미 적어둔 것

        string body = string.IsNullOrWhiteSpace(objectName)
            ? text.Trim()
            : $"{objectName.Trim()} : {text.Trim()}";

        allEntries[entryId] = new NoteEntry
        {
            entryId = entryId,
            triggerType = "Auto",
            triggerKey = "",
            chapter = ChapterLabelOf(investigationId),
            text = body,
            order = autoEntryOrder++,
            category = "",
            title = "",
            speaker = speaker ?? "",
            itemId = itemId ?? ""
        };

        AddEntry(entryId);
    }
```

- [ ] **Step 2: 호출부 고치기 — 여기에 함정이 있다**

`Assets/Scripts/Dialogue/InvestigationController.cs`의 1234번째 줄

```csharp
                NoteManager.Instance.AddAutoEntry(activeScreenId, obj.gameObject.name, noteName, noteBody);
```

을 아래로 바꾼다.

```csharp
                // ===== talkSpeaker를 그대로 넘기면 안 된다 =====
                // InvestigatableObject를 만들 때 talkSpeaker는 "비어 있으면 objectName으로
                // 대신 채우는" 식으로 세팅된다(이 파일 999번째 줄). 그래서 말을 거는 대상이
                // 아닌 물건도 talkSpeaker가 비어 있지 않다. 그대로 넘기면 말을 걸 수 없는 물건까지
                // 전부 "증언" 탭으로 가버린다. Talk 타입일 때만 화자로 인정한다.
                string noteSpeaker = obj.type == HotspotType.Talk ? obj.talkSpeaker : "";

                NoteManager.Instance.AddAutoEntry(
                    activeScreenId, obj.gameObject.name, noteName, noteBody,
                    noteSpeaker, obj.itemId);
```

- [ ] **Step 3: 컴파일 확인**

Unity 에디터로 전환해 컴파일이 끝나기를 기다린다.
**기대 결과:** Console에 빨간 오류가 없다.

- [ ] **Step 4: 자동 메모 분류 확인 (게임 실행)**

1. 게임을 실행해 사람에게 말을 걸 수 있는 조사 화면까지 간다.
2. 그 화면의 **인물(Talk)** 오브젝트에게 말을 건다.
3. 사람이 아닌 오브젝트도 두어 개 조사한다.
4. 에디터를 멈추지 말고 메뉴 `2KH1 > 수첩 분류 점검`을 실행한다.

**기대 결과:**
- 말을 건 두 사람이 **증언** 탭에 있다.
- 사람이 아닌 오브젝트는 **사건경과** 탭에 있다 (증언 탭에 섞여 있으면 Step 2의 함정에 걸린 것이다).
- 조사로 아이템을 얻었다면 그 메모는 **증거** 탭에 있다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/NoteManager.cs Assets/Scripts/Dialogue/InvestigationController.cs
git commit -m "feat: 자동 생성 메모에 화자/아이템 정보를 실어 수첩 탭 분류에 쓰도록 수정

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: 자동 메모가 세이브에서 사라지는 버그 수정

`auto_...` 메모는 CSV에 정의가 없고 런타임에만 만들어진다. 지금은 세이브에 EntryId만 저장돼서, 게임을 껐다 켜고 이어하기 하면 정의를 찾지 못해 조용히 사라진다. 정의 자체를 함께 저장한다.

**Files:**
- Modify: `Assets/Scripts/Save/SaveData.cs`
- Modify: `Assets/Scripts/UI/NoteManager.cs`
- Modify: `Assets/Scripts/Save/SavePointManager.cs`

**Interfaces:**
- Consumes: Task 1의 `NoteEntry` 필드, Task 2의 `speaker`/`itemId`
- Produces:
  - `[System.Serializable] class SavedAutoNote` (필드: `entryId`, `chapter`, `text`, `speaker`, `itemId`, `order`)
  - `SaveData.noteAutoEntries` (`List<SavedAutoNote>`)
  - `NoteManager.GetAutoEntryDefinitions() → List<SavedAutoNote>`
  - `NoteManager.RestoreAutoEntryDefinitions(List<SavedAutoNote>)`

- [ ] **Step 1: `SaveData`에 저장 칸 추가**

`Assets/Scripts/Save/SaveData.cs`에서 `noteEntryIds` 선언(45번째 줄) **바로 아래**에 넣는다. 기존 `noteEntryIds`는 지우지 않는다 — 예전 세이브 파일이 계속 열려야 하기 때문이다.

```csharp
    // ===== 자동으로 만들어진 메모(auto_...)의 정의 =====
    // 조사하다 즉석에서 만들어지는 메모는 NoteEntries.csv에 정의가 없고 실행 중 메모리에만
    // 있다. 그래서 EntryId만 저장하면, 게임을 껐다 켜고 이어하기 할 때 본문을 찾지 못해
    // 그 메모들이 통째로 사라졌다. 정의 자체를 같이 저장해 되살린다.
    //
    // JsonUtility는 Dictionary를 직렬화하지 못하므로 [Serializable] 클래스의 List로 담는다
    // (acquiredItemIds가 HashSet 대신 List를 쓰는 것과 같은 이유).
    public List<SavedAutoNote> noteAutoEntries = new List<SavedAutoNote>();
```

그리고 같은 파일에서 `SaveData` 클래스 **바깥**(파일 끝)에 아래 클래스를 추가한다.

```csharp
// 자동으로 만들어진 수첩 메모 한 줄의 정의. SaveData.noteAutoEntries에 담긴다.
// 필드 구성은 NoteManager.NoteEntry에서 "저장해야 되살릴 수 있는 것"만 추린 것이다.
// (category/title은 CSV에서만 오는 값이라 자동 메모에는 항상 비어 있으므로 저장하지 않는다.)
[System.Serializable]
public class SavedAutoNote
{
    public string entryId;
    public string chapter;
    public string text;
    public string speaker;
    public string itemId;
    public int order;
}
```

- [ ] **Step 2: `NoteManager`에 내보내기/되살리기 추가**

`Assets/Scripts/UI/NoteManager.cs`의 `GetRecordedEntryList()`(345번째 줄) **바로 아래**에 넣는다.

```csharp
    // 세이브용: 지금까지 적힌 메모 중 자동으로 만들어진 것들의 정의를 내보낸다.
    // 이것을 저장해두지 않으면 게임을 껐다 켰을 때 되살릴 수 없다(SaveData.cs 참고).
    public List<SavedAutoNote> GetAutoEntryDefinitions()
    {
        var result = new List<SavedAutoNote>();
        if (allEntries == null) return result;

        foreach (string id in recordedEntryIds)
        {
            if (!allEntries.TryGetValue(id, out var entry)) continue;
            if (!string.Equals(entry.triggerType, "Auto", System.StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new SavedAutoNote
            {
                entryId = entry.entryId,
                chapter = entry.chapter,
                text = entry.text,
                speaker = entry.speaker,
                itemId = entry.itemId,
                order = entry.order
            });
        }

        return result;
    }

    // 로드용: 세이브에 담아둔 자동 메모 정의를 다시 등록한다.
    // RestoreEntries()보다 반드시 먼저 불러야 한다. 그러지 않으면 EntryId만 복원되고
    // 정의가 없어서 GetRecordedEntriesSorted()가 그 메모들을 버린다.
    public void RestoreAutoEntryDefinitions(List<SavedAutoNote> saved)
    {
        if (allEntries == null || saved == null) return;

        foreach (var item in saved)
        {
            if (item == null || string.IsNullOrEmpty(item.entryId)) continue;

            allEntries[item.entryId] = new NoteEntry
            {
                entryId = item.entryId,
                triggerType = "Auto",
                triggerKey = "",
                chapter = item.chapter,
                text = item.text,
                order = item.order,
                category = "",
                title = "",
                speaker = item.speaker,
                itemId = item.itemId
            };

            // 이어서 만들어질 자동 메모가 되살린 것보다 뒤에 오도록 번호를 밀어둔다.
            if (item.order >= autoEntryOrder) autoEntryOrder = item.order + 1;
        }
    }
```

- [ ] **Step 3: 저장할 때 같이 담기**

`Assets/Scripts/Save/SavePointManager.cs`의 `noteEntryIds = ...` 부분(127~129번째 줄)에서, 그 항목 뒤에 쉼표를 붙이고 아래 줄을 추가한다.

```csharp
            noteEntryIds = NoteManager.Instance != null
                ? NoteManager.Instance.GetRecordedEntryList()
                : new System.Collections.Generic.List<string>(),
            noteAutoEntries = NoteManager.Instance != null
                ? NoteManager.Instance.GetAutoEntryDefinitions()
                : new System.Collections.Generic.List<SavedAutoNote>()
```

- [ ] **Step 4: 불러올 때 정의를 먼저 되살리기**

같은 파일의 복원 부분(168~171번째 줄)을 아래로 바꾼다. **순서가 중요하다.**

```csharp
        if (NoteManager.Instance != null && data.noteEntryIds != null)
        {
            // 자동 메모의 정의를 먼저 되살린 뒤에 목록을 복원해야 한다.
            // 순서가 바뀌면 EntryId만 복원되고 본문이 없어서 그 메모들이 사라진다.
            // (예전 세이브 파일에는 이 칸이 없어 null이 오는데, 그건 안에서 걸러낸다.)
            NoteManager.Instance.RestoreAutoEntryDefinitions(data.noteAutoEntries);
            NoteManager.Instance.RestoreEntries(data.noteEntryIds);
        }
```

- [ ] **Step 5: 컴파일 확인**

**기대 결과:** Console에 빨간 오류가 없다.

- [ ] **Step 6: 버그가 고쳐졌는지 확인 — 반드시 에디터를 멈췄다 다시 켠다**

1. 게임을 실행해 조사 화면에서 인물과 대화하고 오브젝트를 몇 개 조사한다.
2. 세이브포인트에서 슬롯에 저장한다.
3. **Unity 플레이 모드를 정지한다** (이 단계를 건너뛰면 메모리에 정의가 남아 있어서 버그가 재현되지 않는다).
4. 다시 실행해 타이틀에서 그 슬롯을 불러온다.
5. 수첩을 연다.

**기대 결과:** 조사하며 자동으로 적힌 메모들이 그대로 남아 있다. (수정 전에는 전부 사라지고 CSV에 적힌 메모만 남았다.)

- [ ] **Step 7: 예전 세이브 파일이 열리는지 확인**

이번 변경 **이전에** 만들어둔 세이브 슬롯을 불러온다. 없으면 `git stash`로 변경을 잠시 되돌려 슬롯을 하나 만든 뒤 되돌아온다.

**기대 결과:** 오류 없이 열리고, CSV에 적힌 메모는 정상으로 보인다. (그 세이브에는 자동 메모 정의가 없으므로 자동 메모가 안 보이는 것은 정상이다.)

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Save/SaveData.cs Assets/Scripts/Save/SavePointManager.cs Assets/Scripts/UI/NoteManager.cs
git commit -m "fix: 게임을 껐다 켜면 자동 생성 수첩 메모가 사라지던 문제 수정

자동 메모는 CSV에 정의가 없어 EntryId만 저장하면 복원할 수 없었다.
정의 자체를 세이브에 함께 담고, 불러올 때 목록보다 먼저 되살린다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: 수첩 화면을 양면 다이어리로 재작성

**Files:**
- Modify: `Assets/Scripts/UI/NotePanelUI.cs` (전체 재작성)

**Interfaces:**
- Consumes: `NoteCatalog.Tabs`, `NoteCatalog.EntriesIn`, `NoteCatalog.TitleOf`, `NoteCatalog.BodyOf`, `NoteManager.GetRecordedEntriesSorted()`

- [ ] **Step 1: `NotePanelUI.cs` 전체를 아래 내용으로 교체**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 재훈이 조사하면서 적어나가는 메모를 양면 다이어리로 보여준다
// =====================================================================================
// 퀵바의 Note 버튼을 누르면 열리는 탭이다.
//
// ===== 화면 구성 =====
//   왼쪽 바깥 : 성격별 세로 탭 4개 (사건경과 / 증거 / 증언 / 업무수첩)
//   왼쪽 페이지 : 그 탭에 속한 메모 목록. 챕터(예: #01)로 묶어서 보여준다.
//   오른쪽 페이지 : 목록에서 고른 메모의 제목과 본문.
//
// 어느 탭에 넣을지와 목록에 띄울 제목은 NoteCatalog가 정한다. 이 스크립트는 그리기만 한다.
//
// ===== 왜 UI를 캔버스에 직접 만드나 (중요) =====
// 처음에는 씬의 NotePanel 안에 메모 UI를 만들었다. 그런데 그 패널은 프로토타입 시절
// 크기와 위치가 제각각으로 잡혀 있고 안에 옛날 오브젝트도 남아 있어서, 코드에서 크기를
// 다시 잡아도 화면 구석에 작게 뜨거나 글자가 잘려 아무것도 안 보였다.
//
// 그래서 메모 화면을 씬의 패널 안이 아니라 캔버스 바로 아래에 따로 만든다. 씬이 어떻게
// 짜여 있든 영향을 받지 않으므로 항상 같은 자리에 같은 크기로 뜬다.
// 씬의 NotePanel은 "열렸는지 닫혔는지"를 알려주는 스위치 역할만 한다
// (UIManager가 그 패널을 켜고 끄므로, 이 스크립트는 그때 맞춰 메모 화면을 보여준다).
public class NotePanelUI : MonoBehaviour
{
    // 캔버스 아래에 만드는 메모 화면의 이름.
    private const string OverlayName = "__NoteOverlay";

    // ----- 색 (아트 에셋이 없어서 색 도형으로만 그린다) -----
    private static readonly Color PaperColor = new Color(0.95f, 0.92f, 0.84f, 1f);
    private static readonly Color InkColor = new Color(0.16f, 0.13f, 0.09f);
    private static readonly Color FadedInkColor = new Color(0.42f, 0.35f, 0.26f);
    private static readonly Color LineColor = new Color(0.55f, 0.45f, 0.32f, 0.7f);
    private static readonly Color SpineColor = new Color(0.28f, 0.21f, 0.13f, 1f);
    private static readonly Color TabIdleColor = new Color(0.72f, 0.66f, 0.55f, 1f);
    private static readonly Color TabActiveColor = PaperColor;
    private static readonly Color RowSelectedColor = new Color(0.82f, 0.74f, 0.58f, 1f);

    private GameObject overlay;

    private readonly List<Button> tabButtons = new List<Button>();
    private RectTransform listContent;      // 왼쪽 페이지에 줄을 쌓는 자리
    private ScrollRect listScroll;
    private ScrollRect detailScroll;
    private TMP_Text detailTitleText;
    private TMP_Text detailBodyText;

    // 지금 보고 있는 탭과 고른 메모.
    private string currentCategory = NoteCatalog.Tabs[0];
    private string selectedEntryId;

    // 목록에 만들어둔 줄 버튼들. 다시 그릴 때 지우려고 들고 있는다.
    private readonly List<GameObject> listRows = new List<GameObject>();

    private void Awake()
    {
        HideOriginalPanelVisuals();
        BuildOverlay();
    }

    private void OnEnable()
    {
        // 어떤 이유로든 NoteManager가 없으면 여기서 만들어 확보한다.
        if (NoteManager.Instance == null)
        {
            new GameObject("NoteManager").AddComponent<NoteManager>();
        }

        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
            NoteManager.Instance.OnNoteChanged += Refresh;
        }

        if (overlay == null) BuildOverlay();

        if (overlay != null)
        {
            overlay.SetActive(true);
            // 다른 UI에 가리지 않도록 항상 맨 앞으로.
            overlay.transform.SetAsLastSibling();
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
        }

        if (overlay != null) overlay.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 내용 그리기
    // ---------------------------------------------------------------------------------
    public void Refresh()
    {
        if (listContent == null) return;

        UpdateTabVisuals();
        RebuildList();
    }

    // 지금 탭에 속한 메모를 챕터별로 묶어 왼쪽 페이지에 쌓는다.
    private void RebuildList()
    {
        foreach (var row in listRows)
        {
            if (row == null) continue;

            // 부모에서 먼저 떼어낸 뒤에 지운다. Destroy()는 이번 프레임이 끝날 때 실제로
            // 지워지기 때문에, 그냥 지우면 아래에서 새로 만든 줄과 옛 줄이 한 프레임 동안
            // 같이 남아 VerticalLayoutGroup이 두 배 높이로 잡히며 목록이 덜컥거린다.
            row.transform.SetParent(null, false);
            Destroy(row);
        }
        listRows.Clear();

        var all = NoteManager.Instance != null
            ? NoteManager.Instance.GetRecordedEntriesSorted()
            : new List<NoteManager.NoteEntry>();
        var entries = NoteCatalog.EntriesIn(all, currentCategory);

        if (entries.Count == 0)
        {
            AddChapterHeading("아직 적어둔 것이 없다.");
            ShowDetail(null);
            return;
        }

        // 고른 메모가 이 탭에 없으면(탭을 막 바꿨을 때) 첫 줄을 대신 고른다.
        bool selectionInThisTab = false;
        foreach (var entry in entries)
        {
            if (entry.entryId == selectedEntryId) { selectionInThisTab = true; break; }
        }
        if (!selectionInThisTab) selectedEntryId = entries[0].entryId;

        string lastChapter = null;
        foreach (var entry in entries)
        {
            if (entry.chapter != lastChapter)
            {
                AddChapterHeading(entry.chapter);
                lastChapter = entry.chapter;
            }

            AddEntryRow(entry);
            if (entry.entryId == selectedEntryId) ShowDetail(entry);
        }

        // 줄을 새로 만들었으니 스크롤을 맨 위로 되돌린다.
        if (listScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            listScroll.verticalNormalizedPosition = 1f;
        }
    }

    // 목록 사이에 들어가는 챕터 소제목 (CSV의 Chapter 칸).
    private void AddChapterHeading(string chapter)
    {
        var go = new GameObject("Chapter", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = chapter;
        text.fontSize = 26;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.color = FadedInkColor;
        text.raycastTarget = false;
        UIFontHelper.Apply(text);

        listRows.Add(go);
    }

    // 목록의 한 줄. 누르면 오른쪽 페이지가 그 메모로 바뀐다.
    private void AddEntryRow(NoteManager.NoteEntry entry)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image),
                                typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 46f;

        var bg = go.GetComponent<Image>();
        bool selected = entry.entryId == selectedEntryId;
        bg.color = selected ? RowSelectedColor : new Color(1f, 1f, 1f, 0.01f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(24f, 0f);
        labelRt.offsetMax = new Vector2(-12f, 0f);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "· " + NoteCatalog.TitleOf(entry);
        label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Left;
        label.color = InkColor;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        UIFontHelper.Apply(label);

        string id = entry.entryId;
        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() =>
        {
            selectedEntryId = id;
            RebuildList();
        });

        listRows.Add(go);
    }

    // 오른쪽 페이지에 메모 하나를 펼친다. null이면 비운다.
    private void ShowDetail(NoteManager.NoteEntry entry)
    {
        if (detailTitleText == null || detailBodyText == null) return;

        detailTitleText.text = entry != null ? NoteCatalog.TitleOf(entry) : "";
        detailBodyText.text = entry != null ? NoteCatalog.BodyOf(entry) : "";

        UIFontHelper.Apply(detailTitleText);
        UIFontHelper.Apply(detailBodyText);

        if (detailScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count && i < NoteCatalog.Tabs.Length; i++)
        {
            var image = tabButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = NoteCatalog.Tabs[i] == currentCategory ? TabActiveColor : TabIdleColor;
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 씬에 있던 원래 패널은 안 보이게 한다
    // ---------------------------------------------------------------------------------
    // 메모 화면을 캔버스에 따로 만들기 때문에, 씬의 NotePanel 자체는 눈에 보이면 안 된다.
    // 다만 UIManager가 이 패널을 켜고 끄면서 여닫음을 관리하므로 오브젝트 자체는 남겨둔다.
    private void HideOriginalPanelVisuals()
    {
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------------------------
    // 메모 화면 만들기 (캔버스 바로 아래)
    // ---------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[NotePanelUI] 씬에 Canvas가 없어 수첩 화면을 만들 수 없습니다.");
            return;
        }

        // 이미 만들어져 있으면 지우고 새로 만든다. 예전 구조(글 덩어리 하나)가 남아 있으면
        // 자리만 차지하고 쓸 수 없기 때문이다.
        //
        // Destroy()가 아니라 DestroyImmediate()를 쓰는 이유: Destroy()는 이번 프레임이
        // 끝날 때 지워지므로, 바로 아래에서 같은 이름으로 새로 만들면 한 프레임 동안
        // __NoteOverlay가 두 개 존재하게 되고 다음번 Find()가 옛것을 집을 수 있다.
        // 여기는 Awake에서 한 번 도는 정리 코드라 즉시 지워도 안전하다.
        var existing = canvas.transform.Find(OverlayName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        tabButtons.Clear();
        listRows.Clear();

        // ----- 화면 전체를 덮는 막 -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 펼친 책 -----
        var book = new GameObject("Book", typeof(RectTransform), typeof(Image));
        book.transform.SetParent(overlay.transform, false);
        var bookRt = book.GetComponent<RectTransform>();
        bookRt.anchorMin = new Vector2(0.5f, 0.5f);
        bookRt.anchorMax = new Vector2(0.5f, 0.5f);
        bookRt.pivot = new Vector2(0.5f, 0.5f);
        bookRt.sizeDelta = new Vector2(1520f, 880f);
        bookRt.anchoredPosition = Vector2.zero;
        book.GetComponent<Image>().color = PaperColor;

        BuildTabs(book.transform);
        BuildSpine(book.transform);
        BuildLeftPage(book.transform);
        BuildRightPage(book.transform);
        BuildCloseButton(book.transform);

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨지므로,
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    // 책 왼쪽 바깥에 세로로 붙는 탭 4개.
    private void BuildTabs(Transform bookTransform)
    {
        const float tabWidth = 150f;
        const float tabHeight = 56f;
        const float gap = 8f;

        for (int i = 0; i < NoteCatalog.Tabs.Length; i++)
        {
            string category = NoteCatalog.Tabs[i];

            var go = new GameObject("Tab_" + category, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(bookTransform, false);
            // 책의 왼쪽 위 모서리를 기준으로 아래로 쌓는다.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 1f);   // 책 왼쪽 바깥으로 나가도록
            rt.sizeDelta = new Vector2(tabWidth, tabHeight);
            rt.anchoredPosition = new Vector2(0f, -60f - i * (tabHeight + gap));

            var bg = go.GetComponent<Image>();
            bg.color = TabIdleColor;
            bg.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Stretch(labelGo.GetComponent<RectTransform>());
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = category;
            label.fontSize = 22;
            label.alignment = TextAlignmentOptions.Center;
            label.color = InkColor;
            label.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                currentCategory = category;
                // 탭을 바꾸면 고른 메모를 비운다. RebuildList()가 그 탭의 첫 줄을 골라준다.
                selectedEntryId = null;
                Refresh();
            });

            tabButtons.Add(button);
        }
    }

    // 가운데 접힘선.
    private void BuildSpine(Transform bookTransform)
    {
        var go = new GameObject("Spine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(bookTransform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(20f, 0f);
        rt.offsetMin = new Vector2(rt.offsetMin.x, 40f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, -40f);

        var img = go.GetComponent<Image>();
        img.color = SpineColor;
        img.raycastTarget = false;
    }

    // 왼쪽 페이지: 세로로 줄을 쌓는 스크롤 목록.
    private void BuildLeftPage(Transform bookTransform)
    {
        var viewport = new GameObject("ListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(0.5f, 1f);
        viewportRt.offsetMin = new Vector2(40f, 90f);    // 아래는 닫기 버튼 자리
        viewportRt.offsetMax = new Vector2(-20f, -40f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);   // 스크롤 입력만 받는다

        var content = new GameObject("ListContent", typeof(RectTransform),
                                     typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0f, 100f);

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.spacing = 2f;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listScroll = viewport.AddComponent<ScrollRect>();
        listScroll.viewport = viewportRt;
        listScroll.content = listContent;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 40f;
    }

    // 오른쪽 페이지: 제목 + 구분선 + 본문(스크롤).
    private void BuildRightPage(Transform bookTransform)
    {
        var titleGo = new GameObject("DetailTitle", typeof(RectTransform));
        titleGo.transform.SetParent(bookTransform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(20f, 0f);
        titleRt.offsetMax = new Vector2(-40f, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, 56f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);

        detailTitleText = titleGo.AddComponent<TextMeshProUGUI>();
        detailTitleText.text = "";
        detailTitleText.fontSize = 32;
        detailTitleText.fontStyle = FontStyles.Bold;
        detailTitleText.alignment = TextAlignmentOptions.Left;
        detailTitleText.color = InkColor;
        detailTitleText.raycastTarget = false;

        var line = new GameObject("DetailDivider", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(bookTransform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.offsetMin = new Vector2(20f, 0f);
        lineRt.offsetMax = new Vector2(-40f, 0f);
        lineRt.sizeDelta = new Vector2(lineRt.sizeDelta.x, 2f);
        lineRt.anchoredPosition = new Vector2(0f, -100f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = LineColor;
        lineImg.raycastTarget = false;

        var viewport = new GameObject("DetailViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0.5f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.offsetMin = new Vector2(20f, 90f);
        viewportRt.offsetMax = new Vector2(-40f, -114f);   // 위는 제목과 구분선 자리
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        var content = new GameObject("DetailContent", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        // 높이를 미리 잡아둔다. 0으로 두면 글이 들어가도 잘려서 안 보인다.
        // 실제 높이는 ContentSizeFitter가 글 길이에 맞춰 다시 계산한다.
        contentRt.sizeDelta = new Vector2(0f, 400f);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        detailBodyText = content.AddComponent<TextMeshProUGUI>();
        detailBodyText.text = "";
        detailBodyText.fontSize = 24;
        detailBodyText.alignment = TextAlignmentOptions.TopLeft;
        detailBodyText.color = InkColor;
        detailBodyText.lineSpacing = 8f;
        detailBodyText.raycastTarget = false;
        detailBodyText.richText = true;
        detailBodyText.overflowMode = TextOverflowModes.Overflow;   // 길어지면 아래로 이어진다

        detailScroll = viewport.AddComponent<ScrollRect>();
        detailScroll.viewport = viewportRt;
        detailScroll.content = contentRt;
        detailScroll.horizontal = false;
        detailScroll.vertical = true;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 40f;
    }

    private void BuildCloseButton(Transform bookTransform)
    {
        var go = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(bookTransform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(200f, 50f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.35f, 0.28f, 0.18f, 0.85f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "닫기";
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.94f, 0.88f);
        label.raycastTarget = false;

        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        // 수첩을 닫는다 = 씬의 NotePanel을 끄는 것(UIManager가 그 상태로 여닫음을 판단한다)
        button.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

**기대 결과:** Console에 빨간 오류가 없다.

- [ ] **Step 3: 스펙 10장의 검증 절차를 전부 수행**

`docs/superpowers/specs/2026-09-21-note-diary-design.md` 10장의 7가지를 순서대로 확인한다.

1. 새 게임 → 수첩 열기 → **업무수첩** 탭에 7줄, 나머지 탭은 "아직 적어둔 것이 없다."
2. 첫 조사 화면 조사 → **사건경과** 탭에 현장 관찰, **증거** 탭에 아이템 메모
3. 인물과 대화 → **증언** 탭에 그 인물의 메모
4. 탭을 바꾸면 오른쪽 페이지가 그 탭의 첫 항목으로 바뀐다
5. 세이브 → **플레이 정지** → 재실행 → 이어하기 → 자동 메모가 남아 있다
6. 개편 전에 만든 세이브 파일을 불러와도 오류가 없다
7. 가장 긴(300자 이상) 증언 메모를 골라 오른쪽 페이지에서 끝까지 스크롤해 읽힌다

추가로 아래도 본다.
- 목록의 줄을 누르면 그 줄 배경이 밝아지고 오른쪽 페이지가 바뀐다
- 제목과 본문에 같은 말이 두 번 나오지 않는다 (예: 제목은 화자 이름, 본문은 `" : "` 뒤부터 시작)
- 수첩을 닫았다 다시 열어도 정상으로 뜬다

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/NotePanelUI.cs
git commit -m "feat: 수첩을 성격별 탭이 달린 양면 다이어리 화면으로 재작성

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## 알려진 한계 (이번 범위 밖)

- **#07 구간에서 저장하면 보류함(`deferredEntryIds`)의 메모가 사라진다.** 세이브에 담기는 것은 `recordedEntryIds`뿐이라 원래부터 그랬다. 이번 버그 수정은 "이미 수첩에 오른 자동 메모"만 살린다.
- 목록 페이지 넘김(`< 1 2 3 >`), 오른쪽 페이지 사진, "Review" 버튼, 조사 완료 체크 표시, 가방·사진 앨범 통합은 스펙 11장대로 하지 않는다.
