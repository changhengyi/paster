using System.IO;
using System.Windows;

namespace Paster;

public partial class SettingsWindow : Window
{
    /// <summary>保存路径时使用；若为 null，表示仅从文件载入，不写 settings。</summary>
    public string? AcceptedClipsJsonPath { get; private set; }

    /// <summary>true：已保存到 settings；“仅从文件载入”为 false。</summary>
    public bool PersistedSettings { get; private set; }

    private bool _suppressStartupToggle;

    public SettingsWindow(string currentClipsPath)
    {
        InitializeComponent();
        PathInput.Text = currentClipsPath;
        _suppressStartupToggle = true;
        StartWithWindowsCheckBox.IsChecked = StartupRegistration.IsEnabled();
        _suppressStartupToggle = false;
        Loaded += (_, _) =>
        {
            PathInput.Focus();
            PathInput.CaretIndex = PathInput.Text.Length;
        };
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressStartupToggle || StartWithWindowsCheckBox.IsChecked is not bool enabled)
        {
            return;
        }

        try
        {
            StartupRegistration.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            StartWithWindowsCheckBox.Checked -= StartWithWindowsCheckBox_Changed;
            StartWithWindowsCheckBox.Unchecked -= StartWithWindowsCheckBox_Changed;
            StartWithWindowsCheckBox.IsChecked = StartupRegistration.IsEnabled();
            StartWithWindowsCheckBox.Checked += StartWithWindowsCheckBox_Changed;
            StartWithWindowsCheckBox.Unchecked += StartWithWindowsCheckBox_Changed;

            System.Windows.MessageBox.Show(
                this,
                $"无法设置开机启动：{ex.Message}",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static string? NormalizeClipsPathOrNull(string? raw)
    {
        var t = raw?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(t);
        }
        catch
        {
            return null;
        }
    }

    private bool TryNormalizePath(out string path)
    {
        path = string.Empty;
        var n = NormalizeClipsPathOrNull(PathInput.Text);
        if (n is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "路径无效，请检查后重试。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            PathInput.Focus();
            return false;
        }

        path = n;
        return true;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 clips.json",
            Filter = "JSON (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = false
        };
        var normalized = NormalizeClipsPathOrNull(PathInput.Text);
        if (normalized is not null)
        {
            try
            {
                dlg.FileName = Path.GetFileName(normalized);
                dlg.InitialDirectory = Path.GetDirectoryName(normalized)
                    ?? AppSettingsStore.PasterAppDataRoot;
            }
            catch
            {
                dlg.InitialDirectory = AppSettingsStore.PasterAppDataRoot;
            }
        }
        else
        {
            dlg.InitialDirectory = AppSettingsStore.PasterAppDataRoot;
        }

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        PathInput.Text = dlg.FileName;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNormalizePath(out var path))
        {
            return;
        }

        AppSettingsStore.Save(new AppSettingsData { ClipsJsonPath = path });
        AcceptedClipsJsonPath = path;
        PersistedSettings = true;
        DialogResult = true;
        Close();
    }

    private void LoadOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNormalizePath(out var path))
        {
            return;
        }

        AcceptedClipsJsonPath = path;
        PersistedSettings = false;
        DialogResult = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
