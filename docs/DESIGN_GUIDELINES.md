# DeckDeckDeck Visual System Guidelines


---

## 0. Visual System 방향 요약

DeckDeckDeck의 시각 시스템은 다음 방향을 따른다.

```text
Main #FFF7D1 + Sub #A77743 + Accent #FFB15E
+ Soft Card-Keycap UI
+ User-provided DeckDuck Assets
```

MVP에서는 상단의 기본 3단계 컬러 팔레트 하나만 먼저 구현한다.  
보조 팔레트는 추후 확장 후보로만 둔다.

---

## 1. 컬러 시스템 원칙

### 1.1 기본 방향

기본 컬러 방향은 다음이다.

> **Main #FFF7D1 + Sub #A77743 + Accent #FFB15E**

이 조합은 DeckDeckDeck의 “살짝 귀여운 카드 도구” 이미지를 유지하면서도, 지나치게 유아적으로 보이지 않도록 따뜻한 배경색과 안정적인 브라운 계열을 함께 사용한다.

Accent 컬러는 버튼, 선택 상태, 중요한 행동처럼 사용자가 바로 알아야 하는 지점에만 제한적으로 사용한다.

---

### 1.2 컬러 사용 우선순위

| 우선순위 | 역할 | 설명 |
|---:|---|---|
| 1 | **가독성** | 텍스트와 버튼 상태가 먼저 명확해야 한다 |
| 2 | **카드 구분** | 덱/카드/빈 카드 상태가 한눈에 보여야 한다 |
| 3 | **산뜻함** | 밝고 부드러운 파스텔 인상을 유지한다 |
| 4 | **브랜드성** | Main, Sub, Accent의 반복으로 DeckDeckDeck다움을 만든다 |
| 5 | **장식성** | 귀여움은 기능을 방해하지 않는 선에서만 사용한다 |

---

## 2. Primary Palette

### 2.1 Default Palette

MVP의 기본 팔레트는 아래 3단계 컬러 조합 하나를 기준으로 한다.

| Role | Hex | 용도 |
|---|---:|---|
| Main | `#FFF7D1` | 앱 전체 배경, 부드러운 기본 면 |
| Sub | `#A77743` | 기본 텍스트, 라벨, 안정적인 강조 |
| Accent | `#FFB15E` | 주요 버튼, 선택 상태, 핵심 포인트 |

---

## 3. Secondary Palette Options

아래 팔레트들은 추후 확장을 위한 보조 후보이다.  
현재 MVP에서는 구현하지 않고, 상단의 기본 3단계 팔레트 하나만 먼저 구현한다.

| 후보 | Main | Sub | Accent |
|---|---:|---:|---:|
| Mint + Cream | `#F1FFF8` | `#A7E8CC` | `#57BFA8` |
| Sky + Lavender | `#F7FAFF` | `#A8D8FF` | `#D8C8FF` |
| Peach + Cream | `#FFF7EF` | `#8A5A44` | `#FFC6A8` |
| Lavender + Mint | `#F8F7FF` | `#34445A` | `#BEEAD8` |

---

## 4. Theme Policy

### 4.1 MVP 정책

MVP에서는 **기본 3단계 팔레트 하나만 구현**한다.

| 항목 | 정책 |
|---|---|
| MVP 기본 팔레트 | Main `#FFF7D1`, Sub `#A77743`, Accent `#FFB15E` |
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
| Card radius | `14px ~ 18px` |
| Card padding | `12px ~ 16px` |
| Card gap | `10px ~ 14px` |
| Border width | `1px` |
| Focus ring width | `2px` |
| Default elevation | 약한 그림자 |
| Pressed offset | `1px ~ 2px` |
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
- 배경은 `bg.surface` 또는 `brand.primary` 계열의 낮은 강조색을 사용한다.
- 테두리는 약하게 둔다.
- 그림자는 얕게 사용한다.

#### Hover

- 카드가 살짝 떠오르는 느낌을 준다.
- 테두리 또는 배경을 한 단계 강조한다.
- 텍스트 대비는 유지한다.
- 과한 애니메이션은 피한다.

#### Pressed

- 실제 키를 누른 듯한 피드백을 준다.
- 그림자를 줄이거나 y축으로 `1px ~ 2px` 이동한다.
- 즉각적인 반응이 중요하다.
- 실행 성공 여부는 별도 토스트나 상태 메시지로 표시한다.

#### Focused

- 키보드 사용자를 위해 가장 명확해야 하는 상태다.
- `focus.ring` 컬러를 사용한다.
- focus ring은 카드 바깥쪽에 표시해 내부 콘텐츠를 가리지 않는다.
- hover보다 focused가 더 우선되어야 한다.

#### Empty

- 빈 카드는 “비어 있음”보다 “추가 가능함”으로 보여야 한다.
- 문구는 **카드 추가**를 사용한다.
- 배경은 더 연하고 부드럽게 둔다.
- 점선 테두리 또는 `+` 아이콘을 사용할 수 있다.
- 빈 상태 전체 화면에서는 DeckDuck placeholder를 사용할 수 있다.

#### Disabled

- 비활성 카드는 삭제된 것이 아니라 잠시 사용할 수 없는 상태로 보여야 한다.
- opacity를 낮추되 텍스트를 완전히 읽기 어렵게 만들지 않는다.
- 실행할 수 없다는 점이 명확해야 한다.
- 가능하다면 짧은 사유를 tooltip이나 보조 문구로 제공한다.

---

### 6.3 Card State Token Example

| 상태 | Background | Border | Text | Extra |
|---|---|---|---|---|
| Default | `bg.surface` | `line.soft` | `text.primary` | 약한 그림자 |
| Hover | `bg.surface.soft` | `brand.mint.strong` | `text.primary` | 살짝 상승 |
| Pressed | `brand.primary` | `brand.primary.hover` | `text.primary` | 눌림감 |
| Focused | `bg.surface` | `focus.ring` | `text.primary` | 2px ring |
| Empty | `#FFFDF4` | `line.soft` 점선 | `text.secondary` | `+ 카드 추가` |
| Disabled | `#F0EEE4` | `#DDD6C2` | `#9B9688` | 낮은 opacity |

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

### 7.2 타입 스케일 초안

| 역할 | 크기 | 굵기 |
|---|---:|---:|
| App title | `22px ~ 26px` | 700 |
| Section title | `18px ~ 20px` | 700 |
| Card title | `14px ~ 16px` | 600 |
| Body | `13px ~ 14px` | 400 |
| Caption | `11px ~ 12px` | 400 |
| Button | `13px ~ 14px` | 600 |

---

### 7.3 텍스트 원칙

- 카드 제목은 짧아야 한다.
- 긴 설명은 카드 안에 모두 넣지 않는다.
- 텐키 위치 인지를 방해하지 않게 텍스트 밀도를 낮춘다.
- 코드/Markdown은 편집 화면에서만 충분히 넓게 보여준다.
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

## 9. Visual MVP Summary

| 항목 | 결정 |
|---|---|
| 기본 팔레트 | Main `#FFF7D1`, Sub `#A77743`, Accent `#FFB15E` |
| 보조 팔레트 | 추후 확장 후보, MVP에서는 구현 제외 |
| MVP 테마 | 기본 3단계 팔레트 하나만 구현 |
| 카드 형태 | Soft Keycap Card |
| 카드 상태 | Default, Hover, Pressed, Focused, Empty, Disabled |
| Editing 상태 | MVP 디자인 가이드에서는 제외 |
| Success/Error 상태 | 카드보다 토스트/문구 중심으로 처리 |
| DeckDuck 이미지 | 직접 생성하지 않음 |
| DeckDuck 처리 | 사용자가 추후 직접 넣을 수 있도록 asset card 위치만 정의 |
| 다음 보강 대상 | Component Guidelines, Screen Guidelines, WPF Resource Token 정리 |
