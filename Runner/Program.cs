using Docker.DotNet;
using Lookout.Runner;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Lookout.Runner.Listener.Sqs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace lookout.Runner;

class Program {
    private static bool IsTest = true;
    private static LogLevel LogLevel = LogLevel.Debug;

    public static async Task Main(String[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddTransient<IMessageProcessor, MessageProcessor<SqsProviderData>>();
        builder.Services.AddTransient<IQueueListener<SqsProviderData>, SqsMessageListener>();

        if (IsTest)
        {
            RegisterTestServices(builder.Services);
        }

        builder.Services.AddTransient<IContainerUpdater, ContainerUpdater>();
        builder.Services.AddTransient<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        builder.Services.AddLogging(configure => configure.AddConsole())
            .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel);

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
