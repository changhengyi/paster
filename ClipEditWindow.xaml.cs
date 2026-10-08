using System.Windows;

namespace Paster;

public partial class ClipEditWindow : Window
{
    public string DialogTitle
    {
        get => DialogTitleBlock.Text;
        set => DialogTitleBlock.Text = value;
    }

    public string TitleText
    {
        get => TitleInput.Text;
        set => TitleInput.Text = value;
    }

    public string ContentText
    {
        get => ContentInput.Text;
        set => ContentInput.Text = value;
    }

    public ClipEditWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ContentInput.Focus();
            ContentInput.SelectAll();
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ContentInput.Text))
        {
            System.Windows.MessageBox.Show(this, "内容不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ContentInput.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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
