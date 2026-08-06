using System.IO;
using System.Windows;
using Microsoft.Win32;
using TELLLauncher.Models;

namespace TELLLauncher.Views;

public partial class EditAppDialog : Window
{
    private readonly AppEntry _draft;
    private readonly string _defaultName;

    public EditAppDialog(AppEntry draft, string title, bool lockGroup = false)
    {
        InitializeComponent();
        _draft = draft;
        _defaultName = draft.Name;
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
        _draft.IconPath = string.IsNullOrWhiteSpace(_draft.TargetPath)
            ? _draft.IconPath
            : _draft.TargetPath;
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
