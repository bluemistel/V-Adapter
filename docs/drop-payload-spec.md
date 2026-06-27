# DropPayload 仕様 (v1)

> English version: [drop-payload-spec.en.md](drop-payload-spec.en.md)

## 1. 概要・設計思想

V-Adapter は、合成音声ソフトの書き出し（同名の `*.wav` + `*.txt`）を検知し、動画編集ソフトの
タイムラインへ投げ込む。このとき **編集ソフト固有の知識を本体から切り離す** ため、検知結果を
**編集ソフト非依存の中立契約 `DropPayload`** に正規化し、差し替え可能な **アダプタ (`IImportAdapter`)**
へ受け渡す。

```
[監視 WavTxtWatcher] → [話者ルーティング DropRouting] → DropPayload（中立契約）
                                                          → IImportAdapter（差し替え可能）
                                                            ├ 組込 gcmz (AviUtl,  v1)
                                                            ├ 組込 gcmz (AviUtl2, v2)
                                                            └ 外部コマンド（payload.json + コマンド実行）
```

設計上の狙い:

- **疎結合** — 本体は「検知 → 正規化 → 受け渡し」のみを担い、編集ソフト固有の操作を持たない。
- **拡張容易** — 新しい編集ソフト（Premiere / DaVinci 等）対応は、本体を変更せず
  **アウトオブツリーの外部アダプタ**として有志が作成・配布できる。
- **意図ベース** — ペイロードは fps 依存の具体値（フレーム数など）を持たず「意図」を表現する。
  具体的な数値はアダプタ側が算出する。

## 2. `DropPayload` v1 スキーマ

`System.Text.Json`（camelCase、列挙体は文字列、null は省略）で直列化する。外部アダプタへは
UTF-8 の `payload.json` として渡す。

| フィールド | 型 | 必須 | 説明 |
|-----------|----|:--:|------|
| `schemaVersion` | int | ✓ | スキーマ版。現在は `1`。 |
| `source` | object | ✓ | 送信元アプリ情報（下記）。 |
| `source.app` | string | ✓ | 送信元アプリ名。常に `"V-Adapter"`。 |
| `source.version` | string |  | 送信元アプリのバージョン。 |
| `files` | array | ✓ | 役割付きファイル（下記）。1つ以上。 |
| `files[].role` | string | ✓ | `"audio"` / `"subtitle"`。 |
| `files[].path` | string | ✓ | ファイルのフルパス。 |
| `files[].encoding` | string |  | テキストの文字コード（判明時。例 `"utf-8"`）。 |
| `speaker` | string |  | 解決済みの話者名。 |
| `routing` | object | ✓ | 配置ヒント（下記）。 |
| `routing.trackHint` | int? |  | 希望トラック（レイヤー）番号。`null` はアダプタ既定に委ねる。 |
| `timing` | object | ✓ | タイミングの意図（下記）。 |
| `timing.advanceToItemEnd` | bool | ✓ | 投入後にカーソル/シークを挿入アイテムの終端へ進める意図。 |

### 例 JSON

```json
{
  "schemaVersion": 1,
  "source": { "app": "V-Adapter", "version": "0.0.2" },
  "files": [
    { "role": "audio", "path": "C:\\out\\0001_ずんだもん_こんにちは.wav" },
    { "role": "subtitle", "path": "C:\\out\\0001_ずんだもん_こんにちは.txt" }
  ],
  "speaker": "ずんだもん",
  "routing": { "trackHint": 5 },
  "timing": { "advanceToItemEnd": true }
}
```

## 3. アダプタ契約

### 3.1 組込 gcmz アダプタ（参考: 写像規則）

組込の `GcmzImportAdapter` は中立ペイロードを ごちゃまぜドロップス 外部連携API のパラメータへ
写像する。写像は純粋関数 `GcmzPayloadMapper` に集約している。

| ペイロード | gcmz パラメータ |
|-----------|----------------|
| `files[].path`（全件） | `files`（フルパス配列） |
| `routing.trackHint`（null は既定レイヤー） | `layer` |
| `timing.advanceToItemEnd` + audio の長さ + プロジェクト fps | `frameAdvance`（= ceil(秒 × fps)）。算出不可時は手動値へフォールバック |
| （AviUtl2 のみ）設定の margin | `margin` |
| AviUtl=1 / AviUtl2=2 | `COPYDATASTRUCT.dwData` |

gcmz 固有の制約として、`files` のパスは **ShiftJIS で表現可能な文字のみ**（絵文字等を含むパスは
非対応）。

### 3.2 外部コマンドアダプタ

V-Adapter は中立ペイロードを一時 `payload.json`（UTF-8, BOM 無し）へ書き出し、設定された
**コマンドテンプレート**を実行する。

- **起動**: テンプレート中の `{payload}` が `payload.json` のフルパスへ置換される。
  パスにスペースがあれば自動でクオートされる。先頭トークンが実行ファイル、残りが引数。
  - 例: `python davinci_import.py {payload}`
  - 例: `import_to_myapp.exe {payload}`
  - 例: `"C:\Program Files\app\run.exe" --in {payload}`
- **作業ディレクトリ / 環境**: 親プロセス（V-Adapter）を継承する。
- **エンコーディング**: `payload.json` は UTF-8（BOM 無し）。stdout/stderr も UTF-8 で読む。
- **終了コード**: `0` を成功、非 0 を失敗とする。
- **stdout JSON（任意）**: stdout の末尾に下記 JSON があれば、終了コードより優先する。
  ```json
  { "success": true, "message": "1 item inserted" }
  ```
  `success` が `false` の場合、終了コードに関わらず失敗扱い（`message` をエラーとして表示）。
- **タイムアウト**: 設定値（既定 15000ms）。超過時はプロセスツリーを終了し失敗とする。
- **後始末**: 実行後に `payload.json` は削除される。アダプタ側で内容を保持したい場合は
  起動直後にコピーすること。

### 3.3 最小リファレンスアダプタ（擬似コード / Python）

```python
import json, sys

# V-Adapter は: <this script> <path-to-payload.json> を実行する
with open(sys.argv[1], encoding="utf-8") as f:
    payload = json.load(f)

audio = next(x["path"] for x in payload["files"] if x["role"] == "audio")
subtitle = next((x["path"] for x in payload["files"] if x["role"] == "subtitle"), None)
track = payload["routing"].get("trackHint")

# ここで対象編集ソフトの API / スクリプトで取り込みを実装する
# ... import_into_editor(audio, subtitle, track, payload["timing"]["advanceToItemEnd"]) ...

print(json.dumps({"success": True, "message": "ok"}))
sys.exit(0)
```

## 4. バージョニング方針

- 互換性のない変更時のみ `schemaVersion` を上げる（フィールド追加など後方互換の変更では上げない）。
- アダプタは未知のフィールドを無視してよい。`schemaVersion` が想定より新しい場合は、
  解釈可能な範囲で処理するか、明示的に失敗（`success:false`）を返すこと。

## 5. 非対象（将来）

- 常駐 stdio JSON-RPC アダプタ（接続確立が重い編集ソフト向け）。現状はワンショット実行型のみ。
- 具体的な Premiere / DaVinci アダプタ本体（有志がアウトオブツリーで作成・配布する対象）。
