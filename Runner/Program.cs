using Docker.DotNet;
using Lookout.Runner;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lookout;

class Program {
    public static async Task Main(String[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddTransient<IMessageProcessor, Lookout.Runner.MessageProcessor>();
        builder.Services.AddTransient<IQueueListener, QueueListener>();
        builder.Services.AddTransient<IContainerUpdater, ContainerUpdater>();
        builder.Services.AddTransient<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());

        using IHost host = builder.Build();

        var lookout = host.Services.GetRequiredService<IMessageProcessor>();
        await lookout.Start();
    }
}
