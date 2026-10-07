using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 세이브포인트에 도착했을 때 뜨는 "어느 슬롯에 저장할까요?" 창
// =====================================================================================
// 시나리오 문서의 {세이브포인트}를 지날 때마다 이 창이 떠서, 플레이어가 슬롯을 골라
// 저장하거나 그냥 넘어갈 수 있게 한다.
//
// ===== 왜 슬롯을 고르게 하나 =====
// 세이브포인트가 9곳이라 슬롯도 9개다. 자동으로 한 곳에 덮어써 버리면 앞 지점으로
// 되돌아갈 수가 없다. 어느 칸에 남길지 플레이어가 정하면, 나중에 다른 선택지를 보려고
// 특정 지점부터 다시 시작하기 쉬워진다.
//
// ===== 씬 배치 =====
// 씬에 미리 만들어둘 것이 없다. GameBootstrap이 게임 씬에 자동으로 만들고,
// SavePointManager가 세이브포인트를 지날 때 알아서 띄운다.
public class SaveSlotDialog : MonoBehaviour
{
    public static SaveSlotDialog Instance;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    // 창 전체. 이걸 켜고 끄는 것으로 여닫는다.
    private GameObject panel;
    private TMP_Text titleLabel;          // 머리말 아래 안내 문구 ("[저장 지점] 저장할 슬롯을 고르세요")
    private SaveSlotRow[] slotRows;

    // 다른 스크립트가 "지금 저장 창이 열려 있나?"를 확인할 때 쓴다.
    // 열려 있는 동안에는 대사가 클릭으로 넘어가면 안 된다.
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildUI();
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 열기 / 닫기
    // ---------------------------------------------------------------------------------

    // SavePointManager가 세이브포인트에 도착했을 때 호출한다.
    public void Open(string savePointName)
    {
        if (panel == null) return;

        if (titleLabel != null)
        {
            titleLabel.text = string.IsNullOrEmpty(savePointName)
                ? "저장할 슬롯을 고르세요"
                : $"[{savePointName}] 저장할 슬롯을 고르세요";
        }

        RefreshSlots();
        panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);

        // ===== 닫히면 대사를 이어서 진행한다 =====
        // 이 창은 항상 세이브포인트 도달의 부작용으로만 뜬다(SavePointManager.ReachSavePoint()
        // 가 여는 유일한 곳이다). 그래서 창이 열리는 시점엔 이미 DialogueSystem이 다음 줄
        // (lineIndex)까지 넘겨놓은 상태이고, 플레이어가 창을 닫으면 곧바로 그 다음 줄로
        // 이어가면 된다. 예전엔 이걸 안 해줘서, 플레이어가 창을 닫은 뒤 대사창을 따로 한 번
        // 더 클릭해야 다음 줄(미니게임/조사 등)로 넘어갔다 - 그 클릭이 암전(ShowLineWithFade)
        // 중이면 isFading에 막혀 화면이 멈춘 것처럼 보이는 문제까지 겹쳐 있었다.
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowNextSentence();
        }
    }

    // 슬롯마다 "무엇이 저장되어 있는지"를 다시 읽어 표시한다.
    // 이 창은 세이브포인트에 도착했을 때만 뜨므로 모든 칸에 저장할 수 있다(빈 칸 포함, 덮어쓰기).
    private void RefreshSlots()
    {
        if (SaveManager.Instance == null || slotRows == null) return;

        for (int i = 0; i < slotRows.Length; i++)
        {
            slotRows[i].Set(SaveManager.Instance.Load(i), isSaveMode: true, locked: false, clickable: true);
        }
    }

    // 슬롯을 골랐을 때: 그 칸에 저장하고 창을 닫는다.
    private void OnClickSlot(int slotIndex)
    {
        if (SavePointManager.Instance == null) return;

        bool ok = SavePointManager.Instance.SaveToSlot(slotIndex);
        if (!ok)
        {
            if (titleLabel != null) titleLabel.text = "저장할 수 없습니다.";
            return;
        }

        // 방금 저장된 내용을 잠깐 보여주고 닫는다.
        RefreshSlots();
        Close();
    }

    // ---------------------------------------------------------------------------------
    // UI 만들기 (Figma "Screen / Save - 세이브포인트")
    // ---------------------------------------------------------------------------------
    //   SAVE / 기록 남기기
    //   [저장 지점] 저장할 슬롯을 고르세요
    //   [01 │ #03 경찰서 ...]  x4  (SaveSlotRow.cs)
    //   저장되는 것은 ... 저장 지점입니다.            [저장하지 않고 계속]
    private void BuildUI()
    {
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[SaveSlotDialog] 씬에 Canvas가 없어 저장 창을 만들 수 없습니다.");
            return;
        }

        panel = SaveScreenUI.BuildFrame(targetCanvas.transform, "SaveSlotDialog", "SAVE", "기록 남기기",
                                        out RectTransform box, out titleLabel);
        // 다른 UI보다 항상 위에 뜨도록 계층 맨 끝으로.
        panel.transform.SetAsLastSibling();

        int slotCount = SaveManager.SlotCount;
        slotRows = new SaveSlotRow[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            int index = i;   // 람다가 반복 변수를 그대로 잡지 않도록 복사
            slotRows[i] = SaveSlotRow.Create(box, i, () => OnClickSlot(index));
        }

        var note = SaveScreenUI.CreateText(box, "Note", "저장되는 것은 지금 이 순간이 아니라 방금 지나온 저장 지점입니다.",
                                           15, FontStyles.Normal, SaveScreenUI.HintColor);
        SaveScreenUI.PlaceTopLeft(note.rectTransform, SaveScreenUI.Pad, SaveScreenUI.FooterY + 16f, 560f, 24f);

        SaveScreenUI.CreateButton(box, "Btn_Skip", "저장하지 않고 계속",
                                  SaveScreenUI.BoxWidth - SaveScreenUI.Pad - 240f, 240f, Close);

        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨지므로, 화면에서 한글이 잘 나오는
        // 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }
}
