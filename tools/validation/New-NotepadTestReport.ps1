[CmdletBinding()]
param([string]$OutputPath = '.\TestResults\Notepad-validation.json')
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This report requires Windows. No application test has been run.'
}
if (Test-Path -LiteralPath $OutputPath) { throw 'Report already exists; choose a new path to preserve results.' }
$windows = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$version = $null
$versionStatus = 'unavailable'
# Inspect only binary version metadata, never window titles, documents or text.
$notepad = Get-Process -Name notepad -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $notepad) {
    try {
        $version = $notepad.MainModule.FileVersionInfo.FileVersion
        $versionStatus = if ($version) { 'observed' } else { 'unavailable' }
    } catch { $versionStatus = 'access-denied-or-process-exited' }
}
$commit = $null
if (Get-Command git -ErrorAction SilentlyContinue) {
    $candidate = & git rev-parse HEAD 2>$null
    if ($LASTEXITCODE -eq 0) { $commit = $candidate }
}
$caseNames = @(
    'activation', 'bounded-context', 'selection-and-caret', 'verified-replacement',
    'whole-document-integrity', 'duplicate-apply', 'expired-capture', 'changed-text',
    'caret-move-and-return', 'focus-switch-and-return', 'selected-text', 'empty-document',
    'multiline-unicode', 'composition', 'pause-after-capture', 'sensitive-scope',
    'denied-edit-session', 'close-and-reopen', 'registration-cleanup'
)
$report = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    commit = $commit
    environment = [ordered]@{
        windowsProductName = $windows.ProductName
        windowsEditionId = $windows.EditionID
        windowsDisplayVersion = $windows.DisplayVersion
        windowsBuild = "$($windows.CurrentBuildNumber).$($windows.UBR)"
        osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        application = 'Notepad'
        applicationVersion = $version
        applicationVersionStatus = $versionStatus
        applicationArchitecture = 'unverified'
        sessionType = 'unverified'
        dpiAndMonitorConfiguration = 'unverified'
    }
    verificationStatus = 'not-run'
    cases = @($caseNames | ForEach-Object {
        [ordered]@{ id = $_; result = 'not-run'; requestHresult = $null; sessionHresult = $null; statusCode = $null }
    })
    scope = 'Synthetic TSF experiment only; no prediction, IPC or popup verification.'
}
$fullPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullPath)) | Out-Null
$json = $report | ConvertTo-Json -Depth 8
# CreateNew prevents accidental overwrites even if another process creates the file.
$stream = [IO.File]::Open($fullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream.Write($bytes, 0, $bytes.Length)
} finally { $stream.Dispose() }
Write-Output 'Environment template created. Every application test remains not-run until actually performed.'
