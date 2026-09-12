$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishExe = Join-Path $Root "artifacts\publish\win-x64\DeckDeckDeck.App.exe"
$RootExe = Join-Path $Root "DeckDeckDeck.exe"

# 1. 기존 실행 중인 DeckDeckDeck 프로세스 안전 종료 및 파일 잠금 해제
Write-Host "기존 DeckDeckDeck 프로세스를 확인하고 안전하게 종료하는 중..."
cmd.exe /c "taskkill /F /IM DeckDeckDeck.exe /T >nul 2>nul & taskkill /F /IM DeckDeckDeck.App.exe /T >nul 2>nul & exit 0"
Start-Sleep -Milliseconds 600

# 2. 최신 빌드 실행 파일 복사 (빌드 결과물이 있는 경우 재시도 지원)
if (Test-Path $PublishExe) {
    Write-Host "최신 빌드 실행 파일을 레포 루트로 복사하는 중..."
    $copied = $false
    for ($i = 0; $i -lt 5; $i++) {
        try {
            Copy-Item $PublishExe $RootExe -Force -ErrorAction Stop
            $copied = $true
            break
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $copied) {
        Copy-Item $PublishExe $RootExe -Force
    }
}

# 3. 사용자의 실제 대화형 화면(WinSta0\Default 데스크톱)으로 앱 실행
if (Test-Path $RootExe) {
    Write-Host "DeckDeckDeck을 사용자의 화면 전면으로 실행합니다..."

    $taskName = "DeckDeckDeck_InteractiveLaunch"
    try {
        & schtasks /create /tn $taskName /tr "`"$RootExe`"" /sc once /st 00:00 /f /it | Out-Null
        & schtasks /run /tn $taskName | Out-Null
        Start-Sleep -Milliseconds 1500
        & schtasks /delete /tn $taskName /f | Out-Null
    } catch {
        # schtasks 실패 시 일반 프로세스 시작으로 Fallback
        Start-Process -FilePath $RootExe -WorkingDirectory $Root
    }

    $newProc = Get-Process -Name DeckDeckDeck, DeckDeckDeck.App -ErrorAction SilentlyContinue | Sort-Object StartTime -Descending | Select-Object -First 1
    if ($newProc) {
        Write-Host "DeckDeckDeck 실행 완료 (PID: $($newProc.Id), 상태: 사용자 화면에서 정상 동작 중)"
    } else {
        Write-Host "DeckDeckDeck 실행 명령 전달 완료"
    }
} else {
    Write-Error "실행 파일($RootExe)이 존재하지 않습니다. 먼저 publish-win-x64를 실행하세요."
}
