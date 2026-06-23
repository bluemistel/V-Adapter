using System.Diagnostics;
using VAdapter.Core.Models;
using VAdapter.Automation.Input;
using VAdapter.Automation.Native;
using VAdapter.Automation.Ocr;
using VAdapter.Automation.Windows;

namespace VAdapter.Automation.Execution;

/// <summary>マクロのスクリプト（命令列）を直列実行するエンジン。</summary>
public sealed class MacroRunner
{
    private const int PollIntervalMs = 150;

    private readonly WindowLocator _locator = new();
    private readonly InputSender _input = new();
    private readonly OcrService _ocr = new();

    /// <summary>待機系命令のポーリング間隔（ミリ秒）。テストで上書き可能。</summary>
    public int PollInterval { get; init; } = PollIntervalMs;

    /// <summary>OCR エンジン（診断用に公開）。</summary>
    public OcrService Ocr => _ocr;

    /// <summary>マクロを実行する。状況に一致するスクリプトを 1 つ選んで実行する。</summary>
    /// <param name="preferredTargetId">
    /// 送信先として優先する対象アプリ ID（UI の送信先トグルで指定）。
    /// 指定があり、かつ該当スクリプトが存在すれば最優先で実行する。
    /// </param>
    public async Task<MacroRunResult> RunAsync(
        Macro macro,
        MacroLibrary library,
        IProgress<string>? log = null,
        CancellationToken ct = default,
        string? preferredTargetId = null)
    {
        var selection = SelectScript(macro, library, preferredTargetId);
        if (selection is null)
        {
            log?.Report($"マクロ「{macro.Name}」: 実行可能なスクリプトがありません。");
            return MacroRunResult.Fail("対象アプリに一致する実行可能なスクリプトがありません。");
        }

        var (script, target) = selection.Value;
        log?.Report($"マクロ「{macro.Name}」を開始（対象: {target?.Name ?? "共通"}）");

        var ctx = new RunContext { Target = target };

        // 開始時に対象ウィンドウを前面化（割当がある場合）。
        if (target is not null)
        {
            var initial = _locator.FindForActivation(target);
            if (initial is not null)
                ForegroundWindowHelper.BringToForeground(initial.Handle);
        }

        foreach (var instruction in script.Instructions)
        {
            ct.ThrowIfCancellationRequested();
            log?.Report($"  実行: {instruction.Summary}");

            try
            {
                var result = await ExecuteAsync(instruction, ctx, log, ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    log?.Report($"  失敗: {result.Error}");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.Report($"  例外: {ex.Message}");
                return MacroRunResult.Fail(ex.Message, instruction);
            }
        }

        log?.Report($"マクロ「{macro.Name}」完了");
        return MacroRunResult.Ok();
    }

    /// <summary>
    /// 実行するスクリプトを選択する。優先順位:
    /// 1) 対象アプリが前面のスクリプト, 2) 対象アプリが起動中のスクリプト,
    /// 3) 共通(対象なし)スクリプト, 4) 対象指定はあるが未起動の先頭。
    /// </summary>
    private (MacroScript Script, TargetApplication? Target)? SelectScript(
        Macro macro, MacroLibrary library, string? preferredTargetId)
    {
        if (macro.Scripts.Count == 0)
            return null;

        // 0) 送信先トグルで明示指定された対象アプリのスクリプトを最優先。
        if (preferredTargetId is not null)
        {
            var preferred = macro.Scripts.FirstOrDefault(s => s.TargetApplicationId == preferredTargetId);
            var preferredTarget = library.FindTarget(preferredTargetId);
            if (preferred is not null && preferredTarget is not null)
                return (preferred, preferredTarget);
        }

        var withTarget = macro.Scripts
            .Where(s => s.TargetApplicationId is not null)
            .Select(s => (Script: s, Target: library.FindTarget(s.TargetApplicationId)))
            .Where(x => x.Target is not null)
            .ToList();

        // 1) 前面一致（プロセス名優先の緩い判定）
        foreach (var (s, t) in withTarget)
        {
            if (_locator.IsForegroundOfApp(t!))
                return (s, t);
        }

        // 2) 起動中
        foreach (var (s, t) in withTarget)
        {
            if (_locator.FindForActivation(t!) is not null)
                return (s, t);
        }

        // 3) 共通（フォールバック）
        var fallback = macro.Scripts.FirstOrDefault(s => s.TargetApplicationId is null);
        if (fallback is not null)
            return (fallback, null);

        // 4) 対象指定はあるが未起動 → 先頭（実行時に失敗する可能性あり）
        if (withTarget.Count > 0)
            return (withTarget[0].Script, withTarget[0].Target);

        return null;
    }

    /// <summary>1 回のマクロ実行を通じて保持する状態。</summary>
    private sealed class RunContext
    {
        public required TargetApplication? Target { get; init; }

        /// <summary>「操作対象の切り替え」で指定された送信先ウィンドウ（null は対象アプリ本体）。</summary>
        public IntPtr? ActiveWindow { get; set; }
    }

    private async Task<MacroRunResult> ExecuteAsync(
        Instruction instruction,
        RunContext ctx,
        IProgress<string>? log,
        CancellationToken ct)
    {
        var target = ctx.Target;
        return instruction switch
        {
            WaitInstruction wait => await ExecuteWait(wait, ct),
            SendKeysInstruction keys => ExecuteSendKeys(keys, ctx),
            ClickInstruction click => ExecuteClick(click, ctx),
            WaitForWindowInstruction waitWin => await ExecuteWaitForWindow(waitWin, target, ct),
            WaitForActivationInstruction waitAct => await ExecuteWaitForActivation(waitAct, target, ct),
            WaitForDialogInstruction waitDlg => await ExecuteWaitForDialog(waitDlg, target, log, ct),
            SwitchTargetInstruction switchTgt => await ExecuteSwitchTarget(switchTgt, ctx, log, ct),
            WaitForTextInstruction waitText => await ExecuteWaitForText(waitText, target, log, ct),
            _ => MacroRunResult.Fail($"未対応の命令: {instruction.Kind}", instruction),
        };
    }

    private async Task<MacroRunResult> ExecuteWait(WaitInstruction wait, CancellationToken ct)
    {
        await Task.Delay(Math.Max(0, wait.DurationMs), ct).ConfigureAwait(false);
        return MacroRunResult.Ok();
    }

    private MacroRunResult ExecuteSendKeys(SendKeysInstruction keys, RunContext ctx)
    {
        if (!keys.KeyCombination.IsValid)
            return MacroRunResult.Fail("キーが未設定です。", keys);

        BringInputForeground(ctx);
        _input.SendKeyCombination(keys.KeyCombination);
        return MacroRunResult.Ok();
    }

    private MacroRunResult ExecuteClick(ClickInstruction click, RunContext ctx)
    {
        var target = ctx.Target;
        var mode = click.CoordinateModeOverride ?? target?.CoordinateMode ?? CoordinateMode.Absolute;

        int screenX, screenY;
        if (mode == CoordinateMode.Absolute)
        {
            // 絶対クリックは現在の操作対象（ダイアログ等）を前面化してから。
            screenX = click.X;
            screenY = click.Y;
            BringInputForeground(ctx);
        }
        else
        {
            // 相対クリックの基準ウィンドウは、操作対象切替中ならそのウィンドウ（ダイアログ等）、
            // なければ対象アプリ本体。アンカー基準＋オフセットで、ウィンドウサイズ変化に追従する。
            IntPtr baseHandle;
            if (ctx.ActiveWindow is { } h && NativeMethods.IsWindow(h))
            {
                baseHandle = h;
            }
            else
            {
                var window = target is null ? null : _locator.FindForActivation(target);
                if (window is null)
                    return MacroRunResult.Fail("相対クリックに必要な対象ウィンドウが見つかりません。", click);
                baseHandle = window.Handle;
            }

            ForegroundWindowHelper.BringToForeground(baseHandle);

            var (_, _, clientW, clientH) = WindowGeometry.GetClientAreaOnScreen(baseHandle);
            var (anchorX, anchorY) = WindowGeometry.AnchorPoint(clientW, clientH, click.Anchor);
            (screenX, screenY) = WindowGeometry.ClientToScreen(baseHandle, anchorX + click.X, anchorY + click.Y);
        }

        _input.ClickAt(screenX, screenY, click.Button);
        return MacroRunResult.Ok();
    }

    private async Task<MacroRunResult> ExecuteSwitchTarget(
        SwitchTargetInstruction instr, RunContext ctx, IProgress<string>? log, CancellationToken ct)
    {
        if (instr.Target == SwitchTargetKind.AppWindow)
        {
            ctx.ActiveWindow = null;
            var window = ctx.Target is null ? null : _locator.FindForActivation(ctx.Target);
            if (window is not null)
                ForegroundWindowHelper.BringToForeground(window.Handle);
            log?.Report("    操作対象をアプリ本体に切り替えました。");
            return MacroRunResult.Ok();
        }

        // ダイアログへ切り替え（出現まで待機）。
        WindowInfo? dialog = null;
        var found = await PollUntilAsync(() =>
        {
            dialog = _locator.FindDialog(ctx.Target, instr.DialogClass, instr.TitleContains);
            return dialog is not null;
        }, instr.TimeoutMs, ct).ConfigureAwait(false);

        if (!found)
        {
            var what = string.IsNullOrEmpty(instr.TitleContains) ? "ダイアログ" : $"ダイアログ「{instr.TitleContains}」";
            return MacroRunResult.Fail($"{what}が {instr.TimeoutMs} ms 以内に見つからず操作対象を切り替えられませんでした。", instr);
        }

        ctx.ActiveWindow = dialog!.Handle;
        ForegroundWindowHelper.BringToForeground(dialog!.Handle);
        log?.Report($"    操作対象をダイアログ「{dialog!.Title}」に切り替えました。");
        return MacroRunResult.Ok();
    }

    /// <summary>現在の操作対象ウィンドウ（切替中ならそれ、無ければ対象アプリ本体）を前面化する。</summary>
    private void BringInputForeground(RunContext ctx)
    {
        if (ctx.ActiveWindow is { } h && NativeMethods.IsWindow(h))
        {
            ForegroundWindowHelper.BringToForeground(h);
            return;
        }
        // 無効化（ダイアログが閉じた等）していたら本体へフォールバック。
        ctx.ActiveWindow = null;
        BringTargetForeground(ctx.Target);
    }

    private async Task<MacroRunResult> ExecuteWaitForWindow(
        WaitForWindowInstruction wait, TargetApplication? target, CancellationToken ct)
    {
        if (target is null)
            return MacroRunResult.Fail("対象アプリが未割当のためウィンドウを待機できません。", wait);

        var found = await PollUntilAsync(
            () => _locator.FindForActivation(target) is not null,
            wait.TimeoutMs, ct).ConfigureAwait(false);

        return found
            ? MacroRunResult.Ok()
            : MacroRunResult.Fail($"ウィンドウが {wait.TimeoutMs} ms 以内に表示されませんでした。", wait);
    }

    private async Task<MacroRunResult> ExecuteWaitForActivation(
        WaitForActivationInstruction wait, TargetApplication? target, CancellationToken ct)
    {
        if (target is null)
            return MacroRunResult.Ok(); // 共通スクリプトでは切替対象が無いので即時成功。

        var found = await PollUntilAsync(() =>
        {
            // プロセス一致で「対象アプリが前面か」を判定（タイトル変化・複数ウィンドウに強い）。
            if (_locator.IsForegroundOfApp(target))
                return true;
            // まだ前面でなければ前面化を試みる。
            var window = _locator.FindForActivation(target);
            if (window is not null)
                ForegroundWindowHelper.BringToForeground(window.Handle);
            return _locator.IsForegroundOfApp(target);
        }, wait.TimeoutMs, ct).ConfigureAwait(false);

        return found
            ? MacroRunResult.Ok()
            : MacroRunResult.Fail($"対象アプリ「{target.Name}」が {wait.TimeoutMs} ms 以内に前面になりませんでした（プロセス名: {target.ProcessName ?? "未設定"}）。", wait);
    }

    private async Task<MacroRunResult> ExecuteWaitForDialog(
        WaitForDialogInstruction wait, TargetApplication? target, IProgress<string>? log, CancellationToken ct)
    {
        WindowInfo? dialog = null;
        var found = await PollUntilAsync(() =>
        {
            dialog = _locator.FindDialog(target, wait.DialogClass, wait.TitleContains);
            return dialog is not null;
        }, wait.TimeoutMs, ct).ConfigureAwait(false);

        if (!found)
        {
            var what = string.IsNullOrEmpty(wait.TitleContains) ? "ダイアログ" : $"ダイアログ「{wait.TitleContains}」";
            return MacroRunResult.Fail($"{what}が {wait.TimeoutMs} ms 以内に表示されませんでした。", wait);
        }

        log?.Report($"    ダイアログ検出: {dialog!.Title}");
        if (wait.BringToForeground)
            ForegroundWindowHelper.BringToForeground(dialog!.Handle);
        return MacroRunResult.Ok();
    }

    private async Task<MacroRunResult> ExecuteWaitForText(
        WaitForTextInstruction wait, TargetApplication? target, IProgress<string>? log, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(wait.Text))
            return MacroRunResult.Fail("検出するテキストが未設定です。", wait);

        if (!_ocr.IsAvailable)
            return MacroRunResult.Fail("OCR エンジンが利用できません（言語パック未導入の可能性）。", wait);

        var found = await PollUntilAsync(async () =>
        {
            var window = target is null ? null : _locator.FindForActivation(target);
            if (window is null)
                return false;

            var (rx, ry, rw, rh) = ResolveRegion(window.Handle, wait.Region);
            if (rw <= 0 || rh <= 0)
                return false;

            // 日本語 OCR は文字が小さいと精度が落ちるため 2 倍に拡大してから認識する。
            var png = ScreenCapture.CaptureRegionPng(rx, ry, rw, rh, scale: 2.0);
            var text = await _ocr.RecognizeAsync(png).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
                log?.Report($"    OCR認識: {Trim(text)}");
            return OcrTextMatcher.Contains(text, wait.Text);
        }, wait.TimeoutMs, ct).ConfigureAwait(false);

        return found
            ? MacroRunResult.Ok()
            : MacroRunResult.Fail($"テキスト「{wait.Text}」が {wait.TimeoutMs} ms 以内に表示されませんでした。", wait);
    }

    private static string Trim(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ");
        return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
    }

    private static (int X, int Y, int W, int H) ResolveRegion(IntPtr hWnd, RelativeRect? region)
    {
        var (clientX, clientY, clientW, clientH) = WindowGeometry.GetClientAreaOnScreen(hWnd);
        if (region is null)
            return (clientX, clientY, clientW, clientH);

        return (clientX + region.X, clientY + region.Y, region.Width, region.Height);
    }

    private void BringTargetForeground(TargetApplication? target)
    {
        if (target is null)
            return;
        var window = _locator.FindForActivation(target);
        if (window is not null)
            ForegroundWindowHelper.BringToForeground(window.Handle);
    }

    private async Task<bool> PollUntilAsync(Func<bool> condition, int timeoutMs, CancellationToken ct)
        => await PollUntilAsync(() => Task.FromResult(condition()), timeoutMs, ct).ConfigureAwait(false);

    private async Task<bool> PollUntilAsync(Func<Task<bool>> condition, int timeoutMs, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await condition().ConfigureAwait(false))
                return true;
            if (sw.ElapsedMilliseconds >= timeoutMs)
                return false;
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }
}
