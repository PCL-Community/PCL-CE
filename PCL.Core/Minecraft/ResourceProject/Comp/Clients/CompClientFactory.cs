using PCL.Core.Minecraft.ResourceProject.Comp.Abstractions;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Clients;

public sealed class CompClientFactory : ICompClientFactory
{
    public ICompClient CreateCurseForgeClient(string apiKey)
    {
        return new CurseForgeClient(apiKey);
    }

    public ICompClient CreateModrinthClient(string? accessToken = null)
    {
        return new ModrinthClient(accessToken);
    }

    public ICompClient CreateAggregateClient(params ICompClient[] clients)
    {
        return new AggregateClient(clients);
    }
}
