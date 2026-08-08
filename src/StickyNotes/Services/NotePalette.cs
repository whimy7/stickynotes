using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StickyNotes.Services;

public static class NotePalette
{
    public static IReadOnlyList<(string Name, string Color)> Colors { get; } =
    [
        ("便签黄", "#FFF2A8"),
        ("柔和白", "#FFF7E0"),
        ("清新绿", "#DDF4E4"),
        ("浅樱粉", "#FCE1E4"),
        ("天空蓝", "#DDEEFF"),
        ("淡紫色", "#E8E4F8")
    ];

    public static ContextMenu CreateMenu(Action<string> selectColor)
    {
        var menu = new ContextMenu();
        foreach (var (name, color) in Colors)
        {
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                BorderBrush = new SolidColorBrush(Color.FromRgb(195, 198, 204)),
                BorderThickness = new Thickness(1),
                Background = (Brush)new BrushConverter().ConvertFromString(color)!
            };
            var label = new TextBlock
            {
                Text = name,
                Margin = new Thickness(9, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(swatch);
            header.Children.Add(label);

            var item = new MenuItem { Header = header, Tag = color };
            item.Click += (_, _) => selectColor((string)item.Tag);
            menu.Items.Add(item);
        }

        return menu;
    }
}

