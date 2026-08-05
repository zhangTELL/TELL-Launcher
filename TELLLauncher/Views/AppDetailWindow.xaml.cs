using System.Windows;
using TELLLauncher.Models;
using TELLLauncher.Services;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Views;

public partial class AppDetailWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppItemViewModel _item;
    private readonly IProcessLauncher _processLauncher;

    public AppDetailWindow(
        MainViewModel viewModel,
        AppItemViewModel item,
        IProcessLauncher? processLauncher = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _item = item;
        _processLauncher = processLauncher ?? new ProcessLauncher();
        DataContext = item;
        WindowHelper.EnableDarkTitleBar(this);
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            $"确定要启动 {_item.Name} 吗？",
            "确认启动",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _processLauncher.Launch(_item.TargetPath!);
        if (!result.Success)
        {
            MessageBox.Show(
                this,
                $"启动失败：{result.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var model = _item.Model;
        var draft = new AppEntry
        {
            Id = model.Id,
            Name = model.Name,
            TargetPath = model.TargetPath,
            IconPath = model.IconPath,
            DetailImagePath = model.DetailImagePath,
            Details = model.Details,
            Group = model.Group,
            Order = model.Order,
            IsHidden = model.IsHidden,
            IsManual = model.IsManual
        };

        var dialog = new EditAppDialog(draft, "编辑应用")
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.UpdateApp(_item, draft);
            _item.Refresh();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
