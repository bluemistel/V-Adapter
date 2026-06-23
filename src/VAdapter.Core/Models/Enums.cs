namespace VAdapter.Core.Models;

/// <summary>クリック座標の解釈方法。</summary>
public enum CoordinateMode
{
    /// <summary>対象ウィンドウのクライアント領域左上を基準とした相対座標。</summary>
    Relative = 0,

    /// <summary>ディスプレイ全体を基準とした絶対座標。</summary>
    Absolute = 1,
}

/// <summary>クリックに使用するマウスボタン。</summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

/// <summary>
/// 相対クリックの基準位置。ウィンドウのクライアント領域内のどの隅／中央を原点とするか。
/// 座標 (X,Y) はこのアンカーからのオフセット。ウィンドウサイズが変わっても、
/// 端・中央に配置された要素を追従してクリックできる。
/// </summary>
public enum ClickAnchor
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
    Center = 4,
}

/// <summary>「操作対象の切り替え」命令で指定する送信先ウィンドウの種類。</summary>
public enum SwitchTargetKind
{
    /// <summary>対象アプリ本体のウィンドウへ戻す。</summary>
    AppWindow = 0,

    /// <summary>対象アプリが表示している Windows 標準ダイアログへ移す。</summary>
    Dialog = 1,
}

/// <summary>ショートカット/キー送信の修飾キー（組み合わせ可能）。</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}
