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
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
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
}
