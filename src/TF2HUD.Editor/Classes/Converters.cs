using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HUDEditor;
using HUDEditor.Classes;

namespace TF2HUDEditor.Classes
{
    public class NullCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return false;
            if (value.GetType() == typeof(string)) return !string.IsNullOrWhiteSpace((string)value);
            if (value.GetType() == typeof(bool)) return value;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullCheckConverterVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullCheckConverterVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }

    public class PageBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var defaultBackground = new BrushConverter().ConvertFromString("#2B2724");

            if (value is null) return defaultBackground;

            var selection = (HUD)value;

            if (selection.Background is null) return defaultBackground;

            if (selection.Background.StartsWith("http"))
            {
                return new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = selection.Opacity,
                    ImageSource = new BitmapImage(new Uri(selection.Background, UriKind.RelativeOrAbsolute))
                };
            }
            else
            {
                // The Background is an RGBA color code, change it to ARGB and set it as the background.
                var colors = Array.ConvertAll(selection.Background.Split(' '), byte.Parse);
                return new SolidColorBrush(Color.FromArgb(colors[^1], colors[0], colors[1], colors[2]));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BtnInstallContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hud = (HUD)value;
            if (hud is not null)
            {
                if (Directory.Exists($"{MainWindow.HudPath}\\{hud.Name}"))
                {
                    MainWindow.Logger.Info($"[BtnInstallContentConverter] {hud.Name} is installed");
                    return Utilities.GetLocalizedString("ui_reinstall") ?? "Reinstall";
                }
                else
                {
                    MainWindow.Logger.Info($"[BtnInstallContentConverter] {hud.Name} is not installed");
                    return Utilities.GetLocalizedString("ui_install") ?? "Install";
                }
            }
            else
            {
                MainWindow.Logger.Info("[BtnInstallContentConverter] Highlighted HUD is null");
                return Utilities.GetLocalizedString("ui_install") ?? "Install";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}