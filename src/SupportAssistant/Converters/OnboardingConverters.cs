using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SupportAssistant.Converters;

/// <summary>
/// Converter for step numbers to colors in the onboarding wizard
/// </summary>
public class StepToColorConverter : IValueConverter
{
    public static readonly StepToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepStr && int.TryParse(stepStr, out int step))
        {
            return currentStep >= step ? Brushes.Green : Brushes.Gray;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for equality comparison
/// </summary>
public class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out int paramValue))
        {
            return intValue == paramValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for status messages to colors
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) || 
                message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return Brushes.Red;
            }
            if (message.Contains("valid", StringComparison.OrdinalIgnoreCase) || 
                message.Contains("success", StringComparison.OrdinalIgnoreCase))
            {
                return Brushes.Green;
            }
        }
        return Brushes.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
