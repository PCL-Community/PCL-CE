using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.IO.Pipes;

public class PipeServer(
    string pipeName,
    string identifier,
    bool stopWhenException,
    Func<StreamReader, StreamWriter, Process?, bool> loopCallback,
    Action<PipeServer>? stopCallback = null,
    int[]? allowedProcessId = null)
{
    public string PipeName { get; } = pipeName;
    public string Identifier { get; } = identifier;

    public NamedPipeServerStream PipeServerStream { get; } = new(pipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.None,
        1024,
        1024);

    private Func<StreamReader, StreamWriter, Process?, bool> _LoopCallback { get; } = loopCallback;
    private Action<PipeServer>? _StopCallback { get; } = stopCallback;
    private int[] _AllowedProcessId { get; } = allowedProcessId ?? [];

    private string _ThreadName => $"PipeServer/{Identifier}";
    private bool _isConnected = false;

    #region Constant

    public static readonly Encoding PipeEncoding = Encoding.UTF8;
    public const char PipeEndingChar = (char)27; // '\e' (Escape)

    #endregion

    public void Start()
    {
        Task.Run(_WorkflowAsync);
    }

    private async Task _WorkflowAsync()
    {
        _LogDebug($"{PipeName} has been launched on thread '{_ThreadName}'");

        var hasNextLoop = true;

        try
        {
            while (hasNextLoop)
            {
                hasNextLoop = await _ServerLoopAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                await PipeServerStream.DisposeAsync().ConfigureAwait(false);
                _LogDebug($"Server has been stopped");
            }
            catch (Exception ex)
            {
                _LogWarn($"Fialed to dispose pipe resource", ex);
            }

            try
            {
                _StopCallback?.Invoke(this);
            }
            catch (Exception ex)
            {
                _LogWarn($"Failed to call stopping callbake", ex);
            }
        }
    }

    private async Task<bool> _ServerLoopAsync()
    {
        bool doNextLoop = true;
        try
        {
            _isConnected = false;

            await PipeServerStream.WaitForConnectionAsync().ConfigureAwait(false);

            Process? clientProcess = null;

            try
            {
                KernelInterop.GetNamedPipeClientProcessId(
                    PipeServerStream.SafePipeHandle.DangerousGetHandle(),
                    out var pid);
                var clientProcessId = (int)pid;

                if (!_ValidateProcessId(clientProcessId, _AllowedProcessId))
                {
                    _LogInfo($"Deny {clientProcessId}");
                    doNextLoop = true;
                }
                else
                {
                    clientProcess = Process.GetProcessById(clientProcessId);

                    _isConnected = true;
                    _LogDebug($"Connected");

                    using var reader = new StreamReader(PipeServerStream, Encoding.UTF8, false, 1024, true);
                    await using var writer = new StreamWriter(PipeServerStream, Encoding.UTF8, 1024, true);

                    doNextLoop = _LoopCallback(reader, writer, clientProcess);

                    await writer.WriteAsync(PipeEndingChar).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    reader.Read();
                }
            }
            catch (Exception ex)
            {
                if (_AllowedProcessId.Length != 0)
                {
                    _LogWarn($"Cannot get client process ID", ex);
                    doNextLoop = true;
                }
                else
                {
                    _isConnected = true;
                    _LogDebug($"Connected (without process ID)");

                    using var reader = new StreamReader(PipeServerStream, Encoding.UTF8, false, 1024, true);
                    await using var writer = new StreamWriter(PipeServerStream, Encoding.UTF8, 1024, true);

                    doNextLoop = _LoopCallback(reader, writer, null);

                    await writer.WriteAsync(PipeEndingChar).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    reader.Read();
                }
            }
        }
        catch (IOException ioEx)
        {
            if (_IsPipeConnedted() && _isConnected)
            {
                _LogDebug($"Client connection has been lost");
                doNextLoop = true;
            }
            else
            {
                _LogWarn($"Server IO warning", ioEx);
                doNextLoop = !stopWhenException;
            }
        }
        catch (Exception ex)
        {
            _LogWarn($"Server error", ex);
            doNextLoop = !stopWhenException;
        }
        finally
        {
            try
            {
                if (!_IsPipeConnedted())
                {
                    PipeServerStream.Disconnect();
                }
            }
            catch (InvalidOperationException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _LogWarn($"Failed to disconnect", ex);
            }

            _isConnected = false;
            _LogDebug("Disconnected");
        }

        return doNextLoop;
    }

    #region Helper Method

    private bool _IsPipeConnedted() =>
        !PipeServerStream.IsConnected;

    private static bool _ValidateProcessId(int clientProcessId, int[] allowedProcessId) =>
        allowedProcessId.Length == 0 || allowedProcessId.Any(id => id == clientProcessId);

    #endregion

    #region Log Method

    private void _LogDebug(string message) =>
        LogWrapper.Debug($"Pipe", $"{Identifier}: {message}");

    private void _LogInfo(string message) =>
        LogWrapper.Info("Pipe", $"{Identifier}: {message}");

    private void _LogWarn(string message, Exception? ex = null) =>
        LogWrapper.Warn(ex, "Pipe", $"{Identifier}: {message}");

    private void _LogError(string message, Exception? ex = null) =>
        LogWrapper.Error(ex, "Pipe", $"{Identifier}: {message}");

    #endregion
}