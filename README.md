<div align="center">

# V-Adapter

**合成音声ソフトの操作を、統一ショートカットから半自動化する Windows デスクトップ向けマクロツール**

[![version](https://img.shields.io/badge/version-0.0.1%20(alpha)-blue)](https://github.com/bluemistel/V-Adapter/releases)
[![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)](https://github.com/bluemistel/V-Adapter)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

</div>

> ⚠️ 本バージョンは **α版 (v0.0.1)** です。仕様は予告なく変更される場合があります。

VOICEVOX / A.I.VOICE2 / CeVIO AI / VOICEPEAK / VoiSona Talk … 合成音声ソフトは各社で UI・ショートカットがバラバラで、複数音源を併用すると操作を覚え直す負担が大きくなります。
**V-Adapter** は「音声の再生」「音声の保存」といった*ユーザー視点の操作*に統一ショートカットを割り当て、その内部を*アプリごとの簡易マクロ*として実行することで、操作体系を統一します。

---

## ✨ 特長

- **統一ショートカット** — マクロごとに任意のグローバルショートカットを割り当て（アプリ起動中に有効、編集中は自動で一時無効化）
- **アプリ別スクリプト** — 1つのマクロに対象アプリごとのスクリプト（命令列）を保持。実行時は前面のアプリを自動判定、または「送信先」で固定
- **堅牢なアプリ検出** — プロセス名優先＋ウィンドウクラス正規化で、起動ごとに変化するクラス名（WPF の GUID、JUCE、WinForms 等）にも追従
- **GUI 主導の設定** — 座標・OCR領域・ウィンドウクラスをクリック／ドラッグで取得
- **インポート / エクスポート** — マクロを `.vamacro` で配布・共有（対象アプリ設定を内包）
- **最前面ピン留め** / 実行ログ / 折りたたみヘルプ

### マクロ命令

| 命令 | 内容 |
|------|------|
| アプリ切替待ち | 対象アプリが前面（アクティブ）になるまで同期的に待機 |
| 待機 | 指定ミリ秒だけ待つ |
| クリック | ウィンドウ相対／ディスプレイ絶対座標をクリック。**基準位置（隅・中央）** 指定でウィンドウサイズ変化に追従 |
| キー送信 | 修飾キー込みのキー組み合わせを送信（スキャンコード送信で JUCE 系にも対応） |
| ウィンドウ表示待ち | 対象アプリのウィンドウが現れるまで待機 |
| ダイアログ表示待ち | Windows 標準ダイアログ（名前を付けて保存 等）を環境・サイズ非依存に検出 |
| 操作対象の切り替え | 以降のキー送信／絶対クリックの送信先をダイアログ／アプリ本体に切替 |
| テキスト表示待ち | OCR で特定テキストの出現を待機（範囲ドラッグ選択・日本語認識最適化） |

---

## 🎙 対象ソフト（同梱テンプレート）

初期状態で以下のテンプレートを同梱しています。**「対象アプリ管理」** から、お使いの環境のアプリを登録（「実行中ウィンドウから取得」が簡単）するとマクロが利用できます。

`VOICEVOX` ・ `A.I.VOICE2` ・ `CeVIO AI` ・ `VOICEPEAK` ・ `VoiSona Talk`

> 特定ソフトに依存しない汎用設計のため、上記以外の合成音声ソフトも追加できます。

---

## 📦 インストール

1. [Releases](https://github.com/bluemistel/V-Adapter/releases) から最新の `VAdapter.App.exe`（self-contained 単一 exe）をダウンロード
2. 任意のフォルダに置いて実行（.NET ランタイムのインストールは不要）

> 管理者権限で動作する合成音声ソフトを操作する場合は、V-Adapter も「管理者として実行」してください。
> 「テキスト表示待ち（OCR）」を使う場合は、Windows の OCR 言語機能（日本語）を追加してください（設定 → 時刻と言語 → 言語と地域 → 日本語 → 言語オプション → オプション機能）。

---

## 🚀 使い方

1. **「対象アプリ管理」** で合成音声ソフトを登録（座標モード：相対／絶対を選択）
2. マクロを選択して **「実行」**、または設定したショートカットで起動
3. 複数アプリ併用時は右の **「送信先」** で送信先を固定（未選択なら前面アプリを自動判定）
4. **「操作」→「編集」** で対象アプリごとのスクリプト（命令列）を作成

標準マクロ：**音声の再生 = `Ctrl+Space`** ／ **音声の保存 = `Ctrl+E`**

---

## 🛠 ソースからビルド

```sh
# 必要: .NET 8 SDK

# ビルド
dotnet build V-Adapter.sln

# テスト
dotnet test

# 実行（開発時）
dotnet run --project src/VAdapter.App

# 配布用 self-contained 単一 exe を発行
dotnet publish src/VAdapter.App -p:PublishProfile=win-x64
# => src/VAdapter.App/bin/Release/publish/win-x64/VAdapter.App.exe
```

### プロジェクト構成

```
src/VAdapter.Core         ドメインモデル・JSON シリアライズ・ストレージ（UI 非依存, net8.0）
src/VAdapter.Automation   Win32 P/Invoke・入力送信・OCR・ホットキー（net8.0-windows）
src/VAdapter.App          WPF UI（net8.0-windows）
tests/VAdapter.Core.Tests ユニットテスト（xUnit）
```

データ保存先: `%APPDATA%/V-Adapter/library.json`

### リリースに同梱する既定設定（組み込みマクロ・対象アプリ・ショートカット）の更新

新規インストール時の初期状態は、埋め込みファイル `src/VAdapter.App/Resources/default-library.json` から読み込まれます（**初回起動＝ライブラリが空のときのみ**シード。既存ユーザーの設定は上書きしません）。

組み込みマクロや対象アプリを増やしたり、ショートカットを変更した場合は、**アプリ上で編集 → 既定シードへ反映 → Release を再発行**します。

```powershell
# 1) アプリを起動して組み込みたいマクロ・対象アプリ・ショートカットを設定
# 2) 現在の設定 (%APPDATA%\V-Adapter\library.json) を既定シードへ反映
pwsh tools/update-default-library.ps1
# （手動で行う場合）
#   copy "%APPDATA%\V-Adapter\library.json" src\VAdapter.App\Resources\default-library.json
# 3) Release を再発行
dotnet publish src/VAdapter.App -p:PublishProfile=win-x64
```

> 注: 既定シードは**新規インストール（library.json が無い環境）**にのみ適用されます。
> 既にアプリを使用中のユーザーへ新しい組み込みマクロを届けたい場合は、`.vamacro` での配布（インポート）をご利用ください。

---

## 📝 更新履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。

## 📄 ライセンス

MIT License — [LICENSE](LICENSE) を参照。
