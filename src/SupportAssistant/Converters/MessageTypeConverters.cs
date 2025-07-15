using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SupportAssistant.Models;

namespace SupportAssistant.Converters;

/// <summary>
/// Converts ChatMessageType to background color
/// </summary>
public class MessageTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatMessageType messageType)
        {
            return messageType switch
            {
                ChatMessageType.User => new SolidColorBrush(Color.FromRgb(227, 242, 253)), // Light blue
                ChatMessageType.Assistant => new SolidColorBrush(Color.FromRgb(245, 245, 245)), // Light gray
                ChatMessageType.System => new SolidColorBrush(Color.FromRgb(255, 243, 224)), // Light orange
                _ => new SolidColorBrush(Colors.White)
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ChatMessageType to horizontal alignment
/// </summary>
public class MessageTypeToAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatMessageType messageType)
        {
            return messageType switch
            {
                ChatMessageType.User => HorizontalAlignment.Right,
                ChatMessageType.Assistant => HorizontalAlignment.Left,
                ChatMessageType.System => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Left
            };
        }
        return HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ChatMessageType to max width
/// </summary>
public class MessageTypeToWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatMessageType messageType)
        {
            return messageType switch
            {
                ChatMessageType.User => 300.0,
                ChatMessageType.Assistant => 450.0,
                ChatMessageType.System => 350.0,
                _ => 400.0
            };
        }
        return 400.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ChatMessageType to show header visibility
/// </summary>
public class MessageTypeToShowHeaderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatMessageType messageType)
        {
            return messageType == ChatMessageType.Assistant; // Only show header for assistant messages
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to boolean indicating if string is not empty
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value?.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
