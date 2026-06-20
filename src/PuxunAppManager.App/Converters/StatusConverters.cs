using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.App.Converters;

/// <summary>把产品状态映射为徽章配色。参数："bg" 背景 / "fg" 前景&圆点。</summary>
public sealed class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is ProductStatus s ? s : ProductStatus.Unknown;
        bool bg = string.Equals(parameter as string, "bg", StringComparison.OrdinalIgnoreCase);

        // (foreground, background)
        var (fg, bgc) = status switch
        {
            ProductStatus.UpToDate => ("#0f8a4f", "#e6f4ec"),
            ProductStatus.UpdateAvailable => ("#0067c0", "#e5f0fb"),
            ProductStatus.Broken => ("#b9610a", "#fbeede"),
            ProductStatus.NotInstalled => ("#c42b1c", "#fde7e4"),
            _ => ("#5f6368", "#eceef0")
        };
        return new SolidColorBrush(Color.Parse(bg ? bgc : fg));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
