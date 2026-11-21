using VAdapter.Automation.Native;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Input;

/// <summary>
/// ショートカットのキーが離されるのを待つ。
///
/// グローバルショートカット（RegisterHotKey）はキーを押した瞬間に通知されるため、
/// そのまま命令を実行すると、利用者がまだ押している Ctrl 等が送信キーやクリックに重なる
/// （例: F5 を送ったつもりが Ctrl+F5 になる）。キーが離されてから始めることで、
/// 利用者から見て「キーを離したときに実行される」動きにそろえる。
/// </summary>
public static class KeyReleaseWaiter
{
    /// <summary>待つ上限の既定値。押したまま離さない使い方でもマクロが止まり続けないようにする。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(1500);

    private const int PollIntervalMs = 10;

    /// <summary>
    /// <paramref name="combo"/> のキーがすべて離されるまで待つ。
    /// </summary>
    /// <returns>離された（または最初から押されていなかった）なら true、上限まで押されたままなら false。</returns>
    public static async Task<bool> WaitAsync(KeyCombination combo, TimeSpan timeout, CancellationToken ct = default)
    {
        var keys = combo.PhysicalVirtualKeys();
        if (keys.Count == 0)
            return true;

        var deadline = DateTime.UtcNow + timeout;
        while (AnyPressed(keys))
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
        return true;
    }

    private static bool AnyPressed(IReadOnlyList<int> keys)
    {
        foreach (var vk in keys)
        {
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
                return true;
        }
        return false;
    }
}
