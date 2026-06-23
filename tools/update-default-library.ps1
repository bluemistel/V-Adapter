<#
.SYNOPSIS
  現在の作業用ライブラリ (%APPDATA%\V-Adapter\library.json) を、
  リリースに同梱する既定シード (src/VAdapter.App/Resources/default-library.json) へ反映します。

.DESCRIPTION
  アプリ上で組み込み用マクロ・対象アプリ・ショートカット等を編集したあと、
  本スクリプトを実行してから Release ビルド／発行を行うと、その設定が
  新規インストール時の初期状態として配布物に含まれます。

  ※ 既定シードは「初回起動（ライブラリが空）」のときだけ読み込まれます。
     既存ユーザーの library.json は上書きされません（ユーザーデータ保護）。

.EXAMPLE
  pwsh tools/update-default-library.ps1
#>

$ErrorActionPreference = 'Stop'

$source = Join-Path $env:APPDATA 'V-Adapter\library.json'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $repoRoot 'src\VAdapter.App\Resources\default-library.json'

if (-not (Test-Path $source)) {
    Write-Error "作業用ライブラリが見つかりません: $source`nアプリを一度起動・設定してから実行してください。"
    exit 1
}

# JSON として妥当か検証してからコピー（壊れたファイルを同梱しない）。
try {
    Get-Content -Raw -Encoding UTF8 $source | ConvertFrom-Json | Out-Null
} catch {
    Write-Error "library.json の JSON 解析に失敗しました。中断します。`n$_"
    exit 1
}

Copy-Item -Force $source $dest

$macroCount  = ((Get-Content -Raw -Encoding UTF8 $dest | ConvertFrom-Json).macros).Count
$targetCount = ((Get-Content -Raw -Encoding UTF8 $dest | ConvertFrom-Json).targets).Count

Write-Host "既定シードを更新しました:" -ForegroundColor Green
Write-Host "  from: $source"
Write-Host "  to  : $dest"
Write-Host "  マクロ: $macroCount 件 / 対象アプリ: $targetCount 件"
Write-Host ""
Write-Host "次に Release を発行してください:" -ForegroundColor Cyan
Write-Host "  dotnet publish src/VAdapter.App -p:PublishProfile=win-x64"
