using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace VAdapter.Automation.Ocr;

/// <summary>Windows 標準 OCR（Windows.Media.Ocr）による文字認識。</summary>
public sealed class OcrService
{
    private OcrEngine? _engine;
    private bool _initialized;

    /// <summary>実際に使用している OCR 言語タグ（診断用。未取得時は空）。</summary>
    public string EngineLanguageTag { get; private set; } = string.Empty;

    /// <summary>
    /// 日本語を最優先で OCR エンジンを取得する。
    /// 利用可能な認識言語から "ja" を明示的に選ぶ（英語エンジンが選ばれて漢字を読めない問題を回避）。
    /// </summary>
    private OcrEngine? GetEngine()
    {
        if (_initialized)
            return _engine;

        _initialized = true;
        try
        {
            // 1) 日本語が認識可能なら明示的に日本語で生成。
            var japanese = OcrEngine.AvailableRecognizerLanguages
                .FirstOrDefault(l => l.LanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase));
            if (japanese is not null)
            {
                _engine = OcrEngine.TryCreateFromLanguage(japanese);
                if (_engine is not null)
                    EngineLanguageTag = japanese.LanguageTag;
            }

            // 2) フォールバック（ユーザー既定言語）。
            if (_engine is null)
            {
                _engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (_engine is not null)
                    EngineLanguageTag = _engine.RecognizerLanguage?.LanguageTag ?? "(user profile)";
            }
        }
        catch
        {
            _engine = null;
        }

        return _engine;
    }

    /// <summary>OCR が利用可能か（対応言語パックが導入されているか）。</summary>
    public bool IsAvailable => GetEngine() is not null;

    /// <summary>日本語 OCR が利用可能か。</summary>
    public bool IsJapaneseAvailable
    {
        get
        {
            _ = GetEngine();
            return EngineLanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>PNG 画像から認識テキスト全体を返す。エンジンが無い場合は空文字。</summary>
    public async Task<string> RecognizeAsync(byte[] pngBytes)
    {
        var engine = GetEngine();
        if (engine is null)
            return string.Empty;

        var softwareBitmap = await DecodeAsync(pngBytes).ConfigureAwait(false);
        try
        {
            var result = await engine.RecognizeAsync(softwareBitmap).AsTask().ConfigureAwait(false);
            return result.Text ?? string.Empty;
        }
        finally
        {
            softwareBitmap.Dispose();
        }
    }

    private static async Task<SoftwareBitmap> DecodeAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
