using Microsoft.Extensions.Logging;

public record Config
{
    public string SqsQueueUrl { get; init; } = default!;
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public bool IsTest { get; init; } = false;

    public void Validate()
    {

        if (string.IsNullOrWhiteSpace(SqsQueueUrl))
            throw new ArgumentException("SqsQueueUrl must not be empty.");
    }
}
