param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path $repoRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "The local .NET SDK is missing at $dotnet"
}

$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\release'))
if (-not $releaseRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Resolved release directory escaped the repository: $releaseRoot"
}
if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

$appDirectory = Join-Path $releaseRoot 'app'
$packageDirectory = Join-Path $releaseRoot 'packages'
New-Item -ItemType Directory -Force -Path $appDirectory, $packageDirectory | Out-Null

& $dotnet test (Join-Path $repoRoot 'TasksList.sln') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }

& $dotnet publish (Join-Path $repoRoot 'src\TasksList.App\TasksList.App.csproj') `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -o $appDirectory
if ($LASTEXITCODE -ne 0) { throw 'Task''sList application publish failed.' }

$plugins = @(
    @{ Project = 'plugins\TasksList.Plugin.BrowserContext\TasksList.Plugin.BrowserContext.csproj'; Id = 'taskslist.browser-context' },
    @{ Project = 'plugins\TasksList.Plugin.DeveloperWorkspace\TasksList.Plugin.DeveloperWorkspace.csproj'; Id = 'taskslist.developer-workspace' },
    @{ Project = 'plugins\TasksList.Plugin.CaptureWorkflows\TasksList.Plugin.CaptureWorkflows.csproj'; Id = 'taskslist.capture-workflows' }
)

foreach ($plugin in $plugins) {
    $pluginDirectory = Join-Path $appDirectory (Join-Path 'plugins' $plugin.Id)
    New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
    & $dotnet publish (Join-Path $repoRoot $plugin.Project) `
        -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -o $pluginDirectory
    if ($LASTEXITCODE -ne 0) { throw "Plugin publish failed: $($plugin.Id)" }

    $temporaryZip = Join-Path $packageDirectory "$($plugin.Id).zip"
    $packagePath = Join-Path $packageDirectory "$($plugin.Id).taskplugin"
    Remove-Item -LiteralPath $temporaryZip -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $packagePath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $pluginDirectory '*') -DestinationPath $temporaryZip -CompressionLevel Optimal
    Move-Item -LiteralPath $temporaryZip -Destination $packagePath
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'browser-extension') -Destination (Join-Path $appDirectory 'browser-extension') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'themes') -Destination (Join-Path $appDirectory 'themes') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\install.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\uninstall.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination $releaseRoot

$requiredReleaseFiles = @(
    'app\TasksList.App.exe',
    'app\themes\default\theme.json',
    'app\browser-extension\manifest.json',
    'app\plugins\taskslist.browser-context\plugin.json',
    'app\plugins\taskslist.developer-workspace\plugin.json',
    'app\plugins\taskslist.capture-workflows\plugin.json',
    'packages\taskslist.browser-context.taskplugin',
    'packages\taskslist.developer-workspace.taskplugin',
    'packages\taskslist.capture-workflows.taskplugin',
    'install.ps1',
    'uninstall.ps1'
)
foreach ($relativePath in $requiredReleaseFiles) {
    $resolvedPath = Join-Path $releaseRoot $relativePath
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "Release validation failed; missing $relativePath"
    }
}

$metadata = [ordered]@{
    product = "Task'sList"
    version = '1.1.0'
    runtime = 'win-x64'
    selfContained = $true
    createdAt = [DateTimeOffset]::UtcNow.ToString('O')
    gitCommit = (git -C $repoRoot rev-parse HEAD).Trim()
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'release.json') -Encoding utf8

$releaseUri = New-Object System.Uri(($releaseRoot.TrimEnd('\') + '\'))
$checksumLines = Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    $fileUri = New-Object System.Uri($_.FullName)
    $relative = [System.Uri]::UnescapeDataString($releaseUri.MakeRelativeUri($fileUri).ToString()).Replace('/', '\')
    "$($hash.Hash.ToLowerInvariant())  $relative"
}
$checksumLines | Set-Content -LiteralPath (Join-Path $releaseRoot 'checksums.sha256') -Encoding utf8

Write-Host "Task'sList release created at $releaseRoot"
