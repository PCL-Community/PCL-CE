namespace PCL.Core.Minecraft.ResourceProject.Comp.Abstractions;

public interface ICompClientFactory
{
    ICompClient CreateCurseForgeClient(string apiKey);
    ICompClient CreateModrinthClient(string? accessToken = null);
    ICompClient CreateAggregateClient(params ICompClient[] clients);
}
