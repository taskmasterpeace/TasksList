param(
    [string]$SourceRoot,
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\TasksList'),
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $PSScriptRoot 'app'
}
$SourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot 'TasksList.App.exe'))) {
    throw "Task'sList release files were not found at $SourceRoot"
}
if (-not $InstallRoot.StartsWith([System.IO.Path]::GetFullPath($env:LOCALAPPDATA), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Per-user install path must remain inside LocalAppData: $InstallRoot"
}

Get-Process -Name 'TasksList.App' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
Copy-Item -Path (Join-Path $SourceRoot '*') -Destination $InstallRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $InstallRoot 'uninstall.ps1') -Force
$executablePath = Join-Path $InstallRoot 'TasksList.App.exe'

$shell = New-Object -ComObject WScript.Shell
$startMenuDirectory = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Task''sList'
New-Item -ItemType Directory -Force -Path $startMenuDirectory | Out-Null
$shortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'Task''sList.lnk'))
$shortcut.TargetPath = $executablePath
$shortcut.WorkingDirectory = $InstallRoot
$shortcut.Description = "Contextual sticky notes and unlimited clipboard history"
$shortcut.IconLocation = "$executablePath,0"
$shortcut.Save()

$browserHostExe = Join-Path $InstallRoot 'plugins\taskslist.browser-context\TasksList.Plugin.BrowserContext.exe'
$nativeManifestPath = Join-Path $InstallRoot 'com.taskslist.browser_context.json'
$nativeManifest = [ordered]@{
    name = 'com.taskslist.browser_context'
    description = "Task'sList Browser Context bridge"
    path = $browserHostExe
    type = 'stdio'
    allowed_origins = @('chrome-extension://fjgjagcnipdddcimgbohdpahbkakmnie/')
}
$nativeManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $nativeManifestPath -Encoding utf8

foreach ($registryPath in @(
    'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.taskslist.browser_context',
    'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.taskslist.browser_context'
)) {
    New-Item -Path $registryPath -Force | Out-Null
    Set-Item -Path $registryPath -Value $nativeManifestPath
}

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TasksList'
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name DisplayName -Value "Task'sList"
Set-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '1.2.0'
Set-ItemProperty -Path $uninstallKey -Name Publisher -Value "Task'sList"
Set-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $InstallRoot
$uninstallCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallRoot 'uninstall.ps1')`" -InstallRoot `"$InstallRoot`""
Set-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstallCommand
Set-ItemProperty -Path $uninstallKey -Name QuietUninstallString -Value "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$(Join-Path $InstallRoot 'uninstall.ps1')`" -InstallRoot `"$InstallRoot`""
Set-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value "$executablePath,0"
Set-ItemProperty -Path $uninstallKey -Name Comments -Value "Contextual sticky notes, unlimited clipboard history, capture workflows, and local plugins."
Set-ItemProperty -Path $uninstallKey -Name URLInfoAbout -Value 'https://github.com/taskmasterpeace/TasksList'
$estimatedSizeKb = [int][Math]::Ceiling(((Get-ChildItem -LiteralPath $InstallRoot -File -Recurse | Measure-Object -Property Length -Sum).Sum) / 1KB)
Set-ItemProperty -Path $uninstallKey -Name EstimatedSize -Value $estimatedSizeKb -Type DWord
Set-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -Type DWord

if (-not $NoLaunch) {
    Start-Process -FilePath $executablePath -WorkingDirectory $InstallRoot
}

Write-Host "Task'sList installed at $InstallRoot"
Write-Host "Browser companion files: $(Join-Path $InstallRoot 'browser-extension')"
