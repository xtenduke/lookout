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
    Config config,
    IDockerClient dockerClient,
    IQueueListener<T> queueListener,
    IContainerUpdater containerUpdater,
    ILogger<MessageProcessor<T>> logger) : IQueueListenerDelegate<T>, IMessageProcessor
{
    private static readonly ConcurrentDictionary<string, DateTime> MessageCache = new();
    private static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromSeconds(30);

    public async Task Start()
    {
        logger.LogDebug("Starting");
        logger.LogDebug("SQS Queue URL: {QueueUrl}", config.SqsQueueUrl);
        await queueListener.StartListening(config.SqsQueueUrl, this);
        Console.ReadKey();
        // keep the program alive
    }

    public void OnReceived(QueueMessage<T> message)
    {
        logger.LogDebug("Received message for imageName: {ImageName}:{TagName}", message.ImageDescription.Name, message.ImageDescription.Tag);
        var cancellationTokenSource = new CancellationTokenSource();
        if (!MessageCache.TryAdd(message.ImageDescription.Name, DateTime.UtcNow)) return;

        cancellationTokenSource.CancelAfter(message.DeployTime ?? DefaultMaxExecutionTime);
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
        logger.LogDebug("Processing message for imageName: {ImageName}:{TagName}", message.ImageDescription.Name, message.ImageDescription.Tag);
        var listParameters = new ContainersListParameters();
        var containers = await dockerClient.Containers.ListContainersAsync(listParameters, cancellationToken);
        var newImageDescription = message.ImageDescription;
        var didUpdateContainers = false;

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
            logger.LogDebug($"Found container(s) running correct image: {string.Join(',', upToDateContainers.Select(x => $"{x.ID}\n"))}");
        }

        if (matchingOutdatedContainers.Count > 0)
        {
            logger.LogDebug($"Found container(s) running outdated image: {string.Join(',', matchingOutdatedContainers.Select(x => $"{x.ID}\n"))}");
            didUpdateContainers = await containerUpdater.HandleContainerImageUpdate(matchingOutdatedContainers, newImageDescription, cancellationToken);
        }

        if (didUpdateContainers)
        {
            await queueListener.ConfirmReceipt(message);
        }
    }
}