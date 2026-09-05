# 2KH1 프로젝트 - 작업 규칙과 구조

> 이 파일은 Claude Code가 자동으로 읽습니다. 사람이 읽어도 되도록 썼습니다.
> 팀원용 변경 내역 정리는 [`팀_인수인계.md`](팀_인수인계.md), CSV 작성법은 [`CSV_가이드.md`](CSV_가이드.md) 참고.

## 이 게임이 무엇인가

Unity 6(`6000.5.7f1`) + URP 2D로 만드는 **한국어 텍스트 어드벤처/추리물**.
회사 비리(산재 은폐)를 파헤치는 이야기이고, 트루 1 + 배드 5 = 총 6개 엔딩으로 갈라진다.
주인공은 **차재훈**, 죽은 친구는 **유한성**, 진범은 **차장**, 사장은 **재훈의 아버지**.

화면 기준 해상도는 **1440x1080 (4:3)** 이다. 모든 그림이 이 크기 기준으로 그려져 있다.

---

## 반드시 지켜야 할 것

### 1. 그림·소리·CSV는 절대 인스펙터로 연결하지 않는다

`Assets/Resources/` 폴더 전체가 `.gitignore` 대상이다(구글 드라이브로 따로 공유).
인스펙터로 연결하면 유니티가 `.meta` 파일의 GUID로 기억하는데, 그 `.meta`가 git으로
공유되지 않아 **팀원마다 GUID가 달라져 참조가 깨진다.**

그래서 전부 **"파일 이름 문자열"로 불러온다.**

```csharp
IllustLoader.LoadBackground("BG_01_Office")     // 배경
IllustLoader.LoadObject("OBJ_01_Notepad")       // 조사 오브젝트
IllustLoader.LoadStanding("STD_Past01_Hansung_Default")  // 캐릭터
Resources.Load<AudioClip>("Sounds/" + name)     // BGM
```

### 2. 코드로 만든 글자에는 반드시 글꼴을 물려준다

`AddComponent<TextMeshProUGUI>()`로 만든 글자는 TMP 기본값인 **LiberationSans**가 붙는데,
여기엔 **한글 글자 모양이 아예 없다.** 그대로 두면 한글이 안 보이거나 검은 네모가 된다.

```csharp
UIFontHelper.ApplyToChildren(만든오브젝트);   // 만든 직후 한 번 호출
```

`UIFontHelper`는 `HasCharacter('가')`로 한글이 실제로 되는 글꼴을 찾아준다
(현재 프로젝트에서는 `NanumBarunGothic SDF`, Dynamic 모드).

### 3. 씬에 있는 패널 "안"에 UI를 만들지 않는다

씬의 `NotePanel` / `InventoryPanel`은 프로토타입 시절 크기·앵커가 제각각이라,
그 안에 UI를 만들면 코드에서 크기를 다시 잡아도 잘리거나 구석에 뜬다.

**UI는 Canvas 바로 아래에 만들고**, 씬 패널은 "열림/닫힘 스위치"로만 쓴다.
`NotePanelUI` / `InventoryPanelUI` / `SettingsPanelUI`가 이 방식이다.

### 4. 조사 화면은 씬에 만들지 않는다

`InvestigationData.csv`를 읽어 **런타임에 생성**된다. 씬에 준비할 것이 하나도 없다.
`InvestigationId`는 **배경 파일 이름과 1:1로 같다** (예: `BG_01_MyDesk`).
새 조사 화면 = CSV에 그 배경 이름으로 줄 추가.

### 5. 브랜치

작업은 `hyeona-feat`에서만. 다른 브랜치는 명시적 지시 없이 건드리지 않는다.

---

## 구조

```
Assets/
├─ Scripts/
│  ├─ Common/     IllustLoader, IllustLayout, UIFontHelper, GameBootstrap
│  ├─ Dialogue/   DialogueSystem(핵심), StageController, StandingSlot,
│  │              InvestigationController, DeductionController, GameFlowManager
│  ├─ Save/       SaveManager, SavePointManager, SaveSlotDialog, SaveData
│  └─ UI/         UIManager, SettingsPanelUI, NotePanelUI, InventoryPanelUI,
│                 NoteManager, ItemDatabase, DocumentViewerController,
│                 AudioManager, FontManager, AspectRatioKeeper
├─ Editor/        IllustTextureImporter, IllustPlacementWindow
├─ Scenes/        Title → SaveData / Settings / SampleScene(본편)
└─ Resources/     ⚠ gitignore. 드라이브에서 받아 넣어야 함
   ├─ Dialogues/  CSV 8종
   ├─ Illusts/    Backgrounds(34) / Objects(47) / Standings(58)
   └─ Sounds/     BGM + SFX/
```

### 흐름의 중심: `DialogueSystem`

CSV 한 줄 = 대사 한 줄. `LineType`으로 갈라진다.

| LineType | 동작 |
|---|---|
| `Normal` / `Narration` | 대사 출력 (타이핑 + 쪽 나누기) |
| `Choice` | 선택지 (**CSV 맨 끝에 몰아서** 적어야 함) |
| `Investigate` | `InvestigationController.Enter()` |
| `Deduction` | `DeductionController.Enter()` |
| `Minigame` | `MinigameController` (팀원 담당, 현재 스텁) |

미니게임·조사·추리는 전부 **똑같은 콜백 배관**이다:
띄우고 → 끝나면 `ShowNextSentence()`로 돌아온다. 새 파트를 추가할 때 이 패턴을 따르면 된다.

### `GameBootstrap`

씬에 없는 매니저를 게임 시작 시 **자동 생성**한다. 매니저를 새로 만들면 여기에 등록할 것.
씬마다 손으로 붙일 필요가 없고, 하나 빠뜨려서 기능이 조용히 죽는 사고를 막는다.

---

## 자주 밟는 지뢰

| 증상 | 원인 |
|---|---|
| 한글이 안 보임 / 검은 네모 | 코드로 만든 글자에 `UIFontHelper` 미적용 |
| UI가 잘리거나 구석에 뜸 | 씬 패널 안에 UI를 만듦 → Canvas 직속으로 |
| 버튼이 안 눌림 | `Button.targetGraphic` 미지정 or `Image.raycastTarget = false` |
| 조사 오브젝트가 엉뚱한 위치 | 그림이 타이트 크롭 → `IllustLayout.csv`에 좌표 필요 |
| 투명한 곳도 클릭됨 | 메뉴 `[2KH1] → [Illusts 폴더 그림 임포트 설정 다시 적용]` |
| 시작하자마자 예외 | `Resources/Dialogues/scenario_01.csv` 없음 (드라이브에서 받을 것) |
| 텍스트 영역 높이 0 | `offsetMin`/`offsetMax`를 둘 다 0으로 두면 높이가 0이 된다 |

### 아트 관련 중요 사항

조사 오브젝트·스탠딩 PNG는 **투명 여백이 잘린 채** 저장되어 있어서 화면 위치 정보가 없다.
두 가지 해결책이 있고 **첫 번째를 강력히 권한다**:

1. **아트를 1440x1080 캔버스 크기 그대로 내보내기** → 위치가 자동으로 맞는다. 배치 작업 불필요.
2. 메뉴 `[2KH1] → [일러스트 배치 도구]`로 마우스 드래그 배치 → `IllustLayout.csv`에 저장

---

## 코드 작성 규칙

- **주석을 상세하게, 한국어로.** 팀원 대부분이 유니티에 익숙하지 않아서 주석이 곧 문서다.
  "무엇을" 뿐 아니라 **"왜 이렇게 했는지"**, 그리고 **확장하는 방법**까지 적는다.
- CSV 컬럼은 **추가만** 한다. `CSVReader.GetField`가 없는 컬럼에 `""`를 돌려주므로
  예전 CSV도 그대로 동작한다. 순서를 바꾸거나 지우면 안 된다.
- **파일을 고쳤으면 사용자에게 목록을 알려준다.** 특히 gitignore된 `Resources/` 안의
  CSV·그림은 git에 안 잡히므로 말해주지 않으면 알 수가 없다.
- 매니저는 전부 같은 싱글톤 패턴:
  `if (Instance == null) Instance = this; else { Destroy(gameObject); return; }`

---

## 팀 분담

- **이번 작업 범위 밖(팀원 담당)**: 타임어택, 이상한 곳 조사 시 시간 감소,
  눈치게임 선택지, 자료실 미니게임 → `MinigameController`가 스텁으로 자리를 잡아두었다.
