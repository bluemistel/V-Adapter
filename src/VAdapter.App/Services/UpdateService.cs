using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using VAdapter.Core.Update;

namespace VAdapter.App.Services;

/// <summary>更新確認の結果。</summary>
public sealed record UpdateResult(bool UpdateAvailable, Version Current, Version? Latest);

/// <summary>
/// GitHub のタグ一覧（API）を取得し、アプリ版と照合して更新の有無を判定する。
/// ダウンロード先は GitHub Releases（将来 Booth 等を追加可能）。
/// ネットワーク不通・レート制限時は静かに失敗（null）。
/// </summary>
public sealed class UpdateService
{
    /// <summary>公開リポジトリのタグ一覧 API。</summary>
    private const string TagsApiUrl = "https://api.github.com/repos/bluemistel/V-Adapter/tags";

    /// <summary>ダウンロード案内先（Releases）。</summary>
    public const string ReleasesUrl = "https://github.com/bluemistel/V-Adapter/releases";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub API は User-Agent 必須。
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("V-Adapter", CurrentVersion().ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    /// <summary>現在のアプリバージョン（アセンブリ）。</summary>
    public static Version CurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// 画面表示用のバージョン文字列（例: "v0.0.5 α"）。
    /// csproj の &lt;Version&gt; から自動生成するため、画面ごとに書き分けて食い違うことがない。
    /// α 表記は InformationalVersion（例 "0.0.5-alpha"）にプレリリース識別子がある場合のみ付ける。
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            var v = CurrentVersion();
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var suffix = informational.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? " α"
                : informational.Contains("beta", StringComparison.OrdinalIgnoreCase) ? " β"
                : "";
            return $"v{v.Major}.{v.Minor}.{v.Build}{suffix}";
        }
    }

    /// <summary>タグ一覧を取得して更新有無を判定する。失敗時は null。</summary>
    public async Task<UpdateResult?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await Http.GetAsync(TagsApiUrl, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var tags = ParseTagNames(json);
            var latest = UpdateCheck.SelectLatest(tags);
            var current = CurrentVersion();
            return new UpdateResult(UpdateCheck.IsUpdateAvailable(current, latest), current, latest);
        }
        catch
        {
            return null; // 不通・タイムアウト・パース失敗は無視。
        }
    }

    private static List<string?> ParseTagNames(string json)
    {
        var names = new List<string?>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return names;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                names.Add(n.GetString());
        }
        return names;
    }
}
