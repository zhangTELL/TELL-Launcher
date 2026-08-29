using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TELLLauncher.Models;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Views;

public partial class AppDetailWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppItemViewModel _item;

    public AppDetailWindow(
        MainViewModel viewModel,
        AppItemViewModel item)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _item = item;
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

        // 走 ViewModel.Launch 以记录 LastLaunchedAt 并刷新"最近启动"
        _viewModel.Launch(_item);
        Close();
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

    private async void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_item.TargetPath) ||
            sender is not Button button)
        {
            return;
        }

        Clipboard.SetText(_item.TargetPath);
        button.Content = "已复制";
        button.IsEnabled = false;

        // 短暂反馈后恢复原状，避免用户不确定是否复制成功
        await Task.Delay(1500);
        button.Content = "复制路径";
        button.IsEnabled = true;
    }
}
