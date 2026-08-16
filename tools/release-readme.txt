============================================================
 V-Adapter  v{VERSION} (β / beta)
 合成音声ソフトの操作を統一ショートカットから半自動化する
 Windows デスクトップ向けマクロツール
============================================================

■ これは何？
  VOICEVOX / A.I.VOICE2 / A.I.VOICE / CeVIO AI / VOICEPEAK /
  VoiSona Talk / VOICEROID2 / COEIROINK v2 など、
  各社で UI・ショートカットがバラバラな合成音声ソフトを、
  「音声の再生」「音声の保存」といった統一ショートカットで操作できます。
  さらに、書き出した音声・字幕を AviUtl / AviUtl2 へ自動投入できます。

  ※ 本バージョンは β 版です。仕様は変更される場合があります。
  ※ 本ソフトは非公式・無保証です（LICENSE.txt の免責事項を参照）。


■ 動作環境
  - Windows 10 / 11 (64bit)
  - .NET ランタイムのインストールは不要（self-contained）


■ 使い方（インストール不要）
  1. zip を「書き込み可能な任意のフォルダ」に展開します（ポータブル形式）。
  2. 「V-Adapter.exe」をダブルクリックして起動します。
  3. 「対象アプリ管理」で合成音声ソフトを登録します
     （「実行中ウィンドウから取得」が簡単。主要ソフトのテンプレートを同梱）。
  4. マクロを選んで「実行」、または設定したショートカットで起動します。
       音声の再生 = Ctrl+Q ／ 音声の保存 = Ctrl+R ／ 合成音声ソフトの起動 = Ctrl+J

  ・詳しい使い方・AviUtl/AviUtl2 連携の設定は、アプリ右上の「?（使い方）」を参照してください。


■ アップデート方法
  新しい zip を「同じフォルダに上書き展開」してください。
  script/user-script/ のマクロと library.json / integration.json は保持されます。
  （起動時に GitHub のタグと照合し、更新があればメイン画面に案内を表示します）


■ AviUtl / AviUtl2 連携（任意）
  合成音声ソフトの wav+txt を、ごちゃまぜドロップス経由でタイムラインへ自動投入できます。
  字幕・口パク生成は PSDToolKit / PSDToolKit2 が担います。
  設定は失敗しやすいので、アプリ内「使い方 → AviUtl・AviUtl2 連携」を必ずご確認ください。
  ※ ごちゃまぜドロップス / GCMZDrops2 は第三者製プラグインです（LICENSE.txt の免責に同意のうえご利用ください）。


■ 注意事項
  - 管理者権限で動作する合成音声ソフトを操作する場合は、V-Adapter も「管理者として実行」してください。
  - 「テキスト表示待ち（OCR）」を使う場合は、Windows の OCR 言語機能（日本語）を追加してください。


■ ファイル構成（すべて exe と同じ階層・ポータブル）
  V-Adapter.exe           … 本体
  library.json            … 対象アプリ（初回起動時に自動生成）
  integration.json        … 連携設定（初回起動時に自動生成）
  script/built-in/        … 同梱の組込マクロ（.vamacro）。更新で上書きされます
  script/user-script/     … あなたが作成・インポートしたマクロ。更新でも温存されます

  ※ 旧バージョン（v0.0.2 以前）から更新した場合、初回起動時に
     %APPDATA%\V-Adapter の旧データを自動で移行します。


■ バージョン / ライセンス / 配布元
  Version : {VERSION} (beta)
  License : MIT License（LICENSE.txt を参照。免責事項・サードパーティ条項を含む）
  GitHub  : https://github.com/bluemistel/V-Adapter
  Wiki    : https://github.com/bluemistel/V-Adapter/wiki

  更新履歴は CHANGELOG.md を参照してください。
============================================================
