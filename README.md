# DeckDeckDeck

Windows용 개인 스트림덱(Stream Deck) 기능을 모방한 텐키(Numpad) 기반 단축키 & 런처 데스크톱 앱입니다.

서버, 계정, 클라우드 동기화 없이 모든 데이터를 로컬에 저장하며, 텐키 단축키와 텐키 모양의 UI를 사용하여 문구 붙여넣기, 프로그램 실행, 미디어 제어, 터미널 명령 등의 작업을 신속하게 실행할 수 있습니다.

---

## 🚀 주요 특징 (Key Features)

### 1. 텐키 기반 UI & 전역 단축키
- **홈 화면 열기**: `Ctrl + Numpad 0`으로 15개 덱(카테고리)을 한눈에 볼 수 있는 홈 화면을 호출합니다.
- **카테고리 바로가기**: `Ctrl + Numpad 1~9` 및 `/`, `*`, `-`, `+`, `.` 키 조합을 통해 특정 덱 화면으로 즉시 진입합니다.
- **텐키 배열 슬롯**: 카테고리 선택 및 개별 카드 실행 UI가 물리적인 텐키 키패드 레이아웃(15개 슬롯)과 1:1 매핑되어 직관적입니다.

### 2. 5가지 카드 실행 타입 (Action Types)
1. **문구 붙여넣기 (`PasteText`)**
   - 긴 줄바꿈 텍스트, Markdown, 코드 블록 원문을 저장하고 현재 커서 위치에 바로 붙여넣습니다.
   - 클립보드 교체 방식을 사용하며, 붙여넣기 후 기존 클립보드 내용을 자동 복원합니다.
   - 단축키 전송 모드로 **`Ctrl + V`** 또는 **`Ctrl + Shift + V`** 방식을 선택적으로 지원합니다.
2. **프로그램/파일 실행 (`LaunchFile`)**
   - 로컬 `.exe` 실행 파일, 문서 파일, 폴더 등을 Windows 기본 쉘 방식으로 실행합니다.
   - 실행 대상의 **아이콘 자동 추출 및 캐싱** 기능을 제공하여 UI에 자동으로 썸네일을 표시합니다.
3. **웹페이지 주소 열기 (`LaunchUrl`)**
   - 입력한 URL을 기본 브라우저로 실행합니다.
   - 프로토콜 주소 자동 보정 기능 (`example.com` 입력 시 `https://example.com`으로 자동 보정)을 포함합니다.
4. **음악/미디어 제어 (`MediaAction`)**
   - **System**: Windows 범용 미디어 키 동작 제어 (재생/일시정지, 이전/다음 곡, 음소거, 볼륨 증가/감소).
   - **Spotify API**: Spotify와 공식 API 연동을 통해 토큰 관리 및 정밀한 음악 컨트롤 기능을 제공합니다.
5. **터미널 명령어 실행 (`TerminalCommand`)**
   - Cmd 또는 PowerShell 환경을 선택하여 입력한 셸 명령어를 실행합니다.
   - 관리자 권한으로 실행 (`RunAsAdministrator`) 옵션을 지원합니다.

### 3. 강력한 편의 기능 및 디자인
- **카드/슬롯 이동 기능**: 카드 편집 창을 통해 이미 생성된 카드를 다른 빈 슬롯이나 다른 슬롯과 위치를 바꾸어 효율적으로 레이아웃을 배치할 수 있습니다.
- **비주얼 커스터마이징**: 각 카드 슬롯별 사용자 지정 이미지 설정 및 자동 썸네일 생성, Lucide 기반 투명 배경 기본 아이콘 및 텍스트 표시 모드를 선택할 수 있습니다.
- **자동 숨김 및 창 위치 복원**: 카드 실행 후 창을 자동으로 숨기는 설정(Auto Hide) 및 마지막 실행 시의 창 크기/위치 자동 복원.

### 4. 데이터 안전성 보강 (Safety & Backup)
- **자동 백업**: 카테고리, 카드, 설정 정보 등이 변경되면 자동으로 앱 데이터 디렉토리 외부에 ZIP 스냅샷을 생성합니다. (최근 10개의 백업 자동 유지 및 오래된 백업 자동 정리)
- **수동 백업 및 ZIP 복원**: 설정 화면에서 직접 즉시 백업할 수 있으며, 기존 백업 ZIP을 불러와 DB, 이미지, 썸네일, 아이콘 캐시를 완전 복원합니다. 복원 실행 전에 현재 상태를 안전하게 선백업합니다.
- **DB 오류 대응**: DB가 잠겨있거나 파일이 손상되었을 때 앱 크래시가 발생하는 것을 방지하며, 기존 DB 백업 후 새 DB 교체 및 UI를 통한 사용자 안내/복구 가이드를 제공합니다.

---

## 🛠️ 기술 스택 (Tech Stack)

- **Language & Runtime**: C# / .NET 10.0 Windows Desktop
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Database**: SQLite
- **Architecture**: Clean Architecture (Domain, UseCases, Infrastructure, ViewModels, Views)
- **External Interop**: Win32 API Interop (전역 단축키 훅, 키보드 입력 시뮬레이션, 미디어 키 제어), Spotify API

---

## 📁 디렉토리 구조 (Directory Structure)

본 프로젝트는 **Clean Architecture** 원칙에 입각하여 도메인 중심의 책임 분리와 의존성 방향을 준수하고 있습니다.

```text
src/DeckDeckDeck.App/
├── Domain/           # 핵심 비즈니스 규칙 및 엔티티 가드 (CategoryRules, SnippetRules 등)
├── UseCases/         # 어플리케이션 유스케이스 오케스트레이션 및 Ports 인터페이스 정의
├── Infrastructure/   # 외부 어댑터 구현 (Persistence DB, Platform API, Storage 파일 제어, Gateways 외부 연동)
├── ViewModels/       # UI 프레젠테이션 논리 (WPF MVVM ViewModels)
├── Views/            # WPF UI 정의 (XAML 및 Value Converters)
├── Models/           # 데이터 구조 및 모델 정의 (Snippet, AppSettings 등)
└── Composition/      # DI 및 팩토리 메서드를 처리하는 Composition Root (앱 구성의 진입점)
```

---

## 🚀 개발 및 빌드 안내

### 전제 조건
- .NET 10.0 SDK가 설치되어 있어야 합니다.

### 테스트 실행
아래 명령어를 사용하여 유닛 테스트 및 아키텍처 의존성 테스트를 수행할 수 있습니다.
```powershell
dotnet test
```

### Windows x64 단일 파일 배포 빌드
사용자가 별도의 설치 과정 없이 바로 더블클릭하여 실행할 수 있는 단일 패키지 `.exe` 파일을 빌드하려면 아래 스크립트를 실행합니다.
```powershell
.\scripts\publish-win-x64.cmd -SelfContained -SingleFile
```
빌드가 성공하면 최신 빌드가 `artifacts/publish/win-x64/DeckDeckDeck.App.exe` 경로에 생성됩니다.

---

## 📄 관련 문서 (Documentation)

- **제품 요구사항 명세서 (PRD)**: [PRD.md](deckdeckdeck/docs/PRD.md)
- **디자인 가이드라인**: [DESIGN_GUIDELINES.md](deckdeckdeck/docs/DESIGN_GUIDELINES.md)
- **클린 아키텍처 규칙**: [clean-architecture.mini.md](docs/clean-architecture.mini.md)
- **소프트웨어 설계 철학**: [a-philosophy-of-software-design.mini.md](docs/a-philosophy-of-software-design.mini.md)
- **에이전트 작업 지침**: [AGENTS.md](AGENTS.md)
