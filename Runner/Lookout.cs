using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Util;
using Lookout.Runner.Listener;

namespace Lookout.Runner;

public interface ILookout
{
    public Task Start();
}

public class Lookout(DockerClient dockerClient, IQueueListener queueListener, IContainerUpdater containerUpdater) : IQueueListenerDelegate, ILookout
{
    public async Task Start()
    {
        queueListener.StartListening("fakequeuearn", this);
        Console.ReadKey();
        // keep the program alive
    }

    public void OnReceived(QueueMessage message)
    {
        _ = Task.Run(async () =>
        {
            await ProcessMessage(message);
        });
    }

    private async Task ProcessMessage(QueueMessage message)
    {
        var listParameters = new ContainersListParameters();
        var containers = await dockerClient.Containers.ListContainersAsync(listParameters);
        var newImageDescription = message.ImageDescription;

        var matchingContainer = containers.Where(x =>
        {
            var localContainerImageName = DockerUtil.GetImageNameFromImageDescription(x.Image);
            return localContainerImageName == newImageDescription.Name;
        }).ToList();

        if (!matchingContainer.Any())
        {
            Logger.Error($"Found no containers running {newImageDescription.Name} for update to {newImageDescription.Name}:{newImageDescription.Tag}");
        }

        foreach (var container in matchingContainer)
        {
            var runningContainerImageTag = DockerUtil.GetImageTagFromImageDescription(container.Image);

            if (runningContainerImageTag != newImageDescription.Tag)
            {
                Logger.Info($"Found {container.Image} running wrong image {runningContainerImageTag} != {newImageDescription.Tag}");
                Logger.Debug("Downloading new image...");
                await containerUpdater.HandleContainerImageUpdate(container, newImageDescription);
                Logger.Info("Complete");
            }
            else
            {
                Logger.Debug($"Found container {container.ID} running correct image");
            }
        }
    }
}