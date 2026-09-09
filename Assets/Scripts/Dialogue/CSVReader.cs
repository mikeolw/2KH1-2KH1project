using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class CSVReader
{
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
    static char[] TRIM_CHARS = { '\"' };

    public static List<Dictionary<string, object>> Read(string file)
    {
        var list = new List<Dictionary<string, object>>();
        TextAsset data = Resources.Load<TextAsset>(file);

        // 파일이 없으면 여기서 멈춘다. 예전에는 곧바로 data.text를 써서 이 경우
        // NullReferenceException으로 게임이 멈췄고, 원인이 "CSV 파일 이름 오타"라는 걸
        // 알아채기 어려웠다.
        if (data == null)
        {
            Debug.LogError($"[CSVReader] Resources/{file}.csv 를 찾지 못했습니다. " +
                           "파일 이름과 위치(Assets/Resources/ 아래)를 확인하세요.");
            return list;
        }

        string text = data.text;

        // ===== 엑셀이 붙이는 BOM 제거 (아주 중요) =====
        // 엑셀에서 CSV를 저장하면 파일 맨 앞에 눈에 보이지 않는 표식 문자(U+FEFF, 이른바 BOM)가
        // 붙는다. 이걸 안 떼면 첫 번째 컬럼 이름이 "LineType"이 아니라 "(보이지 않는 문자)LineType"이
        // 되어버려서, 그 열을 찾는 코드가 전부 실패한다. 결과적으로 "엑셀로 CSV를 열었다 저장했더니
        // 갑자기 대사가 안 나온다" 같은 원인 모를 고장이 생긴다.
        // 글자 하나 떼는 것으로 이 문제가 통째로 사라지므로 여기서 처리한다.
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        var lines = Regex.Split(text, LINE_SPLIT_RE);

        if (lines.Length <= 1) return list;

        var header = Regex.Split(lines[0], SPLIT_RE);

        // 컬럼 이름 앞뒤 공백도 없애준다. 엑셀에서 편집하다 보면 " Speaker"처럼
        // 공백이 섞여 들어가는 일이 있는데, 그러면 그 열을 못 찾는다.
        for (int j = 0; j < header.Length; j++)
        {
            header[j] = header[j].TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Trim();
        }
        for (var i = 1; i < lines.Length; i++)
        {
            var values = Regex.Split(lines[i], SPLIT_RE);
            if (values.Length == 0 || values[0] == "") continue;

            var entry = new Dictionary<string, object>();
            for (var j = 0; j < header.Length && j < values.Length; j++)
            {
                string value = values[j];
                value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\n", "\n");
                entry[header[j]] = value;
            }
            list.Add(entry);
        }
        return list;
    }
}