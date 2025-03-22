using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Listener;
using Lookout.Runner.Util;
using Microsoft.Extensions.Logging;

namespace Lookout.Runner.Docker;

public interface IContainerUpdater
{
    public Task HandleContainerImageUpdate(IReadOnlyCollection<ContainerListResponse> containers, ImageDescription imageDescription);
}

public class ContainerUpdater(IDockerClient dockerClient, ILogger<ContainerUpdater> logger): IContainerUpdater
{
    // Update multiple containers running the same image
    // Trust the caller to send good container data
    // Also trust the caller to be concurrent aware
    public async Task HandleContainerImageUpdate(IReadOnlyCollection<ContainerListResponse> containers,
        ImageDescription imageDescription)
    {
        await PullImage(imageDescription);
        var tasks = containers // Create a range of numbers (1 to 5)
            .Select(container => ReplaceContainer(container, imageDescription)) // Create a task for each number
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task PullImage(ImageDescription imageDescription)
    {
        var progressHandler = new Progress<JSONMessage>(message =>
        {
            if (!string.IsNullOrEmpty(message.ProgressMessage))
            {
                logger.LogDebug("Image Pull Progress: {0}", message.ProgressMessage);
            }
            else if (!string.IsNullOrEmpty(message.Status))
            {
                logger.LogDebug("Image Pull Status: {0}", message.Status);
            }
        });

        await dockerClient.Images.CreateImageAsync(new ImagesCreateParameters()
        {
            FromImage = imageDescription.Name,
            Tag = imageDescription.Tag,
        }, null, progressHandler);
    }

    private async Task<string?> ReplaceContainer(ContainerListResponse existingContainer, ImageDescription imageDescription)
    {
        var runningContainerInspectResponse = await dockerClient.Containers.InspectContainerAsync(existingContainer.ID);

        var createContainerConfig = new CreateContainerParameters(runningContainerInspectResponse.Config)
        {
            // update image config
            Image = $"{imageDescription.Name}:{imageDescription.Tag}"
        };

        string? newContainerId = null;

        try
        {
            var createContainerResponse = await dockerClient.Containers.CreateContainerAsync(createContainerConfig);
            logger.LogDebug($"Created new container id: {createContainerResponse.ID}");
            await dockerClient.Containers.KillContainerAsync(existingContainer.ID, new ContainerKillParameters());
            logger.LogDebug($"Killed old container id: {existingContainer.ID}");
            var result = await dockerClient.Containers.StartContainerAsync(createContainerResponse.ID, new ContainerStartParameters());
            if (!result)
            {
                logger.LogError("Failed to bring up new container");
                logger.LogInformation("Bringing up old container");
                var fallbackResult = await dockerClient.Containers.StartContainerAsync(existingContainer.ID, new ContainerStartParameters());
                if (!fallbackResult)
                {
                    logger.LogCritical("Failed to bring up old container... Oops");
                }
            }
            else
            {
                newContainerId = createContainerResponse.ID;
                logger.LogInformation($"Container {existingContainer.ID} killed");
                logger.LogInformation($"Removing container {existingContainer.ID}");
                await dockerClient.Containers.RemoveContainerAsync(existingContainer.ID, new ContainerRemoveParameters());
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical($"Failed to create new container with message: {ex.Message}");
        }

        logger.LogInformation($"Started new container {newContainerId} running {imageDescription.Name}:{imageDescription.Tag}");
        return newContainerId;
    }
}