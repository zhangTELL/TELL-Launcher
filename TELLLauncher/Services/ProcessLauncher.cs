using System.Diagnostics;
using System.IO;

namespace TELLLauncher.Services;

public sealed record LaunchResult(bool Success, string Message);

public interface IProcessLauncher
{
    LaunchResult Launch(string path);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public LaunchResult Launch(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new LaunchResult(false, "程序路径为空");
        }

        if (!IsUriTarget(path) && !File.Exists(path))
        {
            return new LaunchResult(false, "程序文件不存在");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            return new LaunchResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, ex.Message);
        }
    }

    /// <summary>
    /// 判断目标是否为 URI（如 https://、steam://），此类目标交由 Shell 打开，无需检查文件存在。
    /// </summary>
    public static bool IsUriTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Windows 路径形如 C:\...，其冒号后不是 "//"，以此区分 URI 方案
        var schemeSeparatorIndex = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex <= 0)
        {
            return false;
        }

        return path.Take(schemeSeparatorIndex).All(char.IsLetterOrDigit);
    }
}
