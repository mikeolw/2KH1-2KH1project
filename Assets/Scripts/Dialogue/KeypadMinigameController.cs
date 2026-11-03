using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 번호 입력 미니게임 (사장실 금고 열기에서 사용)
// =====================================================================================
// ===== 어떤 게임인가 =====
// 화면 가운데에 금고 키패드가 뜬다. 플레이어가 숫자 버튼을 눌러 비밀번호를 채우고
// [열기]를 누르면 판정한다.
//   - 맞으면  -> 초록색으로 깜빡인 뒤 패널이 닫히고 다음 줄로 진행 (onSuccess)
//   - 틀리면  -> 빨간색으로 깜빡이고 입력이 지워진다. 횟수 제한도 벌칙도 없다.
// 일부러 실패 경로(엔딩 직행)를 넣지 않았다. 이 금고는 "머리를 써서 알아내는 맛"을 주려는
// 장치이지 플레이어를 떨어뜨리려는 관문이 아니다. 틀려도 계속 두드려볼 수 있어야
// 비전공자 플레이어도 끝까지 가서 "풀었다"는 기분을 받는다.
//
// ===== 정답을 CSV에 적는 법 (MinigameAnswer 칸) =====
//   1) 그냥 숫자를 적는다            : 133837
//   2) "원본+이동값" 으로 적는다      : 800504+3
//      -> 각 자리에 시저 암호(자리마다 같은 수만큼 밀기)를 적용한 값이 정답이 된다.
//         8+3=11 -> 1 (10으로 나눈 나머지), 0+3=3, 0+3=3, 5+3=8, 0+3=3, 4+3=7
//         즉 800504+3 의 정답은 133837 이다. 미는 양을 바꾸려면 +뒤의 숫자만 고치면 된다.
//         (빼는 쪽으로 밀고 싶으면 800504+7 처럼 10에서 뺀 값을 적으면 같은 결과가 된다)
//      2번을 권한다. CSV만 봐도 "무슨 날짜를 몇 칸 민 것"인지 바로 읽히고, 숫자를 손으로
//      다시 계산하다 틀릴 일도 없다.
//
// ===== 안내문과 힌트 =====
//   MinigameLabel : 키패드 위에 굵게 뜨는 한 줄 (예: 금고 비밀번호를 입력하라)
//   MinigameHint  : 그 아래 작게 뜨는 힌트 (예: 아버지가 처음 그 능력을 쓴 날. 세 칸씩 밀어 두었다.)
// 힌트 칸을 비우면 힌트 줄 자체가 안 나온다.
//
// ===== 씬 배치 =====
// TimingClickMinigameController와 같은 방식이다. 인스펙터에 아무것도 연결하지 않아도 되고,
// 씬에 없으면 GameBootstrap이 만들어준다. UI는 그림 파일 없이 코드로 만든다
// (키패드는 서사가 담긴 CG가 아니라 조작용 부품이라 매번 같은 모양이면 된다).
public class KeypadMinigameController : MonoBehaviour
{
    public static KeypadMinigameController Instance;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("색상 (청회색 테마 - MinigameTheme)")]
    public Color displayColor = MinigameTheme.Well;
    public Color textColor = MinigameTheme.TextBright;
    public Color okColor = MinigameTheme.Ok;
    public Color failColor = MinigameTheme.Danger;

    [Header("연출 시간(초)")]
    [Tooltip("정답을 맞혔을 때 초록색을 보여주고 패널을 닫기까지의 시간.")]
    public float successHoldSeconds = 0.9f;
    [Tooltip("틀렸을 때 빨간색을 보여주고 입력을 지우기까지의 시간.")]
    public float failHoldSeconds = 0.7f;

    public bool IsActive => root != null && root.activeSelf;

    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text hintText;
    private TMP_Text displayText;
    private TMP_Text messageText;
    private Image displayBg;

    private string answer = "";
    private readonly StringBuilder input = new StringBuilder();
    private bool resolved;
    private bool locked;          // 판정 연출 중에는 입력을 받지 않는다
    private int wrongCount;
    private Action onSuccess;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ---------------------------------------------------------------------------------
    // 시작
    // ---------------------------------------------------------------------------------
    // DialogueSystem이 LineType=Minigame + MinigameAnswer가 채워진 줄을 만났을 때 호출한다.
    public void StartGame(string label, string answerSpec, string hint, Action onSuccessCallback)
    {
        string parsed = ResolveAnswer(answerSpec);
        if (string.IsNullOrEmpty(parsed))
        {
            // 정답을 못 읽으면 금고 앞에서 게임이 멈춰버린다. 막아 세우는 것보다
            // 그냥 통과시키는 쪽이 안전하다 (다른 미니게임들과 같은 방침).
            Debug.LogError($"[KeypadMinigameController] 정답 '{answerSpec}'을 읽지 못해 미니게임을 건너뜁니다. " +
                           "MinigameAnswer 칸에는 숫자(예: 133837) 또는 \"원본+이동값\"(예: 800504+3)을 적으세요.");
            onSuccessCallback?.Invoke();
            return;
        }

        if (!EnsureUI())
        {
            Debug.LogError("[KeypadMinigameController] UI를 만들지 못해 미니게임을 건너뜁니다. 곧바로 성공 처리합니다.");
            onSuccessCallback?.Invoke();
            return;
        }

        answer = parsed;
        onSuccess = onSuccessCallback;
        resolved = false;
        locked = false;
        wrongCount = 0;
        input.Clear();

        titleText.text = string.IsNullOrWhiteSpace(label) ? "비밀번호를 입력하라" : label;
        hintText.text = hint ?? "";
        hintText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
        messageText.text = "";
        displayBg.color = displayColor;

        root.SetActive(true);
        RedrawDisplay();
    }

    // "800504+3" 처럼 적혀 있으면 시저 암호를 적용한 값을 돌려주고,
    // "133837" 처럼 숫자만 적혀 있으면 그대로 돌려준다. 둘 다 아니면 빈 문자열.
    private static string ResolveAnswer(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return "";
        spec = spec.Trim();

        int plus = spec.IndexOf('+');
        if (plus < 0) return IsAllDigits(spec) ? spec : "";

        string source = spec.Substring(0, plus).Trim();
        string shiftStr = spec.Substring(plus + 1).Trim();
        if (!IsAllDigits(source) || !int.TryParse(shiftStr, out int shift)) return "";

        var sb = new StringBuilder(source.Length);
        foreach (char c in source)
        {
            // 자리마다 같은 수만큼 민다. 10을 넘으면 한 바퀴 돌아 0부터 다시 센다.
            int shifted = ((c - '0' + shift) % 10 + 10) % 10;
            sb.Append((char)('0' + shifted));
        }
        return sb.ToString();
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (c < '0' || c > '9') return false;
        return true;
    }

    // ---------------------------------------------------------------------------------
    // 입력
    // ---------------------------------------------------------------------------------
    private void Update()
    {
        if (!IsActive || locked || resolved) return;

        // 키보드로도 칠 수 있게 한다. 숫자 패드를 마우스로만 누르게 하면
        // "번호를 입력한다"는 행동치고 손이 많이 간다.
        for (int n = 0; n <= 9; n++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + n) || Input.GetKeyDown(KeyCode.Keypad0 + n))
            {
                PressDigit(n);
            }
        }
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) PressBack();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PressConfirm();
    }

    private void PressDigit(int n)
    {
        if (locked || resolved) return;
        if (input.Length >= answer.Length) return;   // 자릿수를 넘겨 입력하지 않는다
        input.Append((char)('0' + n));
        messageText.text = "";
        RedrawDisplay();

        // 자릿수를 다 채우면 알아서 판정한다. [열기]를 따로 누르게 해도 되지만,
        // 다 채운 뒤 한 번 더 누르게 하는 건 번거롭기만 하다.
        if (input.Length == answer.Length) Judge();
    }

    private void PressBack()
    {
        if (locked || resolved) return;
        if (input.Length == 0) return;
        input.Length -= 1;
        messageText.text = "";
        RedrawDisplay();
    }

    private void PressConfirm()
    {
        if (locked || resolved) return;
        if (input.Length < answer.Length)
        {
            messageText.color = textColor;
            messageText.text = $"{answer.Length}자리를 다 눌러야 한다.";
            return;
        }
        Judge();
    }

    // 빈 자리는 밑줄로 보여준다. 예) 133___
    private void RedrawDisplay()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < answer.Length; i++)
        {
            sb.Append(i < input.Length ? input[i] : '_');
            if (i < answer.Length - 1) sb.Append(' ');
        }
        displayText.text = sb.ToString();
    }

    // ---------------------------------------------------------------------------------
    // 판정
    // ---------------------------------------------------------------------------------
    private void Judge()
    {
        if (locked || resolved) return;

        if (input.ToString() == answer)
        {
            locked = true;
            displayBg.color = okColor;
            displayText.color = Color.black;
            messageText.color = okColor;
            messageText.text = "맞았다. 금고가 열린다.";
            Invoke(nameof(Success), successHoldSeconds);
            return;
        }

        wrongCount++;
        locked = true;
        displayBg.color = failColor;
        messageText.color = failColor;
        messageText.text = wrongCount >= 3
            ? "틀렸다. ...숫자를 그대로 쓰진 않으셨을 거다."   // 몇 번 틀리면 한 번 더 밀어준다
            : "틀렸다.";
        Invoke(nameof(ClearAfterFail), failHoldSeconds);
    }

    private void ClearAfterFail()
    {
        if (resolved) return;
        input.Clear();
        displayBg.color = displayColor;
        displayText.color = textColor;
        locked = false;
        RedrawDisplay();
    }

    private void Success()
    {
        if (resolved) return;
        resolved = true;
        locked = false;
        displayText.color = textColor;
        root.SetActive(false);

        var callback = onSuccess;
        onSuccess = null;
        callback?.Invoke();
    }

    // ===== 엔딩이 미니게임 도중에 끼어들 때 =====
    // TimingClickMinigameController.ForceExit()과 같은 이유/방식.
    // onSuccess를 부르지 않고 패널만 치운다.
    public void ForceExit()
    {
        if (resolved) return;
        resolved = true;
        locked = false;
        CancelInvoke();
        if (root != null) root.SetActive(false);
        onSuccess = null;
    }

    // ---------------------------------------------------------------------------------
    // UI 생성 (코드로 직접 - TimingClickMinigameController.EnsureUI()와 같은 방식)
    // ---------------------------------------------------------------------------------
    private bool EnsureUI()
    {
        if (root != null) return true;

        if (targetCanvas == null) targetCanvas = UILayers.RootCanvas();
        if (targetCanvas == null)
        {
            Debug.LogError("[KeypadMinigameController] 씬에 Canvas가 없어 UI를 만들 수 없습니다.");
            return false;
        }

        // 화면 전체를 덮는 어두운 막. 뒤쪽 조사 화면이 눌리지 않게 막는 역할도 한다.
        root = new GameObject("KeypadMinigame", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(targetCanvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        ModalStack.MarkNotWindow(root, "금고 키패드 미니게임. 게임 단계라 창이 아니다 (대사 막기는 IsActive).");

        // 키패드 본체 (미니게임 공통 카드: 어두운 면 + 테두리 + 위쪽 강조선)
        var panel = NewRect("Panel", root.transform, new Vector2(420f, 660f), Vector2.zero);
        MinigameTheme.StyleCard(panel);

        // 머리글
        var header = NewText("Header", panel, 16, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(380f, 24f), new Vector2(0f, 296f));
        header.color = MinigameTheme.Accent;
        header.characterSpacing = 24f;
        header.text = "PASSWORD";

        // 안내문
        titleText = NewText("Title", panel, 26, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(380f, 40f), new Vector2(0f, 256f));

        // 힌트
        hintText = NewText("Hint", panel, 17, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(380f, 60f), new Vector2(0f, 204f));
        hintText.color = MinigameTheme.TextDim;
        hintText.textWrappingMode = TextWrappingModes.Normal;

        // 입력칸 (움푹한 면 + 테두리)
        var display = NewRect("Display", panel, new Vector2(340f, 72f), new Vector2(0f, 128f));
        displayBg = display.gameObject.AddComponent<Image>();
        displayBg.color = displayColor;
        MinigameTheme.AddBorder(display, MinigameTheme.Line);
        displayText = NewText("Value", display, 40, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(320f, 60f), Vector2.zero);

        // 판정 문구
        messageText = NewText("Message", panel, 18, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(380f, 30f), new Vector2(0f, 72f));

        // 숫자 버튼 4줄: 1~9 / 지우기 0 열기
        // 가운데 정렬을 위해 격자 좌표를 직접 계산한다(LayoutGroup을 쓰면 버튼 크기를
        // 따로 맞춰줘야 해서 오히려 손이 간다).
        const float keyW = 100f, keyH = 68f, gapX = 10f, gapY = 10f;
        float originY = 10f;   // 첫 줄(1 2 3)의 세로 위치
        for (int i = 0; i < 9; i++)
        {
            int rowIndex = i / 3, colIndex = i % 3;
            int digit = i + 1;
            MakeKey(panel, digit.ToString(),
                new Vector2((colIndex - 1) * (keyW + gapX), originY - rowIndex * (keyH + gapY)),
                new Vector2(keyW, keyH), () => PressDigit(digit));
        }
        float lastRowY = originY - 3f * (keyH + gapY);
        MakeKey(panel, "지우기", new Vector2(-(keyW + gapX), lastRowY), new Vector2(keyW, keyH), PressBack);
        MakeKey(panel, "0", new Vector2(0f, lastRowY), new Vector2(keyW, keyH), () => PressDigit(0));
        MakeKey(panel, "열기", new Vector2(keyW + gapX, lastRowY), new Vector2(keyW, keyH), PressConfirm);

        root.SetActive(false);
        return true;
    }

    private RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private TMP_Text NewText(string name, Transform parent, float fontSize, FontStyles style,
                             TextAlignmentOptions align, Vector2 size, Vector2 pos)
    {
        var rt = NewRect(name, parent, size, pos);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.color = textColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UIFontHelper.Apply(text);
        return text;
    }

    // 선택지/미션 카드와 같은 모양의 버튼 (마우스 올림 때 테두리가 밝아진다).
    private void MakeKey(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
    {
        var button = MinigameTheme.MakeButton(parent, "Key_" + label, label, size, pos, onClick,
            label.Length > 1 ? 20f : 28f);
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.fontStyle = FontStyles.Bold;
    }
}
