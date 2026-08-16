<#
.SYNOPSIS
  V-Adapter のリリース用パッケージ（self-contained 単一 exe + 同梱ファイル）を作成する。

.DESCRIPTION
  ビルド成果物の置き場所を 1 箇所に固定するためのスクリプト。
  手動で dotnet publish を叩くと出力先がぶれるため、リリース作成は必ずこれを使う。

  出力先の役割（この 3 つ以外にビルド成果物を作らないこと）:
    src/VAdapter.App/bin/Debug/<TFM>/          … デバッグビルド（Visual Studio / F5）
    src/VAdapter.App/bin/Release/              … リリースビルドと publish の中間出力（自動生成・触らない）
    dist/                                      … 配布物のみ（zip・リリースノート）

.PARAMETER Version
  パッケージのバージョン。省略時は VAdapter.App.csproj の <Version> を使う。

.PARAMETER SkipZip
  zip を作らずステージングフォルダの作成までで止める。

.EXAMPLE
  pwsh -File tools/pack-release.ps1
  → dist/V-Adapter v0.0.5/ と dist/V-Adapter v0.0.5.zip を作成
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent $PSScriptRoot
$AppProj   = Join-Path $RepoRoot 'src/VAdapter.App/VAdapter.App.csproj'
$PublishIn = Join-Path $RepoRoot 'src/VAdapter.App/bin/Release/publish/win-x64'
$DistDir   = Join-Path $RepoRoot 'dist'

if (-not (Test-Path $AppProj)) { throw "プロジェクトが見つかりません: $AppProj" }

# --- バージョン決定（未指定なら csproj から） ---
if (-not $Version) {
    $Version = ([xml](Get-Content $AppProj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { throw 'csproj から <Version> を取得できませんでした。-Version で指定してください。' }
}
Write-Host "Version: $Version" -ForegroundColor Cyan

# --- 起動中インスタンスがリポジトリ内の出力を掴んでいるとビルドが失敗する ---
# （C:\tools\V-Adapter などリポジトリ外で動かしている分にはロックされないので止めない）
$running = Get-Process -Name 'V-Adapter', 'VAdapter.App' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($RepoRoot, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
    throw ("V-Adapter がリポジトリ内から起動中です。終了してから再実行してください: " +
           (($running | ForEach-Object { $_.Path }) -join ', '))
}

# --- publish（毎回クリーンにして古いファイルの混入を防ぐ） ---
if (Test-Path $PublishIn) { Remove-Item -Recurse -Force $PublishIn }
Write-Host 'publish 中...' -ForegroundColor Cyan
dotnet publish $AppProj -c Release -p:PublishProfile=win-x64 --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish に失敗しました (exit $LASTEXITCODE)" }
if (-not (Test-Path (Join-Path $PublishIn 'VAdapter.App.exe'))) {
    throw "publish 出力が見つかりません: $PublishIn"
}

# --- ステージング（配布物に入れるものだけを選んで配置） ---
$StageName = "V-Adapter v$Version"
$Stage     = Join-Path $DistDir $StageName
if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Path (Join-Path $Stage 'script/built-in')   -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Stage 'script/user-script') -Force | Out-Null

# 本体（配布時の名前は V-Adapter.exe）。pdb は同梱しない。
Copy-Item (Join-Path $PublishIn 'VAdapter.App.exe') (Join-Path $Stage 'V-Adapter.exe')
Copy-Item (Join-Path $PublishIn 'script/built-in/*.vamacro')   (Join-Path $Stage 'script/built-in')
Copy-Item (Join-Path $PublishIn 'script/user-script/README.txt') (Join-Path $Stage 'script/user-script')

Copy-Item (Join-Path $RepoRoot 'CHANGELOG.md') (Join-Path $Stage 'CHANGELOG.md')
Copy-Item (Join-Path $RepoRoot 'LICENSE')      (Join-Path $Stage 'LICENSE.txt')

# 配布用 README（雛形の {VERSION} を差し替え）
$readme = Get-Content (Join-Path $PSScriptRoot 'release-readme.txt') -Raw -Encoding UTF8
$readme = $readme.Replace('{VERSION}', $Version)
Set-Content -Path (Join-Path $Stage 'README.txt') -Value $readme -Encoding UTF8 -NoNewline

# library.json / integration.json は初回起動時に自動生成されるため同梱しない。

# --- zip（日本語ファイル名を UTF-8 で格納するため .NET API を使う） ---
if (-not $SkipZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $Zip = Join-Path $DistDir "$StageName.zip"
    if (Test-Path $Zip) { Remove-Item -Force $Zip }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $Stage, $Zip, [System.IO.Compression.CompressionLevel]::Optimal, $true, [System.Text.Encoding]::UTF8)
    Write-Host "zip: $Zip ($('{0:N1}' -f ((Get-Item $Zip).Length / 1MB)) MB)" -ForegroundColor Green
}

Write-Host "完了: $Stage" -ForegroundColor Green
Get-ChildItem $Stage -Recurse -File | ForEach-Object {
    '  {0}' -f $_.FullName.Substring($Stage.Length + 1)
}
