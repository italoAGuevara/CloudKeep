using System.Diagnostics;
using System.Runtime.InteropServices;
using HostedService.Entities;
using HostedService.Scripts;
using Microsoft.Extensions.Options;
using Moq;

namespace HostedService.Tests;

public class ScriptRunnerTests
{
    /// <summary>
    /// <see cref="ScriptRunner.RunAsync"/> always resolves a Node path before building process start info,
    /// even for .cmd/.bat; tests place a placeholder file where the runner expects the bundled binary.
    /// </summary>
    private static void EnsurePlaceholderBundledNode()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "runtime", "node");
        Directory.CreateDirectory(dir);
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);
    }

    private static ScriptRunner CreateSut(Mock<IScriptExecutionTimeoutProvider>? timeoutMock = null)
    {
        EnsurePlaceholderBundledNode();
        var mock = timeoutMock ?? new Mock<IScriptExecutionTimeoutProvider>();
        mock
            .Setup(m => m.GetScriptExecutionTimeoutMinutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        return new ScriptRunner(Options.Create(new ScriptRunnerOptions()), mock.Object);
    }

    [Fact]
    public async Task RunAsync_throws_when_script_is_null()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunAsync(null!));
    }

    [Fact]
    public async Task RunAsync_throws_when_file_missing()
    {
        var sut = CreateSut();
        var missing = Path.Combine(Path.GetTempPath(), $"no_script_{Guid.NewGuid():N}.cmd");
        var script = new ScriptConfiguration { ScriptPath = missing, Tipo = ".cmd" };
        await Assert.ThrowsAsync<FileNotFoundException>(() => sut.RunAsync(script));
    }

    [Fact]
    public async Task RunAsync_throws_for_unsupported_extension()
    {
        var sut = CreateSut();
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"sr_txt_{Guid.NewGuid():N}"));
        var path = Path.Combine(dir.FullName, "noop.txt");
        await File.WriteAllTextAsync(path, "x");
        try
        {
            var script = new ScriptConfiguration { ScriptPath = path, Tipo = "" };
            await Assert.ThrowsAsync<NotSupportedException>(() => sut.RunAsync(script));
        }
        finally
        {
            try
            {
                dir.Delete(true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task RunAsync_executes_cmd_and_returns_exit_code_on_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"sr_cmd_{Guid.NewGuid():N}"));
        var cmdPath = Path.Combine(dir.FullName, "exit42.cmd");
        await File.WriteAllTextAsync(cmdPath, "@exit /b 42\r\n");

        try
        {
            var sut = CreateSut();
            var script = new ScriptConfiguration { ScriptPath = cmdPath, Tipo = ".cmd", Arguments = "" };
            var result = await sut.RunAsync(script);
            Assert.Equal(42, result.ExitCode);
        }
        finally
        {
            try
            {
                dir.Delete(true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task RunAsync_kills_script_when_cancellation_is_requested_on_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"sr_cancel_{Guid.NewGuid():N}"));
        var pidPath = Path.Combine(dir.FullName, "pid.txt");
        var scriptPath = Path.Combine(dir.FullName, "sleep.ps1");
        await File.WriteAllTextAsync(
            scriptPath,
            "$PID | Set-Content -Path $args[0]\r\nStart-Sleep -Seconds 30\r\n");

        try
        {
            var sut = CreateSut();
            var script = new ScriptConfiguration { ScriptPath = scriptPath, Tipo = ".ps1", Arguments = pidPath };
            using var cts = new CancellationTokenSource();

            var runTask = sut.RunAsync(script, cts.Token);
            var pid = await WaitForPidAsync(pidPath);

            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            await WaitUntilProcessExitsAsync(pid);
        }
        finally
        {
            try
            {
                dir.Delete(true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private static async Task<int> WaitForPidAsync(string pidPath)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            if (File.Exists(pidPath)
                && int.TryParse((await File.ReadAllTextAsync(pidPath, timeout.Token)).Trim(), out var pid))
            {
                return pid;
            }

            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException("El script no escribió su PID a tiempo.");
    }

    private static async Task WaitUntilProcessExitsAsync(int pid)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return;
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException($"El proceso {pid} siguió vivo después de cancelar el script.");
    }
}
