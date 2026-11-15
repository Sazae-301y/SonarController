using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SonarController.Helpers;

/// <summary>0.0〜1.0 の音量値をパーセント表示文字列に変換する</summary>
public class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? $"{Math.Round(d * 100)}%" : "0%";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>文字列が null/空でなければ Visible、それ以外は Collapsed（エラーメッセージ表示用）</summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool を反転して Visibility に変換する（Classic専用UIの表示切替用）</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>"#RRGGBB" 形式の文字列を SolidColorBrush に変換する（チャンネルごとのアクセントカラー表示用）</summary>
public class HexToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrEmpty(hex))
            return System.Windows.Media.Brushes.White;

        return BrushCache.GetOrAdd(hex, static key =>
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(key));
            brush.Freeze();
            return brush;
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
