using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace TELLLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>一次会话内最多弹出的异常对话框数量。</summary>
    private const int MaxDialogs = 3;

    private static readonly object Sync = new();
    private static readonly HashSet<string> LoggedSignatures = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ReportedSignatures = new(StringComparer.Ordinal);
    private static int _dialogCount;
    private static int _suppressedCount;

    /// <summary>最近一次由 Dispatcher 捕获的异常，用于诊断主窗口创建失败。</summary>
    private static Exception? _lastDispatcherException;

    protected override void OnStartup(StartupEventArgs e)
    {
        // async void 事件处理器中的异常会直接抛到 Dispatcher，没有处理者时进程会直接退出。
        // 先注册，这样 base.OnStartup 内部创建窗口时的异常也能被兜住。
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            base.OnStartup(e);

            // 不再依赖 App.xaml 的 StartupUri：被覆盖的 OnStartup 里，
            // WPF 对 StartupUri 的隐式处理并不总是可靠，已两次出现 MainWindow 为 null。
            // 这里显式创建并显示主窗口，同时把异常完整暴露给日志和弹窗。
            var mainWindow = new MainWindow();
            mainWindow.Show();
            MainWindow = mainWindow;
        }
        catch (Exception ex)
        {
            // 窗口构造/资源加载失败：此时还没有任何窗口，必须显式退出，
            // 否则进程会空转成一个看不见的应用。
            WriteCrashLog(ex);
            MessageBox.Show(
                $"启动失败，已记录到 crash.log：\n\n{ex.Message}",
                "TELL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _lastDispatcherException = e.Exception;
        WriteCrashLog(e.Exception);

        if (ShouldShowDialog(e.Exception))
        {
            MessageBox.Show(
                $"出现未处理的异常，已记录到 crash.log：\n\n{e.Exception.Message}\n\n" +
                "相同的异常只提示一次，后续重复将仅写入日志。",
                "TELL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        else
        {
            IncrementSuppressed();
        }

        // 标记为已处理，避免进程直接退出
        e.Handled = true;
    }

    private static void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        // 非 UI 线程的致命异常，进程即将退出，此时只能尽力留下线索
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrashLog(exception);
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);

        // 声明已观察，避免因后台封面加载失败而终止进程
        e.SetObserved();
    }

    /// <summary>
    /// 渲染循环里反复抛出的同一异常会弹出成堆的对话框，这里按签名去重并限制总数。
    /// </summary>
    private static bool ShouldShowDialog(Exception exception)
    {
        var signature = GetSignature(exception);

        lock (Sync)
        {
            // 同一签名只弹一次：例如 XAML 资源缺失时，每个卡片实例化都会抛出同样的异常
            if (!ReportedSignatures.Add(signature))
            {
                return false;
            }

            if (_dialogCount >= MaxDialogs)
            {
                return false;
            }

            _dialogCount++;
            return true;
        }
    }

    private static void IncrementSuppressed()
    {
        lock (Sync)
        {
            _suppressedCount++;
        }
    }

    private static string GetSignature(Exception exception)
    {
        var firstFrame = exception.StackTrace?
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        return $"{exception.GetType().FullName}|{exception.Message}|{firstFrame}";
    }

    private static void WriteCrashLog(Exception exception)
    {
        var signature = GetSignature(exception);

        // 同一异常只写一次完整堆栈，避免渲染循环把 crash.log 瞬间写满
        lock (Sync)
        {
            if (!LoggedSignatures.Add(signature))
            {
                return;
            }
        }

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TELL Launcher");
            Directory.CreateDirectory(directory);

            var builder = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]")
                .AppendLine(exception.ToString())
                .AppendLine();

            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                builder.ToString());
        }
        catch
        {
            // 日志写入失败不应再次引发异常
        }
    }
}
