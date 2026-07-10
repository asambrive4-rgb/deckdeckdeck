# WPF / Windows 성능 측정 메모

`reduce-ui-jank` Phase 1·4에서 쓰는 **Windows WPF** 측정 참고.  
Android `adb` / `dumpsys gfxinfo` 는 사용하지 않는다.

---

## 0. 측정 원칙

1. **시나리오를 한 문장으로 고정**한 뒤 before/after를 같은 조건으로 비교한다.  
2. 가능하면 **Release** (또는 publish된 `DeckDeckDeck.exe`) 로 체감 측정한다. Debug는 상대 비교만.  
3. 측정 중 **다른 창 조작·자동 백업·대용량 파일 복사**를 섞지 않는다.  
4. “janky 100%” 같은 모바일 지표 대신, 이 앱에서는  
   **완료 시간(ms) + UI 멈춤 여부 + (가능 시) 프로파일러 샘플** 을 쓴다.  
5. before 없이 after만 보고 성공 선언하지 않는다.

---

## 1. 가벼운 측정 (도구 설치 최소)

### 1.1 스톱워치 / 체감 체크리스트

사용자가 직접:

1. 시나리오 직전 상태까지 만든다.  
2. 동작 시작과 동시에 스톱워치.  
3. “완료로 정의한 순간”(예: 썸네일 전부 보임, 편집 화면 입력 가능)에 정지.  
4. 3회 측정 → 중간값 기록.

기록 예:

| 회차 | ms | 메모 |
|------|-----|------|
| 1 | | |
| 2 | | |
| 3 | | |
| 중간값 | | |

### 1.2 앱 내 기동 타이밍 (`StartupTimingLog`) — L1

DeckDeckDeck은 기동 구간을 로그로 남긴다.

- 코드: `src/DeckDeckDeck.App/Infrastructure/Diagnostics/StartupTimingLog.cs`
- 앱 데이터 폴더: `%APPDATA%\NumpadPromptLauncher\`  
  (`AppStoragePaths.AppFolderName`)
- 로그 파일: `%APPDATA%\NumpadPromptLauncher\logs\app.log`
- 한 줄 형식: `Startup timing total=…ms: app composition=…; …; initial home load=…`

**S-home / 콜드 스타트** 시나리오 before/after에 유용.

측정 팁:

- 동일 PC, 가능하면 동일 배터리/전원 상태  
- **직전 실행 프로세스를 종료한 뒤** 측정 (단일 인스턴스면 재실행이 옛 창만 연다)  
- before: 로그에 남아 있는 **이전** `Startup timing` 줄 (날짜·total·composition)  
- after: 최신 exe 기동 직후 **마지막** `Startup timing` 줄  

PowerShell로 최근 기동 줄만 보기:

```powershell
Select-String -Path "$env:APPDATA\NumpadPromptLauncher\logs\app.log" -Pattern "Startup timing" |
  Select-Object -Last 8
```

### 1.3 타이밍 단위 테스트 (로직 경로 wall-clock) — L2

제품 코드에 진단 로그를 넣지 않고도, **테스트에서 `Stopwatch`로 경로 비용**을 잴 수 있다.

이 레포 예:

| 테스트 | 시나리오 | 해석 |
|--------|----------|------|
| `NavigationCacheTimingTests` | 홈↔카테고리 열기, 저장 후 재생성 | cold = 매번 VM/DB 생성(이전 경로에 가깝), warm = 캐시 재사용(안 B after) |

```powershell
dotnet test .\tests\DeckDeckDeck.App.Tests\DeckDeckDeck.App.Tests.csproj `
  --filter "FullyQualifiedName~NavigationCacheTimingTests" `
  --logger "console;verbosity=detailed"
```

작성·유지 규칙:

- 샘플 여러 번 → **중간값(median)** 보고 (첫 회 JIT/워밍업 스파이크 분리)  
- 절대 ms 하드 게이트은 환경마다 깨지기 쉽다 → **상대 비교**(warm ≤ cold) + 느슨한 상한(예: rebuild 2초 미만)  
- 시드 데이터 규모를 고정해 before/after·cold/warm 조건을 맞춘다  
- 출력은 `ITestOutputHelper`로 남겨 Phase 5 표에 옮긴다  
- **Domain/UseCases에 Bitmap·캐시 타입을 끌어오지 않는다** — 측정은 테스트·Infra·VM 경계에서  

한계: L2는 **ViewModel·저장소 경로**다. 페이드·`DropShadow`·`OpacityMask`·디코드 체감은 L4.

### 1.4 임시 Stopwatch 계측 (분석·검증용)

짧은 구간만 볼 때 **합의된 진단 코드**를 제품에 넣을 수 있다.

- `Stopwatch`로 구간 ms 기록  
- 또는 기존 `StartupTimingLog` 패턴을 확장  
- **검증 후 제거**하거나, 상시 로그가 필요하면 파일 로거에만 남기고 UI에 노출하지 않는다  
- 비밀·경로 전체를 로그에 과다 출력하지 않는다

---

## 2. Visual Studio Diagnostic Tools (권장, 개발 PC)

대상: UI 스레드 블로킹, CPU 핫스팟, 할당.

1. Visual Studio에서 WPF 프로젝트 디버그 실행  
2. **Diagnostic Tools** 창에서 CPU Usage / Events 확인  
3. 시나리오 재현  
4. 샘플에서 상위 스택 확인:

| 보이는 패턴 | 해석 단서 |
|-------------|-----------|
| `BitmapImage` / 디코더 / GDI / Shell | 이미지·아이콘 추출이 UI 경로 |
| `FileStream` / SQLite / EF | 동기 디스크·DB가 UI를 막음 |
| `Measure` / `Arrange` / `OnRender` | 레이아웃·렌더 thrash |
| `PropertyChanged` / 바인딩 업데이트 폭주 | 과도한 알림·큰 트리 갱신 |

before/after 비교 시 **같은 시나리오 구간**의 상위 스택·블로킹 시간을 메모한다.

---

## 3. WPF 성능 관련 추가 옵션

환경에 따라 사용 가능하면 사용하고, 없으면 “도구 없음”으로 적고 1·2항에 의존한다.

| 도구 | 용도 |
|------|------|
| Visual Studio **Timeline** / Performance Profiler | UI 스레드 vs 렌더, 이벤트 타임라인 |
| **Perfetto** / ETW (고급) | 시스템 전역 지연 — 필요할 때만 |
| **PresentMon** 등 프레임 도구 | 전체 화면 애니메이션 fps — 이 앱은 작은 창이라 우선순위 낮음 |

이 앱은 게임형 60fps 풀스크린이 아니다.  
**입력 지연·화면 전환·이미지 준비 완료**가 1순위 지표다.

---

## 4. 시나리오별 측정 프로토콜

### 공통

1. 앱을 시나리오 **직전** 상태까지 만든다.  
2. 카운터/스톱워치/프로파일러 구간 시작.  
3. **순수 동작만** 수행 (불필요 클릭·창 이동 자제).  
4. 완료 조건에 도달하면 정지·저장.  
5. 로그/스크린샷/표를 `tmp-perf/` 등에 남길 때는 사용자가 요청하거나 에이전트가 합의했을 때만.  
   (기본적으로 대화에 숫자만 남겨도 됨.)

### S-home — 기동~홈 표시

- 완료: 홈 그리드가 보이고 입력이 가능  
- 지표: `StartupTimingLog` total + 주요 구간, 체감 ms  

### S-nav — 화면 전환

- 완료: 대상 View 표시 + 첫 의미 있는 콘텐츠  
- 주의: 전환 페이드 애니메이션 시간을 실패로 오인하지 말 것 (애니메이션 길이와 블로킹을 구분)

### S-thumb — 썸네일 다수

- 완료: 보이는 슬롯 썸네일이 채워짐 (플레이스홀더→실이미지)  
- 코드 단서: `CachedImageSourceConverter`, prewarm, `DecodePixelWidth`  

### S-scroll — 스크롤

- 동작: 편집/설정 화면을 위아래로 수 회  
- 지표: 끊김 횟수 체감, 프로파일러상 디코드/레이아웃 스파이크  

### S-tray — 트레이 재표시

- 완료: 메인 창이 포그라운드·입력 가능  
- 단서: `ShowMainWindow`, `Dispatcher.BeginInvoke` 경로  

### S-hotkey / S-paste

- 완료: 실행 완료 또는 팔레트/선택 UI가 반응  
- 단서: Coordinator, 클립보드 게이트웨이 — **동기 Win32/클립보드**가 UI를 막는지 확인  

---

## 5. 해석 가이드

| 관찰 | 우선 의심 |
|------|-----------|
| 클릭 후 수 100ms~수 초 입력 불가 | UI 스레드 동기 작업 (I/O, 디코드, 무거운 동기 호출) |
| 입력은 되는데 이미지만 늦게 참 | 백그라운드/첫 디코드·캐시 미스 (체감 품질 이슈) |
| 전환 애니메이션만 부드럽고 끝에서 멈칫 | 전환 후 초기화(`Initialize*`, 컬렉션 재바인딩) |
| 스크롤 중 주기적 끊김 | 뷰포트 진입 시 동기 이미지 로드, 레이아웃 재측정 |
| 기동만 느림 | `StartupTimingLog` 구간 분해 — 트레이 아이콘, DB, 첫 Show 등 |

**GPU vs UI:**  
WPF에서 “느림”의 대부분은 **Dispatcher(UI) 작업** 또는 **첫 이미지 디코드**다.  
`BitmapCache`는 애니메이션 비용을 줄일 수도, 메모리·초기 래스터 비용을 늘릴 수도 있다 → 측정으로 판단.

---

## 6. before / after 표 템플릿 (이전 기록 비교)

**Before 출처를 한 줄로 적는다** (로그 날짜, 테스트 클래스, Phase 1 메모 중 하나).

| 지표 | 계층 | Before (출처) | After | 해석 |
|------|------|---------------|-------|------|
| 시나리오 완료 중간값 (ms) | L2/L4 | | | |
| 최악 1회 (ms) | L2/L4 | | | |
| StartupTiming total | L1 | | | |
| StartupTiming composition / home load | L1 | | | |
| 캐시 미스(cold) vs 히트(warm) | L2 | | | |
| UI 입력 불가 체감 | L4 | 있음/없음 | | |
| 프로파일러 상위 1스택 | L4 | | | |
| 빌드 종류 | | Debug/Release/exe | | |

**이전 기록이 없을 때**

- L2: 같은 런에서 cold(미스) vs warm(히트)을 before/after **대리 비교**로 써도 된다. 단, “구현 전 전용 before 런은 없음”을 명시.  
- L1: `app.log`에 남은 과거 `Startup timing` 줄을 before로 쓴다.  
- 둘 다 없으면 after만 기준선으로 남기고, 다음 `/reduce-ui-jank`에서 비교한다고 적는다.

성공 기준 예:

- 중간값 N% 감소, 또는 warm ≤ cold  
- “입력 불가 구간 체감 없음” + 회귀 테스트 통과  
- 기동 안을 안 건드렸으면 L1 동결(변화 없음)도 정상  

---

## 7. 자동 검증과의 관계

측정은 **체감·프로파일·타이밍 테스트**이고, 회귀는 **기능 테스트**로 막는다.

```powershell
dotnet test .\tests\DeckDeckDeck.App.Tests\DeckDeckDeck.App.Tests.csproj
```

이미지 캐시·경로·네비게이션 관련 테스트가 있으면 우선 실행.  
`NavigationCacheTimingTests`처럼 **실측용** 테스트는 verbosity detailed로 숫자를 남긴다.  
테스트가 통과해도 jank가 남을 수 있다 — L1~L4를 구분해 보고한다.

---

## 8. 안티패턴

- Debug 한 번 측정으로 “N배 빨라짐” 단정  
- 페이드 애니메이션 duration을 성능 개선으로 보고  
- 측정 중 백업·업데이트·다른 앱 풀스크린  
- adb/gfxinfo 수치를 이 프로젝트 보고서에 인용  
- before 시나리오와 after 시나리오가 다름 (슬롯 수·이미지 유무 불일치)  
