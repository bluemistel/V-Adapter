using VAdapter.Core.Models;
using VAdapter.Automation.Native;

namespace VAdapter.Automation.Input;

/// <summary>SendInput によるマウスクリック・キー送信（フォアグラウンド方式）。</summary>
public sealed class InputSender
{
    /// <summary>指定スクリーン座標でクリックする。事前に対象を前面化しておくこと。</summary>
    public void ClickAt(int screenX, int screenY, MouseButton button)
    {
        NativeMethods.SetCursorPos(screenX, screenY);

        var (down, up) = button switch
        {
            MouseButton.Right => (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
            _ => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP),
        };

        SendMouse(down);
        SendMouse(up);
    }

    /// <summary>修飾キー押下後の猶予（ms）。対象アプリが「押しっぱなし」を観測できるようにする。</summary>
    private const int ModifierSettleMs = 30;

    /// <summary>主キーの押下から解放までの保持時間（ms）。</summary>
    private const int KeyHoldMs = 20;

    /// <summary>修飾キー押下 → 主キー押下/解放 → 修飾キー解放 の順でキー組み合わせを送信する。</summary>
    public InputSendResult SendKeyCombination(KeyCombination combo)
    {
        if (!combo.IsValid)
            return new InputSendResult(0, 0, 0);

        var modifiers = ModifierVks(combo.Modifiers).ToList();

        // 1 回の SendInput にまとめず、押下・主キー・解放を別々に送って間に待機を挟む。
        // JUCE 製アプリ（VOICEPEAK 等）は修飾キーの状態を GetAsyncKeyState（リアルタイム）で読むため、
        // まとめて送ると対象アプリが主キーを処理する前に修飾キーの解放が届き、組み合わせが成立しない。
        // 実機の速度・負荷で成否が変わる（＝環境によって動いたり動かなかったりする）のもこれが理由。
        uint requested = 0, sent = 0;
        int lastError = 0;

        void Step(params NativeMethods.INPUT[] inputs)
        {
            var r = Send(inputs);
            requested += r.Requested;
            sent += r.Sent;
            if (!r.AllSent && lastError == 0)
                lastError = r.LastError;
        }

        if (modifiers.Count > 0)
        {
            Step(modifiers.Select(vk => KeyInput(vk, keyUp: false)).ToArray());
            Thread.Sleep(ModifierSettleMs);
        }

        Step(KeyInput((ushort)combo.VirtualKey, keyUp: false));
        Thread.Sleep(KeyHoldMs);
        Step(KeyInput((ushort)combo.VirtualKey, keyUp: true));

        if (modifiers.Count > 0)
        {
            // 解放も主キー処理の完了を待ってから（逆順）。
            Thread.Sleep(ModifierSettleMs);
            var ups = new NativeMethods.INPUT[modifiers.Count];
            for (int i = 0; i < modifiers.Count; i++)
                ups[i] = KeyInput(modifiers[modifiers.Count - 1 - i], keyUp: true);
            Step(ups);
        }

        return new InputSendResult(requested, sent, lastError);
    }

    private static IEnumerable<ushort> ModifierVks(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control)) yield return NativeMethods.VK_CONTROL;
        if (modifiers.HasFlag(KeyModifiers.Alt)) yield return NativeMethods.VK_MENU;
        if (modifiers.HasFlag(KeyModifiers.Shift)) yield return NativeMethods.VK_SHIFT;
        if (modifiers.HasFlag(KeyModifiers.Win)) yield return NativeMethods.VK_LWIN;
    }

    private static NativeMethods.INPUT KeyInput(ushort vk, bool keyUp)
    {
        // スキャンコードで送る（JUCE 等スキャンコード読みのアプリで修飾キー併用が効くように）。
        ushort scan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);

        uint flags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0;
        ushort wVk = vk;
        ushort wScan = 0;

        if (scan != 0)
        {
            flags |= NativeMethods.KEYEVENTF_SCANCODE;
            if (IsExtendedKey(vk))
                flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            wVk = 0;          // スキャンコード送信時は VK を 0 にする
            wScan = scan;
        }

        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = wVk,
                    wScan = wScan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
    }

    /// <summary>拡張キー（E0 プレフィックスが必要なキー）か判定する。</summary>
    private static bool IsExtendedKey(ushort vk) => vk switch
    {
        0xA3 or 0xA5 => true,                  // 右Ctrl / 右Alt
        0x5B or 0x5C or 0x5D => true,          // 左Win / 右Win / Apps
        0x21 or 0x22 or 0x23 or 0x24 => true,  // PageUp/PageDown/End/Home
        0x25 or 0x26 or 0x27 or 0x28 => true,  // ←↑→↓
        0x2D or 0x2E => true,                   // Insert / Delete
        0x90 => true,                           // NumLock
        0x6F => true,                           // テンキー /
        0x2C => true,                           // PrintScreen
        _ => false,
    };

    private static void SendMouse(uint flags)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        Send(new[] { input });
    }

    private static InputSendResult Send(NativeMethods.INPUT[] inputs)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, size);
        int lastError = sent == inputs.Length ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        return new InputSendResult((uint)inputs.Length, sent, lastError);
    }
}

/// <summary>
/// SendInput の送信結果。UIPI（対象アプリのみ管理者実行）やフック干渉でキーが破棄されると
/// Sent が Requested を下回る（Win32 エラー 5 = アクセス拒否は権限差の典型）。
/// </summary>
public readonly record struct InputSendResult(uint Requested, uint Sent, int LastError)
{
    public bool AllSent => Sent == Requested;
}
