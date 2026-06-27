using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Integration;

/// <summary>
/// 参照実装: ワンショット実行型の外部アダプタ。
/// ペイロードを一時 <c>payload.json</c>（UTF-8）へ書き出し、コマンドテンプレートを実行する。
/// 終了コード 0 を成功とし、stdout に <c>{ "success": bool, "message": string }</c> があればそれを優先する。
/// 編集ソフト固有の知識を一切持たず、疎結合の成立を検証可能にする。
/// </summary>
public sealed class ExternalCommandAdapter : IImportAdapter
{
    private readonly string _commandTemplate;
    private readonly int _timeoutMs;

    public ExternalCommandAdapter(string commandTemplate, int timeoutMs)
    {
        _commandTemplate = commandTemplate ?? string.Empty;
        _timeoutMs = timeoutMs > 0 ? timeoutMs : 15000;
    }

    public string Id => "external-command";
    public string DisplayName => "外部アダプタ（コマンド）";

    public AdapterStatus GetStatus()
    {
        if (string.IsNullOrWhiteSpace(_commandTemplate))
            return new AdapterStatus { Available = false, Summary = "外部アダプタ: コマンド未設定" };
        return new AdapterStatus
        {
            Available = true,
            Summary = "外部アダプタ: コマンド設定済み",
            TargetInfo = _commandTemplate,
        };
    }

    public ImportResult Import(DropPayload payload)
    {
        if (string.IsNullOrWhiteSpace(_commandTemplate))
            return ImportResult.Fail("外部アダプタのコマンドが設定されていません。");

        string? jsonPath = null;
        try
        {
            jsonPath = WritePayloadFile(payload);
            var (fileName, arguments) = ExternalCommandLine.Build(_commandTemplate, jsonPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return ImportResult.Fail("外部アダプタのコマンドが不正です。");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return ImportResult.Fail("外部アダプタのプロセスを起動できませんでした。");

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(_timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* 既に終了 */ }
                return ImportResult.Fail($"外部アダプタがタイムアウトしました（{_timeoutMs}ms）。");
            }

            // stdout に結果 JSON があれば優先採用。
            if (TryParseResult(stdout, out var ok, out var message))
                return ok ? ImportResult.Ok(message) : ImportResult.Fail(message ?? "外部アダプタが失敗を返しました。");

            return proc.ExitCode == 0
                ? ImportResult.Ok()
                : ImportResult.Fail($"外部アダプタが異常終了しました（exit {proc.ExitCode}）。{Trim(stderr)}");
        }
        catch (Exception ex)
        {
            return ImportResult.Fail($"外部アダプタの実行に失敗しました: {ex.Message}");
        }
        finally
        {
            if (jsonPath is not null)
            {
                try { File.Delete(jsonPath); } catch { /* 後始末失敗は無視 */ }
            }
        }
    }

    private static string WritePayloadFile(DropPayload payload)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vadapter-payload-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, VAdapterJson.Serialize(payload), new UTF8Encoding(false));
        return path;
    }

    /// <summary>stdout の末尾にある JSON オブジェクトを result として解釈する（無ければ false）。</summary>
    private static bool TryParseResult(string stdout, out bool success, out string? message)
    {
        success = false;
        message = null;
        if (string.IsNullOrWhiteSpace(stdout))
            return false;

        // 最後の '{' から末尾の '}' までを JSON とみなす（ログ行が前置されていても拾える）。
        var start = stdout.LastIndexOf('{');
        var end = stdout.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(stdout.Substring(start, end - start + 1));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var s))
                return false;
            success = s.ValueKind == JsonValueKind.True;
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                message = m.GetString();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();
}
