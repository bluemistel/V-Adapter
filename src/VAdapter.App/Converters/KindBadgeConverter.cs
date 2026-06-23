using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VAdapter.App.Converters;

/// <summary>
/// マクロ種別（"標準"/"ユーザー"）をバッジの配色に変換する。
/// ConverterParameter に "bg" / "fg" を指定。
/// </summary>
public sealed class KindBadgeConverter : IValueConverter
{
    private static readonly SolidColorBrush StandardBg = Freeze("#F1F2F5");
    private static readonly SolidColorBrush StandardFg = Freeze("#6B7382");
    private static readonly SolidColorBrush UserBg = Freeze("#E9F0F9");
    private static readonly SolidColorBrush UserFg = Freeze("#345D9C");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isUser = value as string == "ユーザー";
        var wantFg = parameter as string == "fg";
        return wantFg
            ? (isUser ? UserFg : StandardFg)
            : (isUser ? UserBg : StandardBg);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
