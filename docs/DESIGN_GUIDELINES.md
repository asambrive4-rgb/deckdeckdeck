# DeckDeckDeck Visual System Guidelines


---

## 0. Visual System 방향 요약

DeckDeckDeck의 시각 시스템은 **Bibata-Warm-Ivory (Warm Beige & Bold Black Border)** 방향을 따른다.

```text
Main #F2EEE7 + Surface #FCFAF6 + Accent #D97448 + Border #1E1B18 (Bold 2.5px)
+ Bibata Modern Squircle / Pill Curve UI
+ User-provided DeckDuck Assets
```

Bibata Warm Ivory 마우스 커서의 조형미(포근한 베이지 바디 + 선명한 검정 외곽선)를 UI에 직접 반영하여, 부담 없는 맑고 따뜻한 중립 아이보리 베이스와 또렷하고 볼드한 검정 테두리(2.5px), 알약(Pill)/조약돌을 연상시키는 18~20px의 둥근 모서리가 어우러진 촉각적(Tactile) 디자인을 제공한다.

---

## 1. 컬러 시스템 원칙

### 1.1 기본 방향

기본 컬러 방향은 다음이다.

> **Main #F2EEE7 + Surface #FCFAF6 + Border #1E1B18 (Text #1E1B18, Accent #D97448)**

이 조합은 Bibata 마우스 커서와 완벽한 일체감을 제공하며, 눈부심 없는 부드러운 베이지 면과 또렷한 검정 외곽선으로 카드와 버튼의 경계를 명확하게 구분해 줍니다.

---

### 1.2 컬러 사용 우선순위

| 우선순위 | 역할 | 설명 |
|---:|---|---|
| 1 | **가독성 & 경계** | 2.5px 검정(#1E1B18) 볼드 테두리와 텍스트로 카드와 조작 영역이 선명해야 한다 |
| 2 | **포근한 베이지감** | 차분하고 맑은 중립 아이보리/린넨 베이스(#F2EEE7)로 눈의 피로를 방지한다 |
| 3 | **Bibata 일체감** | 실제 Bibata Warm Ivory 커서(베이지 바디 + 검정 외곽선)와 동일한 미감을 전달한다 |
| 4 | **브랜드성** | Warm Beige 베이스 + 웜 테라코타 포인트로 세련된 레트로/모던 감성을 만든다 |
| 5 | **촉각성 (Tactile)** | 18px 알약형 곡률과 명확한 눌림감으로 기분 좋은 인터랙션을 제공한다 |

---

## 2. Primary Palette (Bibata-Warm-Ivory)

### 2.1 Default Palette

기본 팔레트는 아래의 깊이 있는 Warm Beige & Bold Black Border 시스템을 기준으로 한다.

| Role | Hex | 용도 |
|---|---:|---|
| Main (App Background) | `#F2EEE7` | 앱 전체 배경, 차분하고 맑은 중립 아이보리 |
| Surface (Cards / Inputs) | `#FCFAF6` | 카드 및 입력창 표면, 부드러운 크림 아이보리 |
| Surface Soft (Hover) | `#E7E2D9` | 마우스 호버 시 짙어지는 베이지 표면 |
| Surface Muted (Pressed) | `#DBD4CB` | 클릭/눌림 및 비활성 영역 베이지 |
| Border (Bold Line) | `#1E1B18` / `#000000` | Bibata 시그니처 2.5px 검정 볼드 테두리 |
| Sub Text | `#5C554E` | 보조 텍스트, 설명 라벨 |
| Accent (Terracotta) | `#D97448` | 주요 CTA 버튼, 포커스 링, 핵심 포인트 |
| Text Primary | `#1E1B18` | 검정 테두리와 어울리는 또렷한 딥 블랙 텍스트 |

---

## 3. Secondary Palette Options

아래 팔레트들은 추후 확장을 위한 보조 후보이다.  
현재는 상단의 Bibata-Warm-Ivory 기본 팔레트 하나를 기준으로 운영한다.

| 후보 | Main | Sub | Accent |
|---|---:|---:|---:|
| Mint + Cream | `#F1FFF8` | `#A7E8CC` | `#57BFA8` |
| Sky + Lavender | `#F7FAFF` | `#A8D8FF` | `#D8C8FF` |
| Peach + Cream | `#FFF7EF` | `#8A5A44` | `#FFC6A8` |
| Lavender + Mint | `#F8F7FF` | `#34445A` | `#BEEAD8` |

---

## 4. Theme Policy

### 4.1 MVP 정책

현재 버전에서는 **Bibata-Warm-Ivory 기본 팔레트 하나만 구현**한다.

| 항목 | 정책 |
|---|---|
| MVP 기본 팔레트 | Main `#F7F4EE`, Sub `#6E6259`, Accent `#D97448`, Text `#2B2523` |
| 보조 팔레트 구현 | MVP에서는 제외 |
| 사용자 팔레트 선택 | 추후 고려 |

---

## 5. Card Shape

### 5.1 형태 방향

카드 형태는 **키캡 느낌의 버튼형 카드 + 둥근 사각형 카드**를 혼합한다.

즉, 완전한 키보드 키처럼 딱딱하지 않고,  
완전한 종이 카드처럼 장식적이지도 않은 형태를 사용한다.

> **Soft Keycap Card**

---

### 5.2 형태 원칙

| 요소 | 권장 방향 |
|---|---|
| 모서리 | 둥근 사각형, 부드러운 radius |
| 그림자 | 약한 그림자 또는 얕은 elevation |
| 눌림감 | Pressed 상태에서 살짝 내려가는 느낌 |
| 테두리 | 기본은 약하게, hover/focus에서 명확하게 |
| 내부 여백 | 키캡보다 넉넉하게, 카드보다 단단하게 |
| 비율 | 텐키 카드처럼 안정적인 사각/세로형 타일 |
| 텍스트 | 짧은 제목 중심, 보조 설명은 선택 |

---

### 5.3 권장 수치 초안

아래 수치는 WPF UI 구현 시 참고용 기준이다.

| 항목 | 권장값 |
|---|---:|
| Window frame radius | **`7px` (Windows 11 DWM 네이티브 모서리 1:1 밀착 & 잔여 틈새 차단)** |
| Card radius | `18px ~ 20px` (Bibata 조약돌 스쿼클) |
| Control/Button radius | `18px` (알약/Pill형 둥글기) |
| Card padding | `12px ~ 16px` |
| Card gap | `10px ~ 14px` |
| Border width | **`2.5px` (선명하고 볼드한 검정 테두리)** |
| Focus ring width | `3px` |
| Elevation | 2.5px 검정 라인 기본 + 최상위 패널 은은한 섀도우 |
| Pressed offset | `2.5px` |
| Minimum touch/click area | `64px x 64px` 이상 |

---

## 6. Card States

### 6.1 MVP 포함 상태

MVP 디자인 가이드에는 다음 상태를 포함한다.

| 상태 | 포함 여부 | 목적 |
|---|---|---|
| Default | 포함 | 기본 카드/덱 표시 |
| Hover | 포함 | 마우스 사용 시 선택 가능성 표시 |
| Pressed | 포함 | 클릭/키 입력 시 눌림감 표시 |
| Focused | 포함 | 키보드 조작 시 현재 위치 표시 |
| Empty | 포함 | 새 카드/덱 추가 가능성 표시 |
| Disabled | 포함 | 비활성 카드/덱 표시 |
| Editing | 제외 | 추후 확장 |
| Success | 제외 | 토스트/상태 메시지 중심 |
| Error | 제외 | 토스트/오류 문구 중심 |

---

### 6.2 State Guidelines

#### Default

- 카드 제목이 가장 먼저 보여야 한다.
- 배경은 `bg.surface` (`#FCFAF6`)을 기본으로 사용한다.
- 테두리는 2.5px 볼드 검정 `line.soft` (`#1E1B18`)로 선명하게 두른다.

#### Hover

- 카드가 살짝 떠오르거나 강조되는 느낌을 준다.
- 테두리를 순수 블랙(`line.strong` `#000000`)으로 유지하고 배경을 짙은 베이지(`bg.surface.soft` `#E7E2D9`)로 바꾼다.
- 텍스트 대비는 딥 블랙(`#1E1B18`)으로 유지한다.

#### Pressed

- 실제 키를 누른 듯한 촉각적(Tactile) 피드백을 준다.
- y축으로 `2.5px` 하향 이동(오프셋 마진)하고 배경을 `bg.surface.muted` (`#DBD4CB`)로 전환한다.

#### Focused

- 키보드 사용자를 위해 가장 명확해야 하는 상태다.
- `focus.ring` (웜 테라코타 `#D97448`) 컬러를 사용한다.
- 3px ring을 카드 바깥쪽에 표시한다.

#### Empty

- 빈 카드는 “비어 있음”보다 “추가 가능함”으로 보여야 한다.
- 문구는 **카드 추가**를 사용한다.
- 배경은 차분한 오트밀 베이지(`bg.empty` `#EAE5DC`)를 둔다.
- 점선 또는 실선 2.5px 검정 테두리를 사용한다.

#### Disabled

- 비활성 카드는 삭제된 것이 아니라 잠시 사용할 수 없는 상태로 보여야 한다.
- opacity를 낮추되 텍스트를 완전히 읽기 어렵게 만들지 않는다.

---

### 6.3 Card State Token Example

| 상태 | Background | Border | Text | Extra |
|---|---|---|---|---|
| Default | `bg.surface` (`#FCFAF6`) | `line.soft` (`#1E1B18`) | `text.primary` (`#1E1B18`) | 2.5px 볼드 검정 라인 |
| Hover | `bg.surface.soft` (`#E7E2D9`) | `line.strong` (`#000000`) | `text.primary` (`#1E1B18`) | 짙은 베이지 피드백 |
| Pressed | `bg.surface.muted` (`#DBD4CB`) | `line.strong` (`#000000`) | `text.primary` (`#1E1B18`) | 2.5px 눌림감 |
| Focused | `bg.surface` (`#FCFAF6`) | `focus.ring` (`#D97448`) | `text.primary` (`#1E1B18`) | 3px ring |
| Empty | `bg.empty` (`#EAE5DC`) | `line.soft` (`#1E1B18`) | `text.secondary` (`#5C554E`) | `+ 카드 추가` |
| Disabled | `#E7E2D9` | `#8C8278` | `#8C8278` | 낮은 opacity |

---

## 7. Typography Direction

### 7.1 기본 방향

폰트는 운영체제 기본 UI와 잘 어울리는 sans-serif 계열을 사용한다.

| 용도 | 권장 |
|---|---|
| Windows 기본 UI | `Segoe UI` |
| 한국어 보조 | `Malgun Gothic`, `Pretendard` 가능 |
| 코드/Markdown 미리보기 | `Cascadia Code`, `Consolas` |
| 문서/README | 시스템 기본 sans-serif |

---

### 7.2 타입 스케일 (Bold & Clear Typography)

Bibata-Warm-Ivory 시스템에서는 얇은 글자(400 Regular/Normal) 사용을 지양하고, 선명한 볼드 테두리와 균형을 이루는 도톰하고 또렷한 가중치를 기본으로 사용한다. 앱에 내장된 `SUIT-SemiBold`(600) 원본 글리프를 1:1 매핑하여 WPF의 인위적 볼드 합성(Faux Bold) 왜곡을 방지한다.

| 역할 | 크기 | 굵기 (Weight) | 색상 |
|---|---:|---:|---|
| App / Page title | `22px ~ 26px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |
| TopBar title | `15px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |
| TopBar status | `12px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |
| Card title | `15px ~ 16px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |
| Slot Key | `13px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |
| Button | `13px ~ 14px` | **600 (SemiBold)** | `Text.Primary` / White |
| Body / Field label | `14px` | **600 (SemiBold)** | `Text.Primary` (`#1E1B18`) |

---

### 7.3 텍스트 및 렌더링 선명도 원칙

- **도톰한 볼드 타이포그래피**: 2.5px 검정 테두리와 어울리도록 얇은 폰트를 지양하고 `SemiBold`(600)를 기본 굵기로 통일한다.
- **픽셀 잘림 없는 서브픽셀 렌더링**: 정수 픽셀 강제 스냅으로 글자 획 끝이 잘리는 `Display` 모드 대신 `Ideal` 모드와 `ClearType`을 사용하여 서브픽셀 정밀도로 부드럽고 온전한 글자 형태를 유지한다.
- **줄 높이 여유 확보**: 고정 높이(`BlockLineHeight`)로 인한 초성/받침 클리핑을 방지하기 위해 `MaxHeight` 전략과 여유 있는 `LineHeight`(22px 이상)를 보장한다.
- **블러 없는 칼 같은 렌더링**: 전체 화면에 번짐을 유발하던 `BitmapCache` 래스터화와 외곽 섀도우 블러(`DropShadowEffect`)를 배제하고 깨끗한 2.5px 검정 라인으로 경계를 마감한다.
- 카드 제목은 짧고 직관적이어야 한다.
- 버튼 문구는 감성보다 기능 명확성을 우선한다.

---

## 8. DeckDuck Asset Cards

### 8.1 원칙

DeckDuck 이미지는 이 문서에서 직접 생성하지 않는다.  
사용자가 추후 직접 생성하거나 제작한 이미지를 넣을 수 있도록 **공간과 사용 규칙만 정의**한다.

---

### 8.2 권장 asset 구조

```text
assets/
  brand/
    deckduck/
      app-icon.png
      app-icon.ico
      empty-state.png
      onboarding.png
      helper-small.png
      success-small.png
      release-note.png
```

---

### 8.3 DeckDuck Placeholder Cards

| Card | 파일 예시 | 사용 위치 | 비고 |
|---|---|---|---|
| App Icon | `app-icon.ico` | Windows 앱 아이콘 | 가장 단순한 실루엣 권장 |
| App Icon Source | `app-icon.png` | 아이콘 원본 | 1024x1024 권장 |
| Empty State | `empty-state.png` | 덱/카드가 없을 때 | CTA를 방해하지 않아야 함 |
| Onboarding | `onboarding.png` | 첫 실행 안내 | 캐릭터성이 조금 더 허용됨 |
| Helper Small | `helper-small.png` | 도움말/작은 안내 | UI보다 작게 |
| Success Small | `success-small.png` | 성공 상태/토스트 근처 | 선택 사용 |
| Release Note | `release-note.png` | 릴리즈 노트 | 가장 위트 있는 표현 허용 |

---

### 8.4 이미지 삽입 위치 규칙

DeckDuck 이미지는 다음 위치에 넣을 수 있다.

#### 허용

- 빈 상태 화면의 한쪽
- 온보딩 카드 상단 또는 측면
- 설정 화면 하단의 작은 장식
- 릴리즈 노트 헤더
- 성공 상태의 작은 리액션
- 앱 아이콘

#### 피하기

- 실행 버튼 바로 옆
- 삭제/초기화 확인 화면의 중심
- 오류 메시지의 메인 이미지
- 카드 전체를 가리는 배경
- 카드 제목보다 더 눈에 띄는 위치

---

### 8.5 Placeholder 문구 예시

이미지가 아직 없을 때는 다음처럼 placeholder를 둘 수 있다.

```md
[DeckDuck image placeholder]
```

또는 UI 시안에서:

```text
DeckDuck illustration goes here
```

한국어 문서에서는:

```text
DeckDuck 이미지 자리
```

---

## 9. 반응형 및 가변 비율 레이아웃 원칙 (Responsive & Anti-Clipping Guidelines)

DeckDeckDeck은 고정된 뷰포트가 아닌, **창 크기 조절(Resize, 440px ~ 최대화), 화면 비율 변경, Windows DPI 배율(100% ~ 200%), 시스템 글꼴 크기 변경이 자유로운 가변형 데스크톱 앱**이다.  
따라서 모든 UI 설계와 XAML 코드는 **어떤 화면 크기와 비율에서도 요소가 뭉개지거나 잘리지 않는(Defensive & Anti-Clipping) 반응형 원칙**을 기본으로 준수해야 한다.

### 9.1 핵심 대전제
1. **유동적 가용 너비(Available Width)**: 창 크기 조절뿐 아니라 수직 스크롤바가 생기거나 사라질 때 컨텐츠 영역의 가용 너비(약 16~18px)가 변동됨을 항상 전제한다.
2. **DPI 및 글꼴 배율 확장성**: Windows 디스플레이 설정(125%, 150% 배율)이나 텍스트 크기 확대 시에도 레이아웃이 깨지지 않아야 한다.
3. **콘텐츠 길이 변화 수용**: 다국어 번역, 긴 텍스트 입력, 긴 버튼 레이블(예: 15글자 이상의 액션 버튼)에도 UI가 자연스럽게 늘어나야 한다.

---

### 9.2 XAML 레이아웃 & 코딩 규칙 (Anti-Clipping Rules)

#### 규칙 1. 고정 `Width` 금지 및 비례(`*`) / 가변(`Auto`) 레이아웃 사용
- **버튼이나 입력 컨트롤에 `Width="120"`과 같은 고정 수치를 하드코딩하지 않는다.**
- 다분할 버튼이나 키캡 카드는 `Grid`의 비례 컬럼(`ColumnDefinition Width="*"`)을 사용하여 부모 영역 내에서 균등하게 늘어나고 줄어들게 한다.
- 버튼 자체의 너비는 기본적으로 `Width="Auto"`를 유지하여 내용물(텍스트+패딩)의 길이에 맞게 자연스럽게 확장되도록 한다.

```xaml
<!-- 권장: Grid 비율 분할로 창 너비 변화에 유연 대응 -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="6" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="6" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <Button Grid.Column="0" Style="{StaticResource Deck.TerminalPresetButtonStyle}" ... />
</Grid>
```

#### 규칙 2. 고정 `Height` 대신 `MinHeight` 사용
- 버튼이나 카드에 고정 높이(`Height="58"`)를 부여하면 폰트 크기가 커지거나 텍스트가 두 줄이 될 때 위아래가 잘린다.
- 최소 터치/클릭 영역을 보장하기 위해 **반드시 `MinHeight`를 사용**하고, 내용물이 많아지면 아래로 자연스럽게 늘어날 수 있게 한다.

```xaml
<!-- 권장 -->
<Setter Property="MinHeight" Value="58" />

<!-- 지양: 글꼴 확대 시 위아래 텍스트 잘림 발생 -->
<Setter Property="Height" Value="58" />
```

#### 규칙 3. `StackPanel (Horizontal)` 내부 텍스트 래핑 주의 → `DockPanel` 또는 `Grid` 사용
- WPF의 `StackPanel Orientation="Horizontal"`은 가로 방향으로 자식 요소에게 무한한 공간(`Double.PositiveInfinity`)을 제공한다.
- 따라서 내부의 `TextBlock`에 `TextWrapping="Wrap"`을 지정해도 **부모 너비를 넘어 화면 밖으로 텍스트가 잘려나간다.**
- 아이콘 + 텍스트 제목처럼 가로로 배치되면서 우측 텍스트가 줄바꿈되어야 하는 복합 구조에는 반드시 **`DockPanel (LastChildFill="True")`** 또는 **`Grid (Auto, *)`**를 사용한다.

```xaml
<!-- 권장: 텍스트가 우측 경계에 맞춰 정상적으로 줄바꿈됨 -->
<DockPanel LastChildFill="True">
    <Path DockPanel.Dock="Left" Width="14" Margin="0,0,6,0" ... />
    <TextBlock FontWeight="SemiBold" TextWrapping="Wrap" Text="화면 끄기 모드가 적용되었습니다" />
</DockPanel>

<!-- 지양: 가로 무한 확장으로 인해 우측 끝 글자가 잘려나감 -->
<StackPanel Orientation="Horizontal">
    <Path Width="14" Margin="0,0,6,0" ... />
    <TextBlock TextWrapping="Wrap" Text="화면 끄기 모드가 적용되었습니다" />
</StackPanel>
```

#### 규칙 4. 모든 안내/설명 문장에는 `TextWrapping="Wrap"` 필수 적용
- 카드 내의 설명 문구, 에러 메시지, 불릿 안내문 등 1줄을 넘길 가능성이 있는 모든 `TextBlock`에는 반드시 `TextWrapping="Wrap"`을 명시한다.

#### 규칙 5. 단일 라인 타이틀에는 `TextTrimming="CharacterEllipsis"` 명시
- 버튼명, 슬롯명, 기기명 등 반드시 한 줄로 유지되어야 하는 라벨은 공간이 좁아지더라도 글자가 흉하게 쪼개지지 않고 말줄임표(`...`)로 마감되도록 `TextTrimming="CharacterEllipsis"`를 설정한다.

#### 규칙 6. 공용 디자인 시스템 스타일에는 컨테이너 종속적 크기 제약 금지
- `Theme.xaml`에 등록되는 기본 버튼이나 컴포넌트 스타일에 `Width="120"` 등의 고정 크기를 포함하지 않는다.
- 고정 너비가 필요하다면 공용 스타일이 아닌 특정 뷰의 인스턴스에서 명시하거나, `MinWidth` 형태로 최소 크기만 가이드한다.

#### 규칙 7. 좁은 뷰포트를 대비한 안전 패딩(Padding) 관리
- 3등분 키캡처럼 가로 폭이 좁은 영역에 들어가는 버튼은 좌우 패딩을 과도하게 주지 않는다(`Padding="2,6"` 또는 `Padding="4,6"` 권장). 패딩이 너무 크면 텍스트 영역이 지나치게 줄어들어 조기에 말줄임(`...`)이 발생한다.

---

### 9.3 반응형 검증 체크리스트
UI 코드를 작성하거나 수정한 후에는 다음 4가지 관점을 필히 검증한다:

- [ ] **최소 창 폭(440px) 검증**: 창 너비가 가장 좁을 때 버튼이나 카드의 우측 여백 밖으로 요소가 튀어나가지 않는가?
- [ ] **창 확대/최대화 검증**: 창을 넓혔을 때 어색하게 특정 크기에 고정되지 않고 시각적 균형에 맞게 확장되는가?
- [ ] **DPI 배율(125% ~ 150%) 검증**: Windows 디스플레이 확대 환경에서도 버튼 텍스트의 상하/좌우가 잘리지 않는가?
- [ ] **텍스트 래핑/말줄임 검증**: 긴 문장은 카드를 뚫고 나가지 않고 아래로 자동 줄바꿈되며, 키캡 버튼명은 말줄임(`...`)이 정상 작동하는가?

---

## 10. Visual MVP Summary

| 항목 | 결정 |
|---|---|
| 기본 팔레트 | Bibata-Warm-Ivory (Main `#F7F4EE`, Sub `#6E6259`, Accent `#D97448`, Text `#2B2523`) |
| 보조 팔레트 | 추후 확장 후보, MVP에서는 구현 제외 |
| MVP 테마 | Bibata-Warm-Ivory 단일 테마 구현 |
| 카드 형태 | Bibata Squircle / Soft Keycap Card (Radius 18~20px, Button 18px) |
| 카드 상태 | Default, Hover, Pressed, Focused, Empty, Disabled |
| 레이아웃 원칙 | **가변 비율 반응형 (고정 Width 지양, MinHeight 권장, DockPanel/Grid 기반 텍스트 래핑)** |
| Editing 상태 | MVP 디자인 가이드에서는 제외 |
| Success/Error 상태 | 카드보다 토스트/문구 중심으로 처리 |
| DeckDuck 이미지 | 직접 생성하지 않음 |
| DeckDuck 처리 | 사용자가 추후 직접 넣을 수 있도록 asset card 위치만 정의 |
| 다음 보강 대상 | Component Guidelines, Screen Guidelines, WPF Resource Token 정리 |
