using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LensDocsStudio.Native.Controls;

public sealed class HeadingIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Thickness(Math.Max(0, (value is int level ? level : 1) - 1) * 12, 5, 4, 5);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
