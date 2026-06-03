# DeckDeckDeck

Windows용 개인 스트림덱 기능을 모방한 런처입니다.

## 목표

텐키 기반 UI를 사용해 저장된 LLM 프롬프트/문구를 빠르게 선택하고, 현재 커서 위치에 붙여넣는 데스크톱 앱을 만드는 것이 목표입니다.

## MVP 핵심 기능

- Ctrl + Numpad 0으로 홈 화면 열기
- Ctrl + Numpad 1~9로 카테고리 바로 열기
- 실제 텐키 모양의 카테고리/문구 슬롯 UI
- 카테고리 추가/수정/삭제
- 문구 추가/수정/삭제
- 긴 Markdown/코드블록 텍스트 저장
- 현재 커서 위치에 붙여넣기
- 기존 클립보드 백업 및 복원
- 이미지 선택 및 썸네일 표시
- 로컬 설정 저장

## 기술 스택

- C#
- .NET 10 LTS
- WPF
- SQLite
- Win32 API interop

## 문서

- 제품 요구사항: `PRD.md`
- Codex 작업 지침: `AGENTS.md`
- MVP 작업 목록: `docs/tasks/MVP_BACKLOG.md`
