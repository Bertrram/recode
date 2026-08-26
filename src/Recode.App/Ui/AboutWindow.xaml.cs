using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Recode.Core.Diagnostics;

namespace Recode.App.Ui;

/// <summary>
/// The support window: which formats work, and which backend handles each one.
/// </summary>
/// <remarks>
/// In a healthy installation every row is green, because WIC is part of Windows
/// and everything else ships beside the executable. That is the intended
/// reading of this window: confirmation, not a to do list. It never suggests
/// installing anything.
///
/// When a bundled library is missing, which realistically means a copy that
/// went wrong, the window names the file and the folder it belongs in.
/// </remarks>
public partial class AboutWindow : Window
{
    public AboutWindow(SupportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        InitializeComponent();
        Theme.Apply(this);

        FormatList.ItemsSource = report.Rows.Select(SupportRowView.From).ToList();

        SubtitleText.Text = report.Healthy
            ? "Everything below is bundled. Nothing else to install."
            : "Some bundled files are missing.";

        var problems = report.BrokenBackends
            .Select(DescribeBackendProblem)
            .ToList();

        if (problems.Count > 0)
        {
            ProblemList.ItemsSource = problems;
            ProblemPanel.Visibility = Visibility.Visible;
        }

        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version}";
        RepositoryLink.NavigateUri = new Uri(AppInfo.RepositoryUrl);
        RepositoryLinkText.Text = "GitHub";
    }

    private static string DescribeBackendProblem(BackendStatus backend)
    {
        if (backend.MissingLibrary is not null && backend.ExpectedLocation is not null)
        {
            return $"{backend.MissingLibrary} belongs in {backend.ExpectedLocation}";
        }

        return $"{backend.DisplayName}: {backend.Problem ?? "unavailable"}";
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            // UseShellExecute so the user's default browser opens it, rather
            // than the runtime trying to execute a URL as a program.
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser, or the user cancelled the association prompt. Not
            // worth an error dialog.
        }

        e.Handled = true;
    }
}
