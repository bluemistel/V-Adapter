using System.IO;
using System.Text.RegularExpressions;

namespace VAdapter.Core.Media;

/// <summary>
/// 合成音声ソフトが書き込み途中に作る一時ファイルを判別する。
///
/// 多くのソフトは「一時名で書き切ってから最終名へ改名する」方式を採る。書き込み途中の
/// 半端なファイルを他プロセスに見せないための定石だが、フォルダ監視側から見ると
/// <see cref="FileSystemWatcher"/> は寿命 1ms のファイルでも生成イベントを通知するため、
/// 除外しないと一時ファイルを掴んでしまう競合が起きる。
///
/// 実測（VoiSona Talk 1ブロック書き出し。すべて同一ミリ秒内）:
/// <code>
/// 作成   3_さとうささら_さー_temp97751a69.wav
/// 改名先 3_さとうささら_さー.wav
/// 作成   .3_さとうささら_さー_tempb728ffd1.txt   ← txt は先頭にドットが付く
/// 改名先 3_さとうささら_さー.txt
/// </code>
/// </summary>
public static class TransientFile
{
    /// <summary>
    /// 「_temp」＋16進の識別子で終わる名前。VoiSona Talk が実際に使う形式で、
    /// wav と txt で別々の識別子が振られるため最終名からは逆算できない。
    /// 桁数はバージョンで変わり得るので 6 桁以上を許容する。
    /// </summary>
    private static readonly Regex TempSuffixPattern =
        new(@"_temp[0-9a-f]{6,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>書き込み途中を示す拡張子（V-Adapter 自身の後処理が使う .vadtmp を含む）。</summary>
    private static readonly string[] TempExtensions =
    {
        ".tmp", ".temp", ".part", ".partial", ".crdownload", ".vadtmp",
    };

    /// <summary>
    /// 監視対象から除外すべき一時ファイルかどうかを返す。
    /// 判定は名前だけで行う（一時ファイルは既に消えていることが多く、実体を見に行けないため）。
    /// </summary>
    public static bool IsTransient(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            return false;

        // 先頭ドットは隠しファイル扱いの一時ファイル（VoiSona Talk の txt が該当）。
        // 完成品がドットで始まることはないため、そのまま除外してよい。
        if (name[0] == '.')
            return true;

        // Office 系などが使うロックファイル。
        if (name.StartsWith("~$", StringComparison.Ordinal))
            return true;

        foreach (var ext in TempExtensions)
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return TempSuffixPattern.IsMatch(Path.GetFileNameWithoutExtension(name));
    }
}
