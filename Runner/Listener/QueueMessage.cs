namespace Lookout.Runner.Listener;

public record ImageDescription(string Name, string Tag);

public record QueueMessage(ImageDescription ImageDescription);