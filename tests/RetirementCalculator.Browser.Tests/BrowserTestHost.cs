using System.Diagnostics;
using System.Text;

namespace RetirementCalculator.Browser.Tests;

[TestClass]
public sealed class BrowserTestHost
{
    public const string BaseUrl = "http://127.0.0.1:5198";

    private static readonly StringBuilder HostOutput = new();
    private static Process? _webProcess;

    public static string GetHostOutput()
    {
        lock (HostOutput)
        {
            return HostOutput.ToString();
        }
    }

    [AssemblyInitialize]
    public static async Task StartWebApplication(TestContext _)
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProjectDirectory = Path.Combine(repositoryRoot, "src", "RetirementCalculator.Web");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the build configuration.");
        var applicationPath = Path.Combine(
            webProjectDirectory,
            "bin",
            configuration,
            "net10.0",
            "RetirementCalculator.Web.dll");

        _webProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{applicationPath}\" --urls {BaseUrl}",
                WorkingDirectory = webProjectDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _webProcess.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        _webProcess.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

        if (!_webProcess.Start())
        {
            throw new InvalidOperationException("Could not start the web application.");
        }

        _webProcess.OutputDataReceived += CaptureHostOutput;
        _webProcess.ErrorDataReceived += CaptureHostOutput;
        _webProcess.BeginOutputReadLine();
        _webProcess.BeginErrorReadLine();

        using var httpClient = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        while (!timeout.IsCancellationRequested)
        {
            if (_webProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"The web application exited with code {_webProcess.ExitCode} before becoming ready.");
            }

            try
            {
                using var response = await httpClient.GetAsync(BaseUrl, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(200, timeout.Token);
        }

        throw new TimeoutException("The web application did not become ready within 30 seconds.");
    }

    [AssemblyCleanup]
    public static void StopWebApplication()
    {
        if (_webProcess is null || _webProcess.HasExited)
        {
            return;
        }

        _webProcess.Kill(entireProcessTree: true);
        _webProcess.Dispose();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RetirementCalculator.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private static void CaptureHostOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (HostOutput)
        {
            HostOutput.AppendLine(eventArgs.Data);
        }
    }
}