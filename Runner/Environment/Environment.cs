using Microsoft.Extensions.Logging;

public record Config
{
    public string SqsQueueUrl { get; init; } = default!;
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public bool IsTest { get; init; } = false;

    public string? RegistryUsername { get; init; }
    public string? RegistryPassword { get; init; }
    public string? HostId { get; init; }

    public void Validate()
    {

        if (string.IsNullOrWhiteSpace(SqsQueueUrl))
            throw new ArgumentException("SqsQueueUrl must not be empty.");
    }
}
