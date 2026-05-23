using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

public interface IPipelineStep<in TContext>
{
    string Name { get; }
    bool Block { get; }
    double Weight { get; }
    Task ExecuteAsync(TContext context, CancellationToken ct);
}