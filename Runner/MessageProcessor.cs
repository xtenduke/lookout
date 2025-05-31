using System.Collections.Concurrent;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Lookout.Runner;

public interface IMessageProcessor
{
    public Task Start();
}

public class MessageProcessor<T>(
    Config config,
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
        var imageDescription = message.ImageDescription;

        var (outdatedContainers, upToDateContainers) = await containerUpdater.ListRunningContainers(message.ImageDescription, cancellationToken);

        if (outdatedContainers.Count == 0 && upToDateContainers.Count == 0)
        {
            logger.LogError("Found no containers running {name} for update to {name}:{tag}", imageDescription.Name, imageDescription.Name, imageDescription.Tag);
            return;
        }

        if (upToDateContainers.Count > 0)
        {
            logger.LogDebug("Found container(s) running correct image: {ids}", string.Join(',', upToDateContainers.Select(x => $"{x.ID}\n")));
        }

        if (outdatedContainers.Count > 0)
        {
            logger.LogDebug("Found container(s) running outdated image: {ids}", string.Join(',', outdatedContainers.Select(x => $"{x.ID}\n")));
            var updateContainersResult = await containerUpdater.HandleContainerImageUpdate(
                outdatedContainers,
                imageDescription,
                cancellationToken);

            if (updateContainersResult.Success)
            {
                await queueListener.ConfirmReceipt(message);
            }
        }
    }
}