namespace Lookout.Tests.Mocks;

using Microsoft.Extensions.Logging;

public class ConfigMock
{
    public static Config GetConfigNull(
        string sqsQueueUrl = "",
        LogLevel logLevel = LogLevel.Information,
        bool isTest = false,
        string? registryUsername = null,
        string? registryPassword = null) => new()
        {
            SqsQueueUrl = sqsQueueUrl,
            LogLevel = logLevel,
            IsTest = isTest,
            RegistryUsername = registryUsername,
            RegistryPassword = registryPassword
        };

    public static Config GetConfig(
        string sqsQueueUrl = "https://sqs.us-east-1.amazonaws.com/acctid/test-queue",
        LogLevel logLevel = LogLevel.Information,
        bool isTest = false,
        string? registryUsername = "regusername",
        string? registryPassword = "regpassword") => GetConfigNull(
            sqsQueueUrl,
            logLevel,
            isTest,
            registryUsername,
            registryPassword);
}