using System.Windows;

namespace Recode.App.Ui;

/// <summary>
/// Reports failures that happened with no console to print them to.
/// </summary>
/// <remarks>
/// Conversions started from the context menu have nowhere to write. Without
/// this window a file that failed to convert would simply not appear, and the
/// user would be left guessing. It is only shown when something went wrong; a
/// successful conversion is silent.
/// </remarks>
public partial class MessageWindow : Window
{
    public MessageWindow(string heading, IReadOnlyList<string> details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentNullException.ThrowIfNull(details);

        InitializeComponent();
        Theme.Apply(this);

        HeadingText.Text = heading;
        DetailList.ItemsSource = details;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
