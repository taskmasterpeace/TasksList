param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\TasksList')
)

$ErrorActionPreference = 'Stop'
$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
if (-not $InstallRoot.StartsWith([System.IO.Path]::GetFullPath($env:LOCALAPPDATA), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to uninstall outside LocalAppData: $InstallRoot"
}

Get-Process -Name 'TasksList.App' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Task''sList') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.taskslist.browser_context' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.taskslist.browser_context' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TasksList' -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TasksList' -Force -ErrorAction SilentlyContinue

$currentProcess = $PID
$escapedRoot = $InstallRoot.Replace("'", "''")
$cleanup = "Wait-Process -Id $currentProcess -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '$escapedRoot' -Recurse -Force"
Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-WindowStyle', 'Hidden', '-Command', $cleanup -WindowStyle Hidden

Write-Host "Task'sList was removed. User notes and clipboard data remain in LocalAppData\TasksList."
