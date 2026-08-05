using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Views;

public partial class GameView : UserControl
{
    private Point _dragStartPoint;

    public GameView()
    {
        InitializeComponent();
    }

    private void ListBox_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not ListBox listBox)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not AppItemViewModel)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _dragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(
            listBox,
            new DataObject(typeof(AppItemViewModel), item.DataContext),
            DragDropEffects.Move);
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AppItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox ||
            !e.Data.GetDataPresent(typeof(AppItemViewModel)) ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var source = e.Data.GetData(typeof(AppItemViewModel)) as AppItemViewModel;
        var target = GetItemAtPoint(listBox, e.GetPosition(listBox)) as AppItemViewModel;
        if (source is null)
        {
            return;
        }

        if (target is null)
        {
            viewModel.MoveToEnd(source);
        }
        else
        {
            viewModel.MoveBefore(source, target);
        }
    }

    private static object? GetItemAtPoint(ListBox listBox, Point point)
    {
        var hit = VisualTreeHelper.HitTest(listBox, point);
        return FindAncestor<ListBoxItem>(hit?.VisualHit)?.DataContext;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
