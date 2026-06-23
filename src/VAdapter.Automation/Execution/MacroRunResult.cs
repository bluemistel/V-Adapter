using VAdapter.Core.Models;

namespace VAdapter.Automation.Execution;

/// <summary>マクロ実行の結果。</summary>
public sealed class MacroRunResult
{
    public bool Success { get; init; }

    /// <summary>失敗時のメッセージ（成功時は null）。</summary>
    public string? Error { get; init; }

    /// <summary>失敗した命令（特定できる場合）。</summary>
    public Instruction? FailedInstruction { get; init; }

    public static MacroRunResult Ok() => new() { Success = true };

    public static MacroRunResult Fail(string error, Instruction? instruction = null) =>
        new() { Success = false, Error = error, FailedInstruction = instruction };
}
