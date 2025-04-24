using Amazon.SQS;
using Docker.DotNet;
using Lookout.Runner;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Lookout.Runner.Listener.Sqs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace lookout.Runner;

class Program
{
    public static async Task Main(String[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var config = configuration.Get<Config>() ?? throw new ArgumentNullException("Missing environment variables.");
        config.Validate();

        builder.Services.AddSingleton(config);

        builder.Services.AddTransient<IAmazonSQS, AmazonSQSClient>();
        builder.Services.AddTransient<IMessageProcessor, MessageProcessor<SqsProviderData>>();
        builder.Services.AddTransient<IQueueListener<SqsProviderData>, SqsMessageListener>();

        if (config.IsTest)
        {
            RegisterTestServices(builder.Services);
        }

        builder.Services.AddTransient<IContainerUpdater, ContainerUpdater>();
        builder.Services.AddTransient<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        builder.Services.AddLogging(configure => configure.AddConsole())
            .Configure<LoggerFilterOptions>(options => options.MinLevel = config.LogLevel);

        using IHost host = builder.Build();

        var lookout = host.Services.GetRequiredService<IMessageProcessor>();
        await lookout.Start();
    }

    private static void RegisterTestServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IMessageProcessor, MessageProcessor<TestProviderData>>();
        serviceCollection.AddTransient<IQueueListener<TestProviderData>, FakeQueueListener>();
    }
}
