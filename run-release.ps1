[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSCommandPath
$project = Join-Path $root 'src\SavorReader\SavorReader.csproj'
$tfm = 'net11.0-windows10.0.26100.0'
$rid = 'win-x64'
$output = Join-Path $root "src\SavorReader\bin\Release\$tfm\$rid"
$destination = Join-Path $env:USERPROFILE 'App\SavorReader'
$running = Get-Process -Name SavorReader -ErrorAction SilentlyContinue

if ($running) {
    throw 'SavorReader 正在运行。请先关闭它，再执行 release 部署。'
}

if (-not $NoBuild) {
    & dotnet build $project -c Release --nologo --no-incremental
    if ($LASTEXITCODE -ne 0) {
        throw "Release 构建失败，退出码 $LASTEXITCODE。"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $output 'SavorReader.exe'))) {
    throw "找不到 Release 输出：$output\SavorReader.exe"
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null

# Keep the user's Data directory across deployments; replace everything else.
Get-ChildItem -LiteralPath $destination -Force |
    Where-Object { $_.Name -ne 'Data' } |
    Remove-Item -Recurse -Force

Get-ChildItem -LiteralPath $output -Force |
    Where-Object { $_.Name -ne 'Data' } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }

$exe = Join-Path $destination 'SavorReader.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "部署后找不到可执行文件：$exe"
}

Write-Host "已部署到 $destination"
Write-Host "数据目录：$(Join-Path $destination 'Data')"

if (-not $NoLaunch) {
    Start-Process -FilePath $exe -WorkingDirectory $destination
    Write-Host 'SavorReader 已启动。'
}
