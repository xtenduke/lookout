using Microsoft.Extensions.Logging;

public record Config
{
    public string SqsQueueUrl { get; init; } = default!;
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public bool IsTest { get; init; } = false;

    public string? RegistryUsername { get; init; }
    public string? RegistryPassword { get; init; }
    public string? HostId { get; init; }

    public int PollTimeSeconds { get; init; } = 1;

    public void Validate()
    {

        if (string.IsNullOrWhiteSpace(SqsQueueUrl))
            throw new ArgumentException("SqsQueueUrl must not be empty.");

        if (PollTimeSeconds < 1 || PollTimeSeconds > 20) {
            throw new ArgumentException($"Invalid poll time, must be between 1 and 20 inclusive. Value: {PollTimeSeconds}");
        }
    }
}
