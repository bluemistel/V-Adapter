using System.Diagnostics;
using System.IO;

namespace VAdapter.Core.Launch;

/// <summary>アプリ起動の結果。</summary>
public sealed record LaunchResult(bool Success, bool AlreadyRunning, string? Error)
{
    public static LaunchResult Launched() => new(true, false, null);
    public static LaunchResult Running() => new(true, true, null);
    public static LaunchResult Fail(string error) => new(false, false, error);
}

/// <summary>
/// 実行ファイル（exe）を起動するランチャー。動画編集ソフト・合成音声ソフトを
/// 本アプリから起動する共通処理（「対象アプリの起動」命令／ヘッダーの電源ボタン）で使用する。
/// </summary>
public static class AppLauncher
{
    /// <summary>
    /// 実行ファイルを起動する。
    /// </summary>
    /// <param name="executablePath">起動する exe のフルパス。</param>
    /// <param name="arguments">起動引数（任意）。</param>
    /// <param name="skipIfRunning">同名プロセスが既に起動中なら起動をスキップする。</param>
    /// <param name="processNameForRunningCheck">
    /// 起動中判定に使うプロセス名（拡張子なし）。未指定なら exe ファイル名から推定する。
    /// </param>
    public static LaunchResult Launch(
        string? executablePath,
        string? arguments = null,
        bool skipIfRunning = true,
        string? processNameForRunningCheck = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return LaunchResult.Fail("実行ファイルのパスが指定されていません。");

        if (!File.Exists(executablePath))
            return LaunchResult.Fail($"実行ファイルが見つかりません: {executablePath}");

        var procName = !string.IsNullOrWhiteSpace(processNameForRunningCheck)
            ? processNameForRunningCheck
            : Path.GetFileNameWithoutExtension(executablePath);

        if (skipIfRunning && IsRunning(procName))
            return LaunchResult.Running();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            };
            Process.Start(psi);
            return LaunchResult.Launched();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"起動に失敗しました: {ex.Message}");
        }
    }

    /// <summary>指定プロセス名（拡張子なし）が起動中か。</summary>
    public static bool IsRunning(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
