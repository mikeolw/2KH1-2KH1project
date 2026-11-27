using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 핸드폰 속 내용 (통화기록 / 문자 / 연락처 / 이메일 / 인터넷 / 라디오)
// =====================================================================================
// ===== 파일 =====
// 전부 Assets/Resources/Dialogues/ 에 있다. 문자 내용·이메일 본문·라디오 대사가 전부
// 스토리라서, 공개 저장소에 올라가면 안 되기 때문이다(CSVReader.cs 윗부분 참고).
// 드라이브로 공유하고 git은 무시한다.
//
//   PhoneCallLog.csv   통화기록
//   PhoneMessages.csv  문자
//   PhoneContacts.csv  연락처
//   PhoneEmail.csv     이메일
//   PhoneInternet.csv  인터넷
//   PhoneRadio.csv     라디오
//
// ===== Chapter 칸 - 언제부터 보이나 =====
// 모든 표에 Chapter 칸이 있다(연락처만 없다 - 처음부터 끝까지 같다).
//   빈칸   : 처음부터 계속 보인다
//            (Chapter가 CSV의 첫 칸인데, CSVReader는 첫 칸이 빈 줄을 기본적으로 버린다.
//             그래서 아래에서 keepBlankFirstColumn: true로 읽는다 - 이걸 빼면 빈칸 줄이
//             조용히 사라져서 인터넷/라디오의 '대신 보여줄 줄'이 영영 안 나온다)
//   01     : 1장부터
//   02     : 2장부터
//   02b    : 2장b부터
//   03     : 3장부터
// 지금이 몇 장인지는 진행 중인 시나리오 파일 이름으로 안다
// (DialogueSystem.CurrentScenarioCsv - "Dialogues/scenario_02b" -> "02b").
//
// ===== 쌓이는 것과 갈리는 것 =====
//   쌓인다(누적) : 통화기록 · 문자 · 이메일
//                  장이 넘어가도 지난 기록이 남는다. 지금 장 이하가 전부 보인다.
//   갈린다(교체) : 인터넷 · 라디오
//                  장마다 내용이 통째로 바뀐다. 지금 장 것만 보인다.
//                  지금 장에 적힌 줄이 하나도 없으면 빈칸(=항상) 줄을 대신 보여준다.
//   그대로       : 연락처. Chapter 칸이 없다.
public static class PhoneData
{
    // 장의 순서. 앞에 있을수록 먼저다. 빈칸은 "처음부터"라 맨 앞.
    private static readonly string[] ChapterOrder = { "", "01", "02", "02b", "03" };

    // ---------------------------------------------------------------------------------
    // 한 줄씩
    // ---------------------------------------------------------------------------------
    public class Call
    {
        public string chapter, type, name, number, time;   // type: 수신 / 발신 / 부재중
    }

    public class Message
    {
        public string chapter, direction, name, number, time, text;   // direction: 받음 / 보냄
    }

    public class Contact
    {
        public string name, number, memo;
    }

    public class Mail
    {
        public string chapter, from, subject, time, body;
    }

    public class Site
    {
        public string chapter, title, headline, body;
    }

    public class RadioLine
    {
        public string chapter, frequency, program, speaker, text;
    }

    private static List<Call> calls;
    private static List<Message> messages;
    private static List<Contact> contacts;
    private static List<Mail> mails;
    private static List<Site> sites;
    private static List<RadioLine> radio;

    // ---------------------------------------------------------------------------------
    // 지금 몇 장인가
    // ---------------------------------------------------------------------------------
    public static string CurrentChapter()
    {
        var ds = DialogueSystem.Instance;
        string csv = ds != null ? ds.CurrentScenarioCsv : null;
        if (string.IsNullOrEmpty(csv)) return "";

        // "Dialogues/scenario_02b" -> "02b"
        int slash = csv.LastIndexOf('/');
        if (slash >= 0) csv = csv.Substring(slash + 1);
        const string prefix = "scenario_";
        if (!csv.StartsWith(prefix)) return "";
        string tail = csv.Substring(prefix.Length);

        // 엔딩 대본은 맨 마지막 장으로 친다(그때까지의 기록이 전부 쌓여 있어야 한다)
        if (tail.StartsWith("ending")) return ChapterOrder[ChapterOrder.Length - 1];
        return tail;
    }

    private static int OrderOf(string chapter)
    {
        chapter = (chapter ?? "").Trim();
        for (int i = 0; i < ChapterOrder.Length; i++)
            if (ChapterOrder[i] == chapter) return i;
        // 표에 적힌 장 이름이 위 목록에 없다. 맨 뒤로 보내고 한 번 알려준다.
        WarnOnce("chapter:" + chapter,
                 $"[PhoneData] Chapter '{chapter}'를 모릅니다. " +
                 "PhoneData.ChapterOrder에 추가하거나 표의 오타를 고쳐 주세요.");
        return ChapterOrder.Length;
    }

    private static readonly HashSet<string> warned = new HashSet<string>();
    private static void WarnOnce(string key, string message)
    {
        if (warned.Add(key)) Debug.LogWarning(message);
    }

    // ---------------------------------------------------------------------------------
    // 읽기
    // ---------------------------------------------------------------------------------
    private static string Get(Dictionary<string, object> row, string col) =>
        row.TryGetValue(col, out object v) && v != null ? v.ToString().Trim() : "";

    private static void EnsureLoaded()
    {
        if (calls != null) return;

        calls = new List<Call>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneCallLog", keepBlankFirstColumn: true))
            calls.Add(new Call
            {
                chapter = Get(r, "Chapter"), type = Get(r, "Type"), name = Get(r, "Name"),
                number = Get(r, "Number"), time = Get(r, "Time"),
            });

        messages = new List<Message>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneMessages", keepBlankFirstColumn: true))
            messages.Add(new Message
            {
                chapter = Get(r, "Chapter"), direction = Get(r, "Direction"), name = Get(r, "Name"),
                number = Get(r, "Number"), time = Get(r, "Time"), text = Get(r, "Text"),
            });

        contacts = new List<Contact>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneContacts"))
            contacts.Add(new Contact
            {
                name = Get(r, "Name"), number = Get(r, "Number"), memo = Get(r, "Memo"),
            });

        mails = new List<Mail>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneEmail", keepBlankFirstColumn: true))
            mails.Add(new Mail
            {
                chapter = Get(r, "Chapter"), from = Get(r, "From"), subject = Get(r, "Subject"),
                time = Get(r, "Time"), body = Get(r, "Body"),
            });

        sites = new List<Site>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneInternet", keepBlankFirstColumn: true))
            sites.Add(new Site
            {
                chapter = Get(r, "Chapter"), title = Get(r, "Title"),
                headline = Get(r, "Headline"), body = Get(r, "Body"),
            });

        radio = new List<RadioLine>();
        foreach (var r in CSVReader.Read("Dialogues/PhoneRadio", keepBlankFirstColumn: true))
            radio.Add(new RadioLine
            {
                chapter = Get(r, "Chapter"), frequency = Get(r, "Frequency"),
                program = Get(r, "Program"), speaker = Get(r, "Speaker"), text = Get(r, "Text"),
            });
    }

    // 표를 고치고 바로 확인하고 싶을 때 (에디터에서 Play를 다시 눌러도 된다)
    public static void Reload() { calls = null; EnsureLoaded(); }

    // ---------------------------------------------------------------------------------
    // 꺼내기
    // ---------------------------------------------------------------------------------
    // 쌓이는 것: 지금 장 이하가 전부. 새 것이 위로 오게 뒤집는다.
    private static List<T> Accumulated<T>(List<T> all, System.Func<T, string> chapterOf)
    {
        EnsureLoaded();
        int now = OrderOf(CurrentChapter());
        var result = new List<T>();
        for (int i = 0; i < all.Count; i++)
            if (OrderOf(chapterOf(all[i])) <= now) result.Add(all[i]);
        result.Reverse();
        return result;
    }

    // 갈리는 것: 지금 장 것만. 없으면 빈칸(항상) 줄로 대신한다.
    private static List<T> OfThisChapter<T>(List<T> all, System.Func<T, string> chapterOf)
    {
        EnsureLoaded();
        string now = CurrentChapter();
        var exact = new List<T>();
        var always = new List<T>();
        for (int i = 0; i < all.Count; i++)
        {
            string c = (chapterOf(all[i]) ?? "").Trim();
            if (c == now) exact.Add(all[i]);
            else if (c.Length == 0) always.Add(all[i]);
        }
        return exact.Count > 0 ? exact : always;
    }

    public static List<Call> Calls() => Accumulated(calls, c => c.chapter);
    public static List<Message> Messages() => Accumulated(messages, m => m.chapter);
    public static List<Mail> Mails() => Accumulated(mails, m => m.chapter);

    public static List<Contact> Contacts() { EnsureLoaded(); return contacts; }

    public static List<Site> Sites() => OfThisChapter(sites, s => s.chapter);
    public static List<RadioLine> Radio() => OfThisChapter(radio, r => r.chapter);

    // 라디오 화면에 띄울 주파수. 지금 장의 첫 줄에 적힌 값을 쓴다.
    public static string RadioFrequency()
    {
        var list = Radio();
        for (int i = 0; i < list.Count; i++)
            if (!string.IsNullOrEmpty(list[i].frequency)) return list[i].frequency;
        return "— —";
    }
}
