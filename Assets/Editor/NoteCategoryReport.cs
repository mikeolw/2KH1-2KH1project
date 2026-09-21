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

        var seenIds = new HashSet<string>();

        foreach (string tab in NoteCatalog.Tabs)
        {
            var inTab = NoteCatalog.EntriesIn(entries, tab);
            sb.AppendLine();
            sb.AppendLine($"───── {tab} ({inTab.Count}줄) ─────");

            foreach (var entry in inTab)
            {
                seenIds.Add(entry.entryId);
                string body = NoteCatalog.BodyOf(entry);
                if (body.Length > 40) body = body.Substring(0, 40) + "…";
                sb.AppendLine($"  [{entry.chapter}] {NoteCatalog.TitleOf(entry)}  |  {body}");
            }
        }

        // 네 탭 어디에도 안 들어간 줄. NoteCatalog.CategoryOf()가 잘못된 Category 값을
        // 사건경과로 떨어뜨려 처리하므로 정상적으로는 항상 0건이어야 한다. 그래도 여기서
        // 따로 세는 이유는, 총계와 탭별 합이 어긋나는 경우(EntriesIn/CategoryOf 로직이
        // 나중에 바뀌어 서로 어긋나는 등)를 사람이 눈으로 하나하나 비교하지 않아도
        // 도구가 대신 잡아주기 위해서다.
        var unclassified = new List<NoteManager.NoteEntry>();
        foreach (var entry in entries)
        {
            if (!seenIds.Contains(entry.entryId)) unclassified.Add(entry);
        }

        sb.AppendLine();
        sb.AppendLine($"───── 어느 탭에도 안 들어간 줄 ({unclassified.Count}줄) ─────");
        if (unclassified.Count == 0)
        {
            sb.AppendLine("  없음");
        }
        else
        {
            foreach (var entry in unclassified)
            {
                sb.AppendLine($"  [{entry.entryId}] Category='{entry.category}'  |  {entry.text}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
