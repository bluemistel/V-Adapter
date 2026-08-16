namespace VAdapter.Automation.Aviutl;

/// <summary>ごちゃまぜドロップス共有メモリから読み取った状態。</summary>
public sealed class GcmzInfo
{
    public required IntPtr Window { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>フレームレート分子（VideoRate）。</summary>
    public int VideoRate { get; init; }

    /// <summary>フレームレート分母（VideoScale）。</summary>
    public int VideoScale { get; init; }

    /// <summary>プロジェクトのフレームレート（VideoRate / VideoScale）。算出不可なら 0。</summary>
    public double Fps => VideoScale > 0 ? (double)VideoRate / VideoScale : 0;

    /// <summary>外部連携API バージョン（GCMZAPIVer）。</summary>
    public required int ApiVersion { get; init; }

    /// <summary>編集中プロジェクトのパス（無い場合は空）。</summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// 接続先ウィンドウのプロセス名（拡張子なし。例: aviutl / aviutl2）。取得不可なら空。
    /// ミューテックス・共有メモリ名は AviUtl 無印と AviUtl2 で共通のため、
    /// 両方を起動していると意図しない方へ接続することがある。その検出に使う。
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>プロジェクトが読み込まれているか（解像度が有効か）。</summary>
    public bool HasProject => Width > 0 && Height > 0;
}

/// <summary>投げ込み結果。</summary>
public sealed class GcmzDropResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static GcmzDropResult Ok() => new() { Success = true };
    public static GcmzDropResult Fail(string error) => new() { Success = false, Error = error };
}
