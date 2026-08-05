using System.Windows;
using Microsoft.Win32;
using TELLLauncher.Models;

namespace TELLLauncher.Views;

public partial class EditAppDialog : Window
{
    private readonly AppEntry _draft;

    public EditAppDialog(AppEntry draft, string title)
    {
        InitializeComponent();
        _draft = draft;
        Title = title;

        NameTextBox.Text = draft.Name;
        PathTextBox.Text = draft.TargetPath ?? string.Empty;
        GroupComboBox.ItemsSource = new[] { "IDE", "AI 工具", "游戏" };
        GroupComboBox.SelectedIndex = (int)draft.Group;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择程序文件",
            Filter = "程序或快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathTextBox.Text = dialog.FileName;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (GroupComboBox.SelectedIndex < 0)
        {
            return;
        }

        _draft.Name = NameTextBox.Text.Trim();
        _draft.TargetPath = PathTextBox.Text.Trim();
        _draft.IconPath = string.IsNullOrWhiteSpace(_draft.TargetPath)
            ? _draft.IconPath
            : _draft.TargetPath;
        _draft.Group = (AppGroup)GroupComboBox.SelectedIndex;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
