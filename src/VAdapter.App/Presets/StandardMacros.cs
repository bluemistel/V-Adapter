using VAdapter.Core.Models;

namespace VAdapter.App.Presets;

/// <summary>
/// 標準マクロ（音声の再生 / 音声の保存）のプリセット。アプリ作者が内部スクリプトを構築する。
/// 初回起動時にライブラリへシードする。
/// 既定では「共通（対象アプリ未割当）」スクリプトを 1 つ持ち、ユーザーは対象アプリごとの
/// スクリプトを追加して使い分ける。
/// </summary>
public static class StandardMacros
{
    private const int VK_SPACE = 0x20;
    private const int VK_E = 0x45;

    /// <summary>音声の再生（トリガ: Ctrl+Space）。</summary>
    public static Macro CreatePlayMacro() => new()
    {
        Name = "音声の再生",
        IsBuiltIn = true,
        Shortcut = new KeyCombination(KeyModifiers.Control, VK_SPACE, "Space"),
        Scripts =
        {
            new MacroScript
            {
                TargetApplicationId = null, // 共通（フォールバック）
                Instructions =
                {
                    new WaitForActivationInstruction { TimeoutMs = 3000 },
                    new SendKeysInstruction
                    {
                        KeyCombination = new KeyCombination(KeyModifiers.Control, VK_SPACE, "Space"),
                    },
                },
            },
        },
    };

    /// <summary>音声の保存（トリガ: Ctrl+E）。</summary>
    public static Macro CreateSaveMacro() => new()
    {
        Name = "音声の保存",
        IsBuiltIn = true,
        Shortcut = new KeyCombination(KeyModifiers.Control, VK_E, "E"),
        Scripts =
        {
            new MacroScript
            {
                TargetApplicationId = null, // 共通（フォールバック）
                Instructions =
                {
                    new WaitForActivationInstruction { TimeoutMs = 3000 },
                    new SendKeysInstruction
                    {
                        KeyCombination = new KeyCombination(KeyModifiers.Control, VK_E, "E"),
                    },
                    new WaitForWindowInstruction { TimeoutMs = 5000 },
                },
            },
        },
    };

    /// <summary>
    /// 同梱する対象アプリのテンプレート。ユーザーは「対象アプリ管理」で自身の環境に合わせて
    /// （プロセス名やウィンドウ条件を）確認・登録するとマクロを利用できる。
    /// プロセス名は一般的な既定値の目安（環境により異なる場合は実行中ウィンドウから取得で調整）。
    /// </summary>
    public static IReadOnlyList<TargetApplication> CreateTargetTemplates() => new[]
    {
        new TargetApplication { Name = "VOICEVOX", ProcessName = "VOICEVOX", CoordinateMode = CoordinateMode.Relative },
        new TargetApplication { Name = "A.I.VOICE2", ProcessName = "AIVoice2Editor", CoordinateMode = CoordinateMode.Relative },
        new TargetApplication { Name = "CeVIO AI", ProcessName = "CeVIO AI", CoordinateMode = CoordinateMode.Relative },
        new TargetApplication { Name = "VOICEPEAK", ProcessName = "voicepeak", CoordinateMode = CoordinateMode.Relative },
        new TargetApplication { Name = "VoiSona Talk", ProcessName = "VoiSona", CoordinateMode = CoordinateMode.Relative },
    };

    /// <summary>ライブラリが空のとき、対象アプリテンプレートと標準マクロを投入する。</summary>
    public static void SeedInto(MacroLibrary library)
    {
        library.Targets.AddRange(CreateTargetTemplates());
        library.Macros.Add(CreatePlayMacro());
        library.Macros.Add(CreateSaveMacro());
    }
}
