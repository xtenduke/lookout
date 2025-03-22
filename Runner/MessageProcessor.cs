using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Util;
using Lookout.Runner.Listener;
using Microsoft.Extensions.Logging;

namespace Lookout.Runner;

public interface IMessageProcessor
{
    public Task Start();
}

public class MessageProcessor(
    IDockerClient dockerClient,
    IQueueListener queueListener,
    IContainerUpdater containerUpdater,
    ILogger<MessageProcessor> logger) : IQueueListenerDelegate, IMessageProcessor
{
    private static readonly ConcurrentDictionary<string, DateTime> MessageCache = new();
    // I should be something like 30s * the number of containers I expect are running
    // Maybe instead we should be dispatching an async event per running container
    // Doesn't really make much sense
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task Start()
    {
        await queueListener.StartListening("fakequeuearn", this);
        Console.ReadKey();
        // keep the program alive
    }

    public void OnReceived(QueueMessage message)
    {
        if (MessageCache.TryAdd(message.ImageDescription.Name, DateTime.UtcNow))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessMessage(message);
                }
                finally
                {
                    // Remove the message from cache after the cache duration
                    await Task.Delay(CacheDuration);
                    MessageCache.TryRemove(message.ImageDescription.Name, out _);
                }
            });
        }
    }

    private async Task ProcessMessage(QueueMessage message)
    {
        var listParameters = new ContainersListParameters();
        var containers = await dockerClient.Containers.ListContainersAsync(listParameters);
        var newImageDescription = message.ImageDescription;

        var matchingContainers = containers.Where(x =>
        {
            var localContainerImageName = DockerUtil.GetImageNameFromImageDescription(x.Image);
            return localContainerImageName == newImageDescription.Name;
        }).ToList();

        if (matchingContainers.Count() == 0)
        {
            logger.LogError($"Found no containers running {newImageDescription.Name} for update to {newImageDescription.Name}:{newImageDescription.Tag}");
        }

        var matchingOutdatedContainers = matchingContainers.Where(container =>
            DockerUtil.GetImageTagFromImageDescription(container.Image) != newImageDescription.Tag).ToList();

        var upToDateContainers = matchingContainers.Except(matchingOutdatedContainers).ToList();
        if (upToDateContainers.Count() > 0)
        {
            logger.LogDebug($"Found containers running correct image: ${upToDateContainers.Select(x => $"{x.ID}\n")}");
        }

        if (matchingOutdatedContainers.Count() > 0)
        {
            logger.LogDebug($"Found containers running outdated image: ${matchingOutdatedContainers.Select(x => $"{x.ID}\n")}");
            await containerUpdater.HandleContainerImageUpdate(matchingOutdatedContainers, newImageDescription);
        }
    }
}