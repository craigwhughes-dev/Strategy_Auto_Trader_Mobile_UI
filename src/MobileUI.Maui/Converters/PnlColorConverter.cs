using System.Globalization;

namespace StrategyTradingAppUI.Maui.Converters;

public class PnlColorConverter : IValueConverter
{
    private static readonly Color ProfitLight = Color.FromArgb("#16A34A");
    private static readonly Color ProfitDark = Color.FromArgb("#4ADE80");
    private static readonly Color LossLight = Color.FromArgb("#DC2626");
    private static readonly Color LossDark = Color.FromArgb("#F87171");
    private static readonly Color NeutralLight = Color.FromArgb("#6B7280");
    private static readonly Color NeutralDark = Color.FromArgb("#9CA3AF");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (value is double pnl)
        {
            if (pnl > 0) return dark ? ProfitDark : ProfitLight;
            if (pnl < 0) return dark ? LossDark : LossLight;
        }
        return dark ? NeutralDark : NeutralLight;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToColorConverter : IValueConverter
{
    private static readonly Color OnlineLight = Color.FromArgb("#16A34A");
    private static readonly Color OnlineDark = Color.FromArgb("#4ADE80");
    private static readonly Color OfflineLight = Color.FromArgb("#DC2626");
    private static readonly Color OfflineDark = Color.FromArgb("#F87171");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (value is bool isRunning)
        {
            if (isRunning) return dark ? OnlineDark : OnlineLight;
            return dark ? OfflineDark : OfflineLight;
        }
        return dark ? Colors.LightGray : Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool running && running ? "Daemon Online" : "Daemon Offline";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }
}

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count > 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
