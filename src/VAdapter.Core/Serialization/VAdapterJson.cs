using System.Text.Json;
using System.Text.Json.Serialization;

namespace VAdapter.Core.Serialization;

/// <summary>アプリ共通の JSON シリアライズ設定。</summary>
public static class VAdapterJson
{
    /// <summary>列挙体は文字列、インデント有り、null 省略の共通オプション。</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
