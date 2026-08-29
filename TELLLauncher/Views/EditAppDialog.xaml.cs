using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Views;

public partial class EditAppDialog : Window
{
    private readonly AppEntry _draft;
    private readonly string _defaultName;
    private readonly string? _originalTargetPath;

    public EditAppDialog(AppEntry draft, string title, bool lockGroup = false)
    {
        InitializeComponent();
        _draft = draft;
        _defaultName = draft.Name;
        _originalTargetPath = draft.TargetPath;
        Title = title;

        NameTextBox.Text = draft.Name;
        PathTextBox.Text = draft.TargetPath ?? string.Empty;
        DetailImageTextBox.Text = draft.DetailImagePath ?? string.Empty;
        DetailsTextBox.Text = draft.Details ?? string.Empty;
        GroupComboBox.ItemsSource = new[] { "IDE", "AI 工具", "游戏" };
        GroupComboBox.SelectedIndex = (int)draft.Group;

        if (lockGroup)
        {
            GroupPanel.Visibility = Visibility.Collapsed;
        }

        WindowHelper.EnableDarkTitleBar(this);
    }

    private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var path = PathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) ||
            ProcessLauncher.IsUriTarget(path) ||
            File.Exists(path))
        {
            PathWarningText.Visibility = Visibility.Collapsed;
            return;
        }

        PathWarningText.Text = "该路径当前不存在，保存后应用会标记为“未定位”；不影响保存";
        PathWarningText.Visibility = Visibility.Visible;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择程序文件",
            Filter = "程序或快捷方式 (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathTextBox.Text = dialog.FileName;

            if (NameTextBox.Text == _defaultName
                || string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择页面图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.avif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.avif;*.tif;*.tiff|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            DetailImageTextBox.Text = dialog.FileName;
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

        // 只有当图标此前就是"跟随程序路径"时，才让它跟着新的路径走。
        // 无条件改写会覆盖掉用户为条目单独指定的图标。
        if (string.IsNullOrWhiteSpace(_draft.IconPath) ||
            string.Equals(
                _draft.IconPath,
                _originalTargetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            _draft.IconPath = _draft.TargetPath;
        }
        _draft.DetailImagePath = DetailImageTextBox.Text.Trim();
        _draft.Details = DetailsTextBox.Text.Trim();
        _draft.Group = (AppGroup)GroupComboBox.SelectedIndex;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
