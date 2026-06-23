using System.Windows;
using VAdapter.Automation.Input;

namespace VAdapter.App.Services;

/// <summary>「次の左クリック」のスクリーン座標を捕捉するヘルパー。</summary>
public static class CoordinateCapture
{
    // 捕捉中のフックを強参照で保持する。
    // SetWindowsHookEx に渡したデリゲートが GC 回収されるとネイティブ側からの
    // コールバックでアクセス違反（異常終了）になるため、完了まで必ず生かしておく。
    private static readonly HashSet<GlobalMouseHook> ActiveHooks = new();

    /// <summary>低レベルマウスフックで次の左クリック位置を 1 回だけ取得する。</summary>
    public static Task<(int X, int Y)> CaptureNextClickAsync()
    {
        var tcs = new TaskCompletionSource<(int, int)>();
        var hook = new GlobalMouseHook();
        ActiveHooks.Add(hook);

        void Cleanup()
        {
            hook.Dispose();
            ActiveHooks.Remove(hook);
        }

        hook.LeftButtonDown += (x, y) =>
        {
            if (!tcs.TrySetResult((x, y)))
                return;
            // 再入を避けるためフック解除はメッセージ処理後に行う。
            Application.Current.Dispatcher.BeginInvoke(new Action(Cleanup));
        };

        hook.Start();
        if (!hook.IsActive)
        {
            Cleanup();
            tcs.TrySetException(new InvalidOperationException("マウスフックの設定に失敗しました。"));
        }

        return tcs.Task;
    }
}
