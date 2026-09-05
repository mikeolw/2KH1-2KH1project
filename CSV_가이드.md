# CSV 작성 가이드

기획·시나리오 담당자가 **유니티를 열지 않고** 게임 내용을 고칠 수 있도록, 대사·조사·아이템·
조사기록·추리를 전부 CSV로 관리한다. 이 문서는 그 CSV들을 어떻게 쓰는지 정리한 것이다.

> **CSV 파일 위치**: `Assets/Resources/Dialogues/`
> 이 폴더는 `.gitignore` 대상이라 git으로 공유되지 않는다. 구글 드라이브에서 받아서
> 이 경로에 직접 넣어야 한다. (그림도 마찬가지 — `Assets/Resources/Illusts/`)

## 공통 규칙

- 첫 줄은 **컬럼 이름**이다. 지우거나 순서를 바꾸면 안 된다. 뒤에 새 컬럼을 추가하는 건 안전하다.
- 칸 안에 **쉼표(,)** 를 쓰려면 그 칸 전체를 큰따옴표로 감싼다. → `"오늘도, 어김없이"`
- 칸 안에서 **줄바꿈**을 하려면 `\n` 이라고 적는다. (엔터를 실제로 치면 안 된다)
- **비어 있는 칸**은 대부분 "이전 상태를 그대로 유지"라는 뜻이다. 바뀌는 줄에만 적으면 된다.
- 엑셀로 편집한다면 저장할 때 **CSV UTF-8** 형식을 선택할 것. (안 그러면 한글이 깨진다)

---

## 1. `scenario_XX.csv` — 대사 대본

한 줄이 대사 한 줄이다. 위에서 아래로 순서대로 재생된다.

| 컬럼 | 설명 |
|---|---|
| `LineType` | 줄의 종류. 아래 표 참고 |
| `Speaker` | 화자 이름 (예: `재훈`). `Narration`이면 비워둔다 |
| `Sentence` | 실제 대사 내용 |
| `BGM` | 배경음악 파일명(확장자 제외). 빈칸이면 **정지**, 같은 이름이면 계속 재생 |
| `SFX` | 효과음 파일명. BGM과 규칙 동일 |
| `IsFadeOut` | `TRUE`면 화면을 어둡게 했다가 이 줄을 보여주고 다시 밝아진다 |
| `Item` | 이 줄에서 자동으로 얻는 아이템의 `ItemId` |
| `ChoiceText` | 선택지 문구 (`LineType=Choice`일 때만) |
| `NextScenario` | 다음에 재생할 CSV 파일명 |
| `IsEndingChoice` | `TRUE`면 이 선택지를 고르는 순간 엔딩으로 직행 |
| `TargetEnding` | 갈 엔딩 이름: `True` `Bad_A` `Bad_B` `Bad_C` `Bad_D` `Bad_E` |
| `InvestigationId` | 띄울 조사 화면 (`LineType=Investigate`일 때) |
| `DeductionId` | 띄울 추리 문제 묶음 (`LineType=Deduction`일 때) |
| `Background` | 배경 그림 파일명. **빈칸=유지**, `none`=배경 지움 |
| `Standing` | 캐릭터 스탠딩 파일명. **빈칸=유지**, `none`=전원 퇴장, 여러 명은 `\|`로 구분 |
| `StandingPos` | 각 캐릭터 자리. `L`(왼쪽) `C`(가운데) `R`(오른쪽), `\|`로 구분 |
| `Talker` | 지금 말하는 캐릭터 자리. 빈칸이면 `Speaker` 이름으로 자동 인식, `none`이면 아무도 입을 안 움직임 |
| `IsSavePoint` | `TRUE`면 이 지점부터 저장 가능 |
| `SavePointId` | 세이브 슬롯에 표시될 이름 (예: `#01 사무실`) |
| `NoteRealtime` | `off`=조사기록 실시간 갱신 중지, `on`=보류분 한꺼번에 반영 |

### LineType 종류

| 값 | 하는 일 |
|---|---|
| `Normal` | 일반 대사 (화자 이름 표시) |
| `Narration` | 지문·독백 (화자 이름 숨김) |
| `Choice` | 선택지. **CSV 맨 끝에 몰아서** 적어야 한다 |
| `Investigate` | 조사 화면을 띄운다 (`InvestigationId` 필요) |
| `Deduction` | 추리 문제를 낸다 (`DeductionId` 필요) |
| `Minigame` | 미니게임을 띄운다 (팀원 담당, 현재 스텁) |

### 표정 바꾸기 예시

표정은 **바뀌는 줄에만** 적으면 된다. 빈칸인 줄은 앞의 표정이 그대로 유지된다.

```
Normal,한성,어른이 되어야 한다면...,,,,,,,,,,BG_Past01_Home,STD_Past01_Hansung_Default|STD_Past01_Jaehoon_Default,L|R,,,
Normal,재훈,경찰이 되고 싶은 거야?,,,,,,,,,,,,,,,          ← 표정 유지
Normal,한성,야!,,,,,,,,,,,STD_Past01_Hansung_Angry|STD_Past01_Jaehoon_Default,L|R,,,   ← 여기서만 화난 표정
```

### 입 뻐끔(립싱크)

스탠딩 파일 옆에 `_OpenMouse`가 붙은 짝이 있으면, 대사가 타이핑되는 동안 자동으로 입이 움직인다.
- `STD_Past01_Hansung_Default.png` + `STD_Past01_Hansung_Default_OpenMouse.png` → 자동 립싱크
- `_OpenMouse` 짝이 없으면 그냥 입을 안 움직인다 (오류 아님)

누가 말하는지는 `Characters.csv`의 한글 이름 ↔ 영문 토큰 표로 자동 판별한다.

---

## 2. `InvestigationData.csv` — 조사 화면

| 컬럼 | 설명 |
|---|---|
| `InvestigationId` | 조사 화면 이름. 시나리오의 `InvestigationId`와 일치해야 한다 |
| `HotspotKey` | 씬에 배치된 조사 오브젝트의 **GameObject 이름과 정확히 같아야** 한다 |
| `Type` | `Item`(획득) / `Description`(설명만) / `Talk`(대사창으로 말 걸기) |
| `ObjectName` | 조사했을 때 뜨는 이름 |
| `Speaker` | `Talk`일 때 화자 이름 |
| `Text` | 설명문 또는 대사 |
| `ItemId` | `Item`일 때 얻는 아이템 (`ItemData.csv`와 맞출 것) |
| `Sprite` | 오브젝트 그림 파일명 (`Illusts/Objects/` 기준) |

**특수한 HotspotKey 두 개**
- `IntroText` : 조사 화면 상단 안내문을 바꾼다
- `Background` : 그 조사 화면의 배경 그림을 지정한다 (`Sprite` 칸에 배경 파일명)

**투명 픽셀 클릭**: 조사 오브젝트는 투명 배경 PNG라도 **그림이 그려진 부분만** 클릭된다.
자동으로 적용되므로 따로 설정할 것은 없다.
> 그림을 새로 넣은 뒤 클릭이 이상하면 유니티 상단 메뉴
> **[2KH1] → [Illusts 폴더 그림 임포트 설정 다시 적용]** 을 한 번 실행할 것.

---

## 3. `ItemData.csv` — 아이템 정의

| 컬럼 | 설명 |
|---|---|
| `ItemId` | 아이템 고유 이름(영문). 다른 CSV에서 이 값으로 아이템을 가리킨다 |
| `DisplayName` | 가방에 표시될 한글 이름 |
| `Description` | 가방에서 아이템을 눌렀을 때 나오는 설명 |
| `Icon` | 가방 목록 아이콘 그림 파일명 |
| `ViewerType` | `None`=설명만 / `Document`=서류 펼쳐보기 / `Photo`=사진 펼쳐보기 |
| `ViewerImages` | 펼쳐서 볼 그림들. 여러 장은 `\|`로 구분 (넘겨서 볼 수 있다) |
| `AcquireScene` | 얻는 장면 (참고용 메모) |

## 4. `ItemCombinations.csv` — 아이템 조합

| 컬럼 | 설명 |
|---|---|
| `ItemA`, `ItemB` | 합칠 두 아이템 (순서 무관) |
| `ResultItem` | 합쳐서 나오는 아이템 |
| `ConsumeA`, `ConsumeB` | `TRUE`면 재료가 사라진다 |
| `ResultMessage` | 조합 성공 시 뜨는 문구 |

## 5. `NoteEntries.csv` — 조사기록(수첩)

| 컬럼 | 설명 |
|---|---|
| `EntryId` | 메모 고유 이름 |
| `TriggerType` | 언제 추가될지: `Item` / `Investigate` / `Hotspot` / `Manual` |
| `TriggerKey` | `Item`→아이템id, `Investigate`→조사화면id, `Hotspot`→`조사화면id\|핫스팟키` |
| `Chapter` | 수첩에서 묶어 보여줄 소제목 (예: `#01 사무실`) |
| `Text` | 수첩에 적히는 문장. **재훈의 시점(1인칭)** 으로 쓴다 |
| `Order` | 같은 챕터 안에서의 순서 (작을수록 위) |

## 6. `DeductionData.csv` — 추리 문제

| 컬럼 | 설명 |
|---|---|
| `DeductionId` | 추리 묶음 이름 |
| `Step` | 몇 번째 문제인지 (1, 2, 3...). 같은 번호끼리 한 문제의 보기가 된다 |
| `Question` | 문제 문장. 같은 Step의 첫 줄에만 적으면 된다 |
| `ChoiceText` | 보기 문구 |
| `IsCorrect` | 정답이면 `TRUE` |
| `ResultText` | 이 보기를 골랐을 때의 반응 문장 |
| `WrongEnding` | 틀렸을 때 갈 엔딩. 비우면 `Bad_D` |

**하나라도 틀리면 그 자리에서 바로 엔딩으로 간다.** (다시 풀 기회 없음)

## 7. `Characters.csv` — 화자 이름 ↔ 스탠딩 연결

| 컬럼 | 설명 |
|---|---|
| `Speaker` | 대사 CSV의 `Speaker` 칸에 적는 한글 이름 (예: `한성`) |
| `StandingToken` | 스탠딩 파일명에 들어 있는 영문 (예: `Hansung`) |
| `DisplayName`, `Note` | 참고용 |

이 표에 없는 화자는 입 뻐끔 연출이 적용되지 않는다(스탠딩 그림이 없는 인물).

---

## 그림 파일 넣는 곳

| 종류 | 폴더 | CSV에 적는 예시 |
|---|---|---|
| 배경 | `Assets/Resources/Illusts/Backgrounds/` | `BG_01_Office` |
| 조사 오브젝트 | `Assets/Resources/Illusts/Objects/` | `OBJ_01_Notepad` |
| 캐릭터 스탠딩 | `Assets/Resources/Illusts/Standings/` | `STD_Past01_Hansung_Default` |
| 배경음악 | `Assets/Resources/Sounds/` | `reminiscence1-1` |
| 효과음 | `Assets/Resources/Sounds/SFX/` | `Door1_SFX` |

**확장자(.png, .mp3)는 적지 않는다.**

---

## 자주 겪는 문제

| 증상 | 원인과 해결 |
|---|---|
| 게임 시작하자마자 오류 | `scenario_01.csv`가 폴더에 없다. 드라이브에서 받아 넣을 것 |
| 그림이 안 나옴 | 파일명 철자·대소문자 확인. 콘솔에 `[IllustLoader]` 경고가 뜬다 |
| 조사 화면이 안 뜸 | `InvestigationId`가 씬의 조사 화면 등록 이름과 다르다 |
| 조사 오브젝트를 눌러도 반응 없음 | `HotspotKey`가 씬의 GameObject 이름과 다르다 |
| 투명한 곳도 클릭됨 | `[2KH1] → [Illusts 폴더 그림 임포트 설정 다시 적용]` 실행 |
| 한글이 깨짐 | CSV를 **UTF-8**로 저장했는지 확인 |
| 저장 버튼이 안 눌림 | 아직 세이브포인트를 지나지 않았다. 정상 동작이다 |
