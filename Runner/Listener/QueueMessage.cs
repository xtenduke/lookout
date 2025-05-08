namespace Lookout.Runner.Listener;

public record ImageDescription(string Name, string Tag);

public record QueueMessage<T>(ImageDescription ImageDescription, T ProviderData, TimeSpan? DeployTime);