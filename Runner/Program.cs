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

        builder.Services.AddTransient<ILookout, Lookout.Runner.Lookout>();
        builder.Services.AddTransient<IQueueListener, QueueListener>();
        builder.Services.AddTransient<IContainerUpdater, ContainerUpdater>();
        builder.Services.AddTransient<DockerClient>(_ => new DockerClientConfiguration().CreateClient());

        using IHost host = builder.Build();

        var lookout = host.Services.GetRequiredService<ILookout>();
        await lookout.Start();
    }
}
