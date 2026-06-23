namespace VAdapter.Core.Models;

/// <summary>
/// マクロが操作する対象アプリケーション（合成音声ソフト）の識別設定。
/// </summary>
public sealed class TargetApplication
{
    /// <summary>一意な識別子。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>表示名（例: "VOICEVOX"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>対象プロセス名（拡張子なし、大文字小文字無視）。任意。</summary>
    public string? ProcessName { get; set; }

    /// <summary>ウィンドウタイトルの一致パターン（部分一致、または正規表現）。任意。</summary>
    public string? WindowTitlePattern { get; set; }

    /// <summary><see cref="WindowTitlePattern"/> を正規表現として扱うか。</summary>
    public bool TitleIsRegex { get; set; }

    /// <summary>ウィンドウクラス名（任意。指定時は完全一致で絞り込み）。</summary>
    public string? WindowClass { get; set; }

    /// <summary>このアプリに対するクリック座標の既定の解釈方法。</summary>
    public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Relative;
}
