using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SstvVPrinter.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Closing += (_, args) =>
        {
            if (!AllowExplicitClose)
            {
                args.Cancel = true;
                Hide();
            }
        };
    }

    public bool AllowExplicitClose { get; set; }
}
