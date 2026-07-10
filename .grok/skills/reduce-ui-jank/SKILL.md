---
name: reduce-ui-jank
description: >
  WPF 앱 UI 렉·스터터링·프레임 드랍·창 전환 지연을 분석 → 기획(3안) → 구현 →
  Windows/WPF 측정으로 검증하는 루프. Dispatcher(UI 스레드) 부하, 불필요한
  바인딩 재평가·레이아웃 패스, 동기 I/O·이미지 디코드, 스크롤·애니메이션 병목을
  찾아 우선순위를 매기고, 항상 보완 깊이별 3가지 방안을 제시한 뒤 사용자가 고른
  방향으로 고치고 before/after로 효과를 숫자·체감으로 확인한다.
  Use when: "UI 렉", "스터터링", "프레임 드랍", "jank", "버벅", "끊김",
  "메인 스레드", "Dispatcher", "부드럽지 않", "성능 병목", "창이 느림",
  "썸네일 느림", "reduce-ui-jank", "/reduce-ui-jank",
  또는 WPF 화면 동작이 무거워 분석·기획·측정으로 개선하고 싶을 때.
metadata:
  short-description: "WPF UI jank: analyze → 3 options → measure"
---

# /reduce-ui-jank — WPF UI 렉·스터터링 줄이기

**Windows WPF 데스크톱 앱**(이 레포: DeckDeckDeck)에서 끊김·버벅임·프레임 드랍·
창/화면 전환 지연을 **코드 분석 → 방안 제시 → 구현 → 측정 검증** 순으로 다룬다.

이 skill은 “무조건 최소 변경”을 강요하지 않는다.  
대신 **깊이 다른 3가지 보완 방안**을 항상 제시하고, 사용자가 고른 안으로 진행한다.

**하지 말 것:** Android `adb` / `dumpsys gfxinfo` / Compose / Gradle / Wear 전용 절차.  
이 프로젝트는 WPF + .NET이다.

---

## 언제 쓰는가

- 홈·카테고리·편집 화면 전환 시 멈칫
- 슬롯/리스트 썸네일·아이콘이 늦게 뜨거나 스크롤이 끊김
- 트레이에서 창 다시 열기·팔레트 표시가 무거움
- 저장/붙여넣기/핫키 직후 UI가 한동안 굳음
- 애니메이션·페이드 중 스터터링
- 사용자가 `/reduce-ui-jank` 또는 위 키워드로 요청

---

## 전체 루프 (필수 순서)

```
1) 분석(Analysis)     → 병목 후보 + 우선순위 보고서 (+ 가능하면 before 실측)
2) 기획(Plan)         → 반드시 3안 제시 → 사용자 선택
3) 구현(Implement)    → 선택한 안만 (또는 합의된 범위)
4) 검증(Verify)       → 회귀 테스트 + 실측(L1~L4) + 이전 기록과 비교
5) 보고(Report)       → 계층별 숫자·체감 비교, 남은 리스크, 다음 후보
```

중간에 사용자가 “분석만 / 기획만 / 측정만”이라고 하면 해당 단계만 한다.

프로젝트 지침:

- `AGENTS.md` — 한국어 설명, 최소 관련 변경, 테스트·exe 준비 규칙
- `docs/clean-architecture.mini.md` — UI/게이트웨이/유스케이스 책임 경계
- `docs/a-philosophy-of-software-design.mini.md` — 측정 없는 최적화 금지 취지
- 디자인 관련이면 `docs/DESIGN_GUIDELINES.md`

---

## Phase 1 — 분석

### 1.1 범위 잡기

- 화면·시나리오를 **한 문장**으로 고정한다.  
  예: “홈 숫자패드 슬롯 썸네일 9칸이 보이는 상태에서 카테고리 전환”  
  예: “트레이 더블클릭 후 메인 창이 다시 보일 때까지”  
  예: “스니펫 편집 화면을 아래로 스크롤할 때”
- 애매하면 짧게 확인한다. 합리적 기본값이 있으면 밝히고 진행한다.

DeckDeckDeck에서 자주 쓰는 시나리오 후보:

| ID | 시나리오 |
|----|----------|
| S-home | 앱 기동 후 홈 첫 표시까지 |
| S-nav | 홈 ↔ 편집/설정/핫키 목록 전환 |
| S-thumb | 썸네일이 많은 슬롯 그리드 첫 표시·재진입 |
| S-scroll | 편집/설정 `ScrollViewer` 스크롤 |
| S-tray | 트레이에서 창 재표시 |
| S-hotkey | 글로벌/다이렉트 핫키 → 실행 또는 팔레트 |
| S-paste | 붙여넣기 선택 세션 중 UI 응답 |

### 1.2 코드에서 찾을 것 (WPF 우선순위 단서)

| 증상 후보 | 코드 신호 |
|-----------|-----------|
| 주기적 hitch | `DispatcherTimer`, 짧은 주기의 `BeginInvoke`, 타이머마다 큰 VM 갱신 |
| 탭/전환 직후 멈칫 | UI 스레드 동기 I/O, 깊은 컬렉션 재구성, 다중 `OnPropertyChanged` 연쇄 |
| 이미지 늦게 뜸 / 스크롤 끊김 | 바인딩 컨버터에서 동기 `BitmapImage` 디코드, 캐시 미스, 큰 원본 디코드, `File.Exists` 반복 |
| 창 표시 느림 | 생성자·`Loaded`에서 무거운 초기화, 트레이→Show 경로의 동기 작업 |
| 레이아웃 thrash | 측정/정렬을 유발하는 잦은 크기 변경, 중첩 `ScrollViewer`, 과도한 템플릿 재생성 |
| GPU/합성 vs UI | `BitmapCache` 과다, 큰 비주얼 트리 + 애니메이션, UI 스레드 디코드 vs 렌더 |

**검색 키워드 예 (이 레포 기준):**

```text
Dispatcher.Invoke
Dispatcher.BeginInvoke
DispatcherTimer
Task.Run
OnPropertyChanged
SetProperty
IValueConverter
BitmapImage
DecodePixelWidth
Freeze(
BitmapCache
CacheMode
CompositionTarget
File.Read
File.Exists
ShellFileIcon
Thumbnail
Prewarm
InitializeHome
CurrentViewModel
```

**이미 잘 된 부분(분석 시 “유지”로 적을 것, 함부로 제거 금지):**

- `CachedImageSourceConverter` — 경로 probe 캐시, `DecodePixelWidth`, `Freeze`, `PrewarmFiles`
- `App.xaml.cs` — 첫 렌더 후 `InitializeHome`, 썸네일 백그라운드 prewarm
- `StartupTimingLog` — 기동 구간 타이밍
- `MainWindow` / `Theme` — `BitmapCache`, 페이드 스토리보드 (과하면 비용이 될 수 있음 → 측정으로 판단)

**책임 경계 주의:**

- jank 원인이 유스케이스·도메인 “규칙”이 아니라 **표시/디코드/I/O 배치**인 경우가 많다.
- 규칙을 UI에 끌어오거나, 게이트웨이를 ViewModel에 직접 때려 넣지 않는다.
- 표시용 캐시·스로틀은 View/Infrastructure 쪽에 두고, Domain/UseCases를 오염시키지 않는다.

### 1.3 실측 (가능하면 분석 단계부터)

구현 **전 before** 를 잡는다. 세부 절차는  
`references/measure-wpf-performance.md` 를 따른다.

기록할 것 (가능한 범위):

| 지표 | 의미 |
|------|------|
| 시나리오 완료 시간 (ms) | 스톱워치 / `Stopwatch` / `StartupTimingLog` |
| UI 스레드 블로킹 | VS Diagnostic Tools, 수동 “입력이 안 먹힌 구간” |
| 레이아웃/렌더 비용 | WPF Performance / Timeline 샘플 |
| 메모리·이미지 수 | 디코드 폭주 여부 |
| 체감 메모 | 끊김 횟수, 어느 동작 직후인지 |

측정 불가 시: **이유를 쓰고**, 코드 기반 추정임을 명시한다.  
“느릴 것 같다”만으로 Phase 3으로 가지 않는다.

### 1.4 분석 산출물

사용자에게 **한국어**, 쉬운 말로:

1. 한 줄 요약  
2. 이미 잘 된 부분  
3. 병목 목록 (P0 / P1 / P2…) + 파일·근거  
4. 시나리오별 부담 지도  
5. (있으면) before 숫자  
6. 다음 단계: 기획 3안 제시 예정  

보고서 파일은 사용자가 요청할 때만 저장한다. 기본은 대화 응답.

---

## Phase 2 — 기획 (3안 필수)

**구현 전에** 반드시 아래 3단 깊이로 방안을 제시한다.  
“최소 변경만” 고르지 말고, 트레이드오프를 드러낸다.

### 안 A — 기존 방법에서 보완

- 구조·API·책임 경계는 거의 유지
- 캐시 키/TTL, prewarm 범위, `DecodePixelWidth`, 조건 가드,  
  불필요 `PropertyChanged` 제거, `BeginInvoke` 우선순위 조정, 동기 경로 가드 등
- **장점:** 리스크·리뷰 범위 작음  
- **단점:** 한계가 빨리 올 수 있음  
- **예상 효과 / 검증 방법**

### 안 B — 기존 로직·아키텍처를 조금 수정하며 보완

- 이미지 로드를 UI 바인딩 밖·백그라운드로 이동, 상태 위치 조정,  
  화면 전환 시 로드 순서 변경, 작은 표시 전용 모델 분리, 디스패처 사용 정리
- **장점:** 원인에 더 직접적  
- **단점:** 호출부·테스트·바인딩 수정 필요  
- **예상 효과 / 검증 방법**

### 안 C — 로직·아키텍처·패러다임을 더 크게 바꾸며 보완

- 가상화 리스트 도입, 이미지 파이프라인 재설계, 창/팔레트 수명주기 재구성,  
  렌더 모델·네비게이션 구조 교체 등
- **장점:** 천장 성능·유지보수에 유리할 수 있음  
- **단점:** 범위·회귀 위험 큼, 단계 분할 필요할 수 있음  
- **예상 효과 / 검증 방법**

### 기획 표 형식 (권장)

| | 안 A (보완) | 안 B (소수정) | 안 C (대수정) |
|--|-------------|---------------|---------------|
| 핵심 아이디어 | | | |
| 주요 터치 파일 | | | |
| AGENTS/아키텍처 충돌 | | | |
| 리스크 | | | |
| 예상 체감 | | | |
| 권장 여부 | (한 줄 이유) | | |

- 분석 근거상 **추천 1개**를 표시하되, 선택을 강요하지 않는다.
- 사용자가 고르기 전 **코드를 크게 바꾸지 않는다**.
- 디자인 토큰·레이아웃을 바꿀 계획이면 `DESIGN_GUIDELINES.md` 준수 여부를 적는다.

---

## Phase 3 — 구현

1. 사용자가 고른 안(또는 “A+B 일부” 등 합의 범위)만 구현한다.  
2. 고르지 않은 안의 대규모 리팩터를 몰래 섞지 않는다.  
3. 사용자 대면 설명은 한국어·쉬운 말로 (`AGENTS.md`).  
4. 관련 단위 테스트·뷰 렌더 테스트를 맞춘다.  
5. 구현 중 발견한 **별 이슈**는 이번 범위에 섞지 말고 보고에 남긴다.  
6. WPF UI 스레드 규칙: UI 객체·`BitmapImage` 생성/사용 스레드를 지키고,  
   필요할 때만 `Dispatcher`로 마샬링한다. `Freeze()` 가능한 자유 스레드는 문서화한다.

---

## Phase 4 — 검증

검증은 **회귀 테스트 + 실측 + (가능하면) 이전 기록 비교**를 함께 한다.  
단위 테스트만 통과했다고 jank 개선 성공으로 단정하지 않는다.

측정 세부·명령·로그 경로: `references/measure-wpf-performance.md`.

### 4.1 자동 회귀

```powershell
dotnet test .\tests\DeckDeckDeck.App.Tests\DeckDeckDeck.App.Tests.csproj
```

관련 시나리오가 있으면 해당 테스트 클래스만 먼저 돌려도 된다.  
빌드 깨짐·테스트 실패는 성공으로 보고하지 않는다.

### 4.2 실측 계층 (가능하면 모두)

| 계층 | 무엇을 재나 | 이 레포에서 |
|------|-------------|-------------|
| **L1 로직 기동** | S-home 구간 ms | `StartupTimingLog` → `%APPDATA%\NumpadPromptLauncher\logs\app.log` |
| **L2 타이밍 테스트** | 네비/캐시 등 **로직 경로** wall-clock | `NavigationCacheTimingTests` 등 (`Stopwatch` + 중간값) |
| **L3 실제 exe** | 배포 바이너리 기동·스모크 | publish → `DeckDeckDeck.exe` 실행 후 로그/체감 |
| **L4 체감·프로파일** | 렌더·그림자·입력 불가 | 사용자 체크리스트 / VS Diagnostic Tools |

**해석 규칙 (중요):**

- L2는 ViewModel·DB·캐시 비용을 잘 보여 준다. **WPF 레이아웃·그림자·이미지 디코드 전체 체감과 동일하지 않다.**  
- L1/L3은 실제 프로세스 기동. 안이 기동을 안 건드렸으면 total이 비슷해도 정상이다.  
- 보고 시 계층을 표에 명시한다. “테스트 ms = 화면이 N배 부드러움”으로 과장하지 않는다.

### 4.3 이전 기록과 비교 (before / after)

1. **Before 출처를 밝힌다** (우선순위):  
   - 이번 세션 Phase 1에서 남긴 숫자  
   - `app.log`의 직전 `Startup timing` 줄들 (날짜·구간)  
   - 동일 타이밍 테스트의 cold 경로 / 캐시 없을 때 중간값  
   - 없으면 “before 없음 → after만 기록, 다음 비교용 기준선”이라고 쓴다  
2. **After**는 구현·테스트 통과 **후** 같은 명령·같은 시나리오로 다시 측정한다.  
3. **같은 조건**만 나란히 둔다 (가능하면 Release/exe, 슬롯·이미지 수 유사).  
4. 비교 표 필수 열:

| 지표 | 계층 | Before | After | 해석 |
|------|------|--------|-------|------|
| 시나리오 완료 중간값 (ms) | L2/L4 | | | |
| StartupTiming total / 주요 구간 | L1 | | | |
| 캐시 히트 vs 미스 (해당 시) | L2 | | | |
| UI 멈춤 체감 | L4 | | | |
| 빌드 | | Debug/Release/exe | | |

5. 성공 기준: 기획 때 합의값, 또는 “warm ≤ cold + 여유”, “입력 불가 체감 없음” + 회귀 통과.  
6. **목표 미달**이면 숨기지 말고 남은 병목과 다음 안(B/C)을 제안한다.

**L2 실행 예 (네비 캐시·전환 로직):**

```powershell
dotnet test .\tests\DeckDeckDeck.App.Tests\DeckDeckDeck.App.Tests.csproj `
  --filter "FullyQualifiedName~NavigationCacheTimingTests" `
  --logger "console;verbosity=detailed"
```

출력의 `cold` / `warm` / `rebuild` 중간값을 표에 옮긴다.  
cold ≈ “매번 새로 만들던 이전 경로”, warm ≈ “캐시 재사용 after”로 해석할 수 있다  
(구현 전 전용 before 런이 없을 때의 **합리적 대리 비교**).

**L1 실행 예:**

1. 기존 `DeckDeckDeck` 프로세스 종료 후 최신 exe 기동  
2. `app.log` 마지막 `Startup timing total=…` 줄을 before 기록과 나란히 비교  

### 4.4 exe 갱신 (AGENTS.md)

`AGENTS.md`에 따라 **관련 코드 변경 + 테스트 성공 후** 사용자가 더블클릭 실험할 수 있게  
Windows x64 단일 파일 exe를 갱신한다:

```powershell
.\scripts\publish-win-x64.cmd -SelfContained -SingleFile
Get-Process -Name DeckDeckDeck -ErrorAction SilentlyContinue | Stop-Process
Copy-Item .\artifacts\publish\win-x64\DeckDeckDeck.App.exe .\DeckDeckDeck.exe -Force
.\DeckDeckDeck.exe
```

파일이 잠겨 있으면 rename 후 복사 등 우회를 시도하고,  
단일 인스턴스 때문에 **옛 프로세스가 남으면 사용자가 종료 후 재실행**해야 최신 빌드가 뜬다고 알린다.

### 4.5 스모크 체크리스트 (사용자용)

시나리오에 맞게 고른다. 예:

- [ ] 홈 슬롯 썸네일이 깨지지 않고 표시된다  
- [ ] 카테고리/화면 전환 후 빈 화면·중복 로딩이 없다  
- [ ] 편집 화면 스크롤·저장이 동작한다  
- [ ] 핫키·붙여넣기·트레이 열기가 이전과 같다  
- [ ] 이미지 없는 슬롯·잘못된 경로에서 예외로 죽지 않는다  
- [ ] (캐시 안) 저장 후 목록/제목이 최신으로 보이는지  

---

## Phase 5 — 보고

- 무엇을 왜 바꿨는지 (파일 단위, 쉬운 말)  
- **계층별** before/after 숫자 또는 측정 불가 사유  
- 이전 기록 출처 (로그 날짜, 테스트 클래스, Phase 1 메모)  
- 목표 달성 여부 (계층마다 다를 수 있음 — 예: L2 개선, L1 동일)  
- 남은 P0/P1  
- 다음에 쓸 안(B/C)이 있으면 한 줄 예고  
- exe 갱신 여부 (`DeckDeckDeck.exe`)  

---

## 행동 원칙

1. **측정 가능한 주장** — 코드 근거 + 가능하면 숫자.  
2. **3안 강제** — 기획 단계에서 A/B/C를 빼먹지 않는다.  
3. **선택 존중** — 사용자가 C를 고르면 최소 변경 강요로 되돌리지 않는다. 리스크는 분명히.  
4. **시나리오 순수성** — 측정에 무관한 자동 애니메이션·백그라운드 백업·다른 창 조작을 섞지 않는다.  
5. **UI 스레드 vs 백그라운드** — 멈춤이 Dispatcher 블로킹인지, 디스크/디코드인지, 레이아웃인지 구분한다.  
6. **보안** — 비밀키·토큰을 로그/테스트에 넣지 않는다.  
7. **프로젝트 지침** — `AGENTS.md` 및 clean architecture / design 가이드 우선.  
8. **Android 절차 금지** — `adb`, gfxinfo, Compose 리컴포즈 용어로 이 앱을 진단하지 않는다.

---

## 안티패턴

- 분석 없이 바로 대규모 리팩터  
- 3안 없이 “이 한 가지로 갑니다”  
- before 없이 after만 보고 성공 선언  
- 캐시·`Freeze`·prewarm을 “복잡하다”는 이유로 측정 없이 제거  
- Domain/UseCases에 UI 캐시·Bitmap 타입을 끌어올림  
- 요청 범위 밖 포맷 변경·파일 정리 섞기  
- jank 해결을 위해 디자인 가이드를 무시하는 레이아웃 개조  

---

## 빠른 체크리스트

- [ ] 시나리오 한 문장 (WPF/DeckDeckDeck 맥락)  
- [ ] P0 병목 + 근거 파일  
- [ ] before 측정·출처 (또는 불가 사유 / cold 대리 기준)  
- [ ] 안 A / B / C 제시 + 추천 + 아키텍처 충돌 메모  
- [ ] 사용자 선택  
- [ ] 구현  
- [ ] `dotnet test` 회귀  
- [ ] L2 타이밍 테스트 (해당 시) + L1 `app.log` after  
- [ ] before/after 표 (계층 열 포함)  
- [ ] 필요 시 publish / `DeckDeckDeck.exe`  
- [ ] 스모크 체크리스트 + 남은 이슈  
