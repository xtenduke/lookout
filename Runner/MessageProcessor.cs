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

public class MessageProcessor<T>(
    IDockerClient dockerClient,
    IQueueListener<T> queueListener,
    IContainerUpdater containerUpdater,
    ILogger<MessageProcessor<T>> logger) : IQueueListenerDelegate<T>, IMessageProcessor
{
    private static readonly ConcurrentDictionary<string, DateTime> MessageCache = new();
    // This should come from the message - some images will take longer than others
    private static readonly TimeSpan MaxExecutionTime = TimeSpan.FromSeconds(30);

    public async Task Start()
    {
        await queueListener.StartListening("fakequeuearn", this);
        Console.ReadKey();
        // keep the program alive
    }

    public void OnReceived(QueueMessage<T> message)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        if (!MessageCache.TryAdd(message.ImageDescription.Name, DateTime.UtcNow)) return;

        cancellationTokenSource.CancelAfter(MaxExecutionTime);
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessMessage(message, cancellationTokenSource.Token);
            }
            finally
            {
                MessageCache.TryRemove(message.ImageDescription.Name, out _);
                cancellationTokenSource.Dispose();
            }
        }, cancellationTokenSource.Token);
    }

    private async Task ProcessMessage(QueueMessage<T> message, CancellationToken cancellationToken)
    {
        var listParameters = new ContainersListParameters();
        var containers = await dockerClient.Containers.ListContainersAsync(listParameters, cancellationToken);
        var newImageDescription = message.ImageDescription;

        var matchingContainers = containers.Where(x =>
        {
            var localContainerImageName = DockerUtil.GetImageNameFromImageDescription(x.Image);
            return localContainerImageName == newImageDescription.Name;
        }).ToList();

        if (matchingContainers.Count == 0)
        {
            logger.LogError($"Found no containers running {newImageDescription.Name} for update to {newImageDescription.Name}:{newImageDescription.Tag}");
        }

        var matchingOutdatedContainers = matchingContainers.Where(container =>
            DockerUtil.GetImageTagFromImageDescription(container.Image) != newImageDescription.Tag).ToList();

        var upToDateContainers = matchingContainers.Except(matchingOutdatedContainers).ToList();
        if (upToDateContainers.Count > 0)
        {
            logger.LogDebug($"Found containers running correct image: ${upToDateContainers.Select(x => $"{x.ID}\n")}");
        }

        if (matchingOutdatedContainers.Count > 0)
        {
            logger.LogDebug($"Found containers running outdated image: ${matchingOutdatedContainers.Select(x => $"{x.ID}\n")}");
            await containerUpdater.HandleContainerImageUpdate(matchingOutdatedContainers, newImageDescription, cancellationToken);
        }

        // Complete message
        await queueListener.ConfirmReceipt(message);
    }
}