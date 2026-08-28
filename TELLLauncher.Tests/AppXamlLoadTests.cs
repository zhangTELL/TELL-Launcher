using System.Windows;
using System.Windows.Controls;
using TELLLauncher.Views;

namespace TELLLauncher.Tests;

public class AppXamlLoadTests
{
    /// <summary>
    /// 加载 App.xaml，并用其中的 ContextMenu 样式真实构建一个含 Separator 的菜单。
    /// </summary>
    /// <remarks>
    /// 覆盖两类只有在运行时才暴露的 XAML 错误：
    /// 1. StaticResource 前向引用 —— LoadComponent 会直接抛 XamlParseException。
    /// 2. ItemContainerStyle 被套用到 Separator —— 只有 Measure 生成项容器时才抛
    ///    "用于类型 'MenuItem' 的样式不能应用于类型 'Separator'"。
    /// 这两类错误编译期都不报错，因此必须有测试兜住。
    /// </remarks>
    [Fact]
    public void AppResources_LoadAndApplyContextMenuStyleWithSeparator()
    {
        Exception? captured = null;
        ResourceDictionary? resources = null;

        var thread = new Thread(() =>
        {
            try
            {
                var uri = new Uri("/TELL Launcher;component/App.xaml", UriKind.Relative);
                var app = (Application)Application.LoadComponent(uri);
                resources = app.Resources;

                var contextMenuStyle = resources[typeof(ContextMenu)] as Style;
                if (contextMenuStyle is null)
                {
                    captured = new InvalidOperationException("App.xaml 中缺少 ContextMenu 样式");
                    return;
                }

                var menu = new ContextMenu { Style = contextMenuStyle };
                menu.Items.Add(new MenuItem { Header = "详情" });
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem { Header = "移除/隐藏" });

                menu.ApplyTemplate();

                // 强制生成项容器：StyleSelector 在这一步才真正被调用
                menu.Measure(new Size(1000, 1000));
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
        Assert.NotNull(resources);

        var contextMenuStyle = resources[typeof(ContextMenu)] as Style;
        Assert.NotNull(contextMenuStyle);

        var selectorSetter = contextMenuStyle.Setters
            .OfType<Setter>()
            .FirstOrDefault(s => s.Property == ContextMenu.ItemContainerStyleSelectorProperty);

        Assert.NotNull(selectorSetter);
        Assert.IsType<ContextMenuStyleSelector>(selectorSetter.Value);

        var selector = (ContextMenuStyleSelector)selectorSetter.Value;
        Assert.NotNull(selector.MenuItemStyle);
        Assert.NotNull(selector.SeparatorStyle);

        Assert.IsType<Style>(resources["CardContextMenuItemStyle"]);
        Assert.IsType<Style>(resources["CardContextMenuSeparatorStyle"]);
    }
}
