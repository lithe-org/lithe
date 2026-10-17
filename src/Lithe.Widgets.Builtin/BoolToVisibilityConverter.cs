using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Lithe.Widgets.Builtin;

/// <summary>
/// 布尔值转可见性。组件库里不能用 App 的静态资源，所以自带转换器。
/// </summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 转换。
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool boolean && boolean;
        var invert = parameter as string == "Invert";
        return flag != invert ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 反向转换。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility visibility && visibility == Visibility.Visible;
}