# Windows x64 exe 만들기

이 문서는 WPF 앱을 Windows x64용 exe로 만드는 방법입니다.

## 준비물

- 이 프로젝트의 대상 프레임워크에 맞는 .NET SDK
- 현재 프로젝트 대상 프레임워크: `net10.0-windows`

SDK가 없으면 `dotnet publish`가 실행되지 않습니다. 런타임만 설치된 상태로는 exe를 만들 수 없습니다.

## 기본 빌드

레포 루트에서 아래 명령어를 실행하세요.

```powershell
.\scripts\publish-win-x64.cmd
```

빌드가 끝나면 exe는 아래 위치에 생깁니다.

```text
artifacts\publish\win-x64\DeckDeckDeck.App.exe
```

이 방식은 실행할 PC에 .NET 런타임이 설치되어 있어야 합니다.

## 런타임 포함 빌드

다른 PC에서 .NET 설치 없이 실행하고 싶으면 아래처럼 실행하세요.

```powershell
.\scripts\publish-win-x64.cmd -SelfContained
```

파일 크기는 커지지만, 실행하는 PC에 .NET 런타임을 따로 설치할 필요가 줄어듭니다.

## 단일 파일 exe

파일을 하나의 exe에 최대한 묶고 싶으면 아래처럼 실행하세요.

```powershell
.\scripts\publish-win-x64.cmd -SelfContained -SingleFile
```

WPF 앱은 일부 파일이 실행 시 풀릴 수 있습니다. 그래도 사용자는 보통 exe 하나를 실행하면 됩니다.

## PowerShell 스크립트를 직접 실행하고 싶을 때

Windows 실행 정책 때문에 `.ps1` 파일이 바로 실행되지 않을 수 있습니다.
그럴 때는 위의 `.cmd` 파일을 쓰면 됩니다.
