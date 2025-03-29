using Docker.DotNet;
using Lookout.Runner;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace lookout;

class Program {
    public static async Task Main(String[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddTransient<IMessageProcessor, MessageProcessor<TestProviderData>>();
        builder.Services.AddTransient<IQueueListener<TestProviderData>, QueueListener>();
        builder.Services.AddTransient<IContainerUpdater, ContainerUpdater>();
        builder.Services.AddTransient<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        builder.Services.AddLogging(configure => configure.AddConsole())
            .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);

        using IHost host = builder.Build();

        var lookout = host.Services.GetRequiredService<IMessageProcessor>();
        await lookout.Start();
    }
}
