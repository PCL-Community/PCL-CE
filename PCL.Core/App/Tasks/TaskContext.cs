using System;
using System.Threading;

namespace PCL.Core.App.Tasks;

public class TaskContext<TInput, TOutput>
{
    internal TaskContext(CancellationToken ct) => CancellationToken = ct;


    public TInput? Input { get; set; }
    public TOutput? Output { get; set; }

    private double _progress;

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 1);
            OnProgressChanged?.Invoke(_progress);
        }
    }

    public CancellationToken CancellationToken { get; set; }

    public bool IsCancelled => CancellationToken.IsCancellationRequested;

    public void ThrowIfCancelled() => CancellationToken.ThrowIfCancellationRequested();


    internal event TaskProgressEvent? OnProgressChanged;

}