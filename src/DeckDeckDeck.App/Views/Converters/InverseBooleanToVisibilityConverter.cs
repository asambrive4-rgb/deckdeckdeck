// 역할: 불리언(참/거짓) 값을 반대로 뒤집어 화면 요소를 보이거나 숨기는(Visibility) WPF 변환기입니다.
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeckDeckDeck.App.Views.Converters;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}
