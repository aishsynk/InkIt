using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Win32;

namespace ScreenCanvas.Support;

/// <summary>
/// Keeps one InkIt per Windows session. A second launch (a shortcut, an inkit:// link, a double-clicked .inkit file,
/// a Stream Deck button) passes its request to the running InkIt over a local pipe and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Name = $"InkIt-{Environment.UserName}-{System.Diagnostics.Process.GetCurrentProcess().SessionId}";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    public bool IsFirst { get; }

    /// <summary>Raised on a background thread with the arguments another launch passed on.</summary>
    public event Action<string[]>? ArgumentsReceived;

    public SingleInstance()
    {
        _mutex = new Mutex(true, @"Local\" + Name, out var createdNew);
        IsFirst = createdNew;
    }

    /// <summary>Hands the arguments to the running InkIt. Returns false if it could not be reached.</summary>
    public static bool Forward(string[] args)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            pipe.Connect(2000);
            var payload = Encoding.UTF8.GetBytes(string.Join('\n', args.Length == 0 ? ["--show"] : args));
            pipe.Write(payload);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { return false; }
    }

    public void Listen()
    {
        var thread = new Thread(() =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    pipe.WaitForConnectionAsync(_stop.Token).GetAwaiter().GetResult();
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    var text = reader.ReadToEnd();
                    ArgumentsReceived?.Invoke(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
                catch (OperationCanceledException) { return; }
                catch (IOException) { Thread.Sleep(200); }
            }
        }) { IsBackground = true, Name = "InkIt single-instance pipe" };
        thread.Start();
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

/// <summary>Registers inkit:// links and .inkit files for the current user when they are not registered yet.</summary>
public static class ShellRegistration
{
    public static void EnsureRegistered()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        var command = $"\"{exe}\" \"%1\"";
        try
        {
            using (var protocol = Registry.CurrentUser.CreateSubKey(@"Software\Classes\inkit"))
            {
                if (protocol.GetValue("URL Protocol") is null)
                {
                    protocol.SetValue(string.Empty, "URL:InkIt command");
                    protocol.SetValue("URL Protocol", string.Empty);
                    using var icon = protocol.CreateSubKey("DefaultIcon");
                    icon.SetValue(string.Empty, $"\"{exe}\",0");
                    using var open = protocol.CreateSubKey(@"shell\open\command");
                    open.SetValue(string.Empty, command);
                }
            }
            using (var extension = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.inkit"))
            {
                if (extension.GetValue(string.Empty) is null) extension.SetValue(string.Empty, "InkIt.Drawings");
            }
            using (var type = Registry.CurrentUser.CreateSubKey(@"Software\Classes\InkIt.Drawings"))
            {
                if (type.GetValue(string.Empty) is null)
                {
                    type.SetValue(string.Empty, "InkIt drawings");
                    using var icon = type.CreateSubKey("DefaultIcon");
                    icon.SetValue(string.Empty, $"\"{exe}\",0");
                    using var open = type.CreateSubKey(@"shell\open\command");
                    open.SetValue(string.Empty, command);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
    }
}
