// 역할: 이미지 파일 경로나 바이트 배열을 WPF 화면에서 바인딩할 수 있는 이미지 소스로 변환하고 캐시합니다.
using System.Globalization;
using System.Windows.Data;
using DeckDeckDeck.App.Views.Imaging;

namespace DeckDeckDeck.App.Views.Converters;

public sealed class CachedImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && !string.IsNullOrWhiteSpace(path)
            ? FrozenImageCache.GetOrLoad(path, FrozenImageCache.ParseDecodePixelWidth(parameter))
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    public static void PrewarmFiles(IEnumerable<string?> paths, int decodePixelWidth) =>
        FrozenImageCache.PrewarmFiles(paths, decodePixelWidth);
}
