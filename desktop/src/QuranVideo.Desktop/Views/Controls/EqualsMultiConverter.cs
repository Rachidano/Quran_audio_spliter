using System.Globalization;
using System.Windows.Data;

namespace QuranVideo.Desktop.Views.Controls;

/// <summary>Vrai si toutes les valeurs liées sont égales (ex. : ligne du tableau = ayah en cours).</summary>
public sealed class EqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length >= 2 && values.Skip(1).All(v => Equals(v, values[0]));

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
