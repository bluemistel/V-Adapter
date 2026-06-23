using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using VAdapter.Core.Models;
using VAdapter.Automation.Native;

namespace VAdapter.Automation.Windows;

/// <summary>トップレベルウィンドウの列挙と、対象アプリ設定に基づく特定を行う。</summary>
public sealed class WindowLocator
{
    /// <summary>可視のトップレベルウィンドウ（タイトル有り）を列挙する。UI のアプリ選択用。</summary>
    public IReadOnlyList<WindowInfo> EnumerateVisibleWindows()
    {
        var result = new List<WindowInfo>();
        var processNameCache = new Dictionary<uint, string>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            var title = GetWindowText(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            result.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ClassName = GetClassName(hWnd),
                ProcessId = pid,
                ProcessName = ResolveProcessName(pid, processNameCache),
            });
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>対象アプリ設定に一致する最初の可視ウィンドウを返す（なければ null）。</summary>
    public WindowInfo? Find(TargetApplication target)
    {
        var titleRegex = BuildTitleRegex(target);
        return EnumerateVisibleWindows().FirstOrDefault(w => Matches(w, target, titleRegex));
    }

    /// <summary>
    /// アクティブ化・送信用の緩いウィンドウ検索。プロセス名が設定されていればそれを最優先で照合し、
    /// （実行中にタイトルが変化しても追従できるよう）タイトル条件は無視する。
    /// メインウィンドウはタイトルが長い傾向があるため、その候補を優先して返す。
    /// </summary>
    public WindowInfo? FindForActivation(TargetApplication target)
    {
        if (string.IsNullOrEmpty(target.ProcessName))
            return Find(target);

        return EnumerateVisibleWindows()
            .Where(w => string.Equals(w.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase))
            .Where(w => ClassMatches(w.ClassName, target.WindowClass))
            .OrderByDescending(w => w.Title.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// 対象アプリに属する Windows 標準ダイアログ（コモンダイアログ等）を検索する。
    /// ダイアログ枠クラス（既定 "#32770"）と、対象アプリと同一プロセスであることで判定。
    /// タイトル部分一致を指定すると種類を絞り込める。
    /// </summary>
    public WindowInfo? FindDialog(TargetApplication? target, string dialogClass, string? titleContains)
    {
        if (string.IsNullOrEmpty(dialogClass))
            dialogClass = "#32770";

        return EnumerateVisibleWindows()
            .Where(w => string.Equals(w.ClassName, dialogClass, StringComparison.Ordinal))
            .Where(w => target is null
                        || string.IsNullOrEmpty(target.ProcessName)
                        || string.Equals(w.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrEmpty(titleContains)
                        || w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    /// <summary>
    /// 現在の前面ウィンドウが対象アプリに属するか判定する。
    /// プロセス名が設定されていればプロセス一致のみで判断（タイトル変化に強い）。
    /// </summary>
    public bool IsForegroundOfApp(TargetApplication target)
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(fg, out var pid);

        if (!string.IsNullOrEmpty(target.ProcessName))
            return string.Equals(ResolveProcessName(pid), target.ProcessName, StringComparison.OrdinalIgnoreCase);

        // プロセス名未設定のときはフル条件で判定。
        var info = new WindowInfo
        {
            Handle = fg,
            Title = GetWindowText(fg),
            ClassName = GetClassName(fg),
            ProcessId = pid,
            ProcessName = ResolveProcessName(pid),
        };
        return Matches(info, target, BuildTitleRegex(target));
    }

    // WPF の HwndWrapper クラス名等に含まれる GUID（起動ごとに変化）。
    private static readonly Regex GuidLike = new(
        @"[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    // JUCE("JUCE_19ef2440e4c") や WinForms 等に含まれる、起動ごとに変わる長い 16 進ラン。
    private static readonly Regex HexRun = new(@"[0-9a-fA-F]{6,}", RegexOptions.Compiled);

    /// <summary>
    /// ウィンドウクラスの照合。完全一致に加え、起動ごとに変化する識別子
    /// （WPF "HwndWrapper[...;;{GUID}]" の GUID や JUCE "JUCE_xxxxxxxxxxx" の 16 進サフィックス等）を
    /// 除去した正規化形でも一致を許容する。パターンが空なら常に一致。
    /// プロセス名で別アプリは区別されるため、クラスの積極的な正規化は安全。
    /// </summary>
    internal static bool ClassMatches(string actualClass, string? patternClass)
    {
        if (string.IsNullOrEmpty(patternClass))
            return true;
        if (string.Equals(actualClass, patternClass, StringComparison.Ordinal))
            return true;
        return string.Equals(NormalizeClass(actualClass), NormalizeClass(patternClass), StringComparison.Ordinal);
    }

    /// <summary>クラス名から起動ごとに変化する識別子（GUID・長い 16 進ラン）を除去して安定化する。</summary>
    internal static string NormalizeClass(string className)
    {
        if (string.IsNullOrEmpty(className))
            return string.Empty;
        var normalized = GuidLike.Replace(className, string.Empty);
        return HexRun.Replace(normalized, string.Empty);
    }

    private static Regex? BuildTitleRegex(TargetApplication target)
    {
        if (!target.TitleIsRegex || string.IsNullOrEmpty(target.WindowTitlePattern))
            return null;
        try { return new Regex(target.WindowTitlePattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException) { return null; }
    }

    private static bool Matches(WindowInfo w, TargetApplication target, Regex? titleRegex)
    {
        if (!string.IsNullOrEmpty(target.ProcessName) &&
            !string.Equals(w.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!ClassMatches(w.ClassName, target.WindowClass))
            return false;

        if (!string.IsNullOrEmpty(target.WindowTitlePattern))
        {
            if (titleRegex is not null)
            {
                if (!titleRegex.IsMatch(w.Title)) return false;
            }
            else if (!w.Title.Contains(target.WindowTitlePattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // 何の条件も無いマッチ（全フィルタ空）は曖昧なので除外。
        return !string.IsNullOrEmpty(target.ProcessName)
            || !string.IsNullOrEmpty(target.WindowClass)
            || !string.IsNullOrEmpty(target.WindowTitlePattern);
    }

    /// <summary>指定スクリーン座標の直下にあるトップレベルウィンドウの情報を取得する。</summary>
    public WindowInfo? GetTopLevelWindowAt(int screenX, int screenY)
    {
        var hit = NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = screenX, Y = screenY });
        if (hit == IntPtr.Zero)
            return null;

        var root = NativeMethods.GetAncestor(hit, NativeMethods.GA_ROOT);
        if (root == IntPtr.Zero)
            root = hit;

        NativeMethods.GetWindowThreadProcessId(root, out var pid);
        return new WindowInfo
        {
            Handle = root,
            Title = GetWindowText(root),
            ClassName = GetClassName(root),
            ProcessId = pid,
            ProcessName = ResolveProcessName(pid),
        };
    }

    internal static string GetWindowText(IntPtr hWnd)
    {
        var len = NativeMethods.GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string ResolveProcessName(uint pid)
    {
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveProcessName(uint pid, Dictionary<uint, string> cache)
    {
        if (cache.TryGetValue(pid, out var cached))
            return cached;

        string name = string.Empty;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            name = proc.ProcessName; // 拡張子なし
        }
        catch
        {
            // アクセス不可・終了済みなどは空のまま。
        }

        cache[pid] = name;
        return name;
    }
}
