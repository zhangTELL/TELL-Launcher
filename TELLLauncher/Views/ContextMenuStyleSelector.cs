using System.Windows;
using System.Windows.Controls;

namespace TELLLauncher.Views;

/// <summary>
/// 右键菜单的项容器样式选择器。
/// </summary>
/// <remarks>
/// <see cref="ItemsControl.ItemContainerStyle"/> 会应用到 <b>所有</b> 项容器，
/// 包括 <see cref="Separator"/>。若把 TargetType 为 <see cref="MenuItem"/> 的样式
/// 直接赋给 ItemContainerStyle，打开含分隔线的菜单会抛
/// "用于类型 'MenuItem' 的样式不能应用于类型 'Separator'"。
/// 因此这里按容器类型分别返回样式。
/// </remarks>
public sealed class ContextMenuStyleSelector : StyleSelector
{
    public Style? MenuItemStyle { get; set; }

    public Style? SeparatorStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container)
    {
        return container is Separator ? SeparatorStyle : MenuItemStyle;
    }
}
