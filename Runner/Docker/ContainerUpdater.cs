using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Listener;
using Lookout.Runner.Util;
using Microsoft.Extensions.Logging;

namespace Lookout.Runner.Docker;

public interface IContainerUpdater
{
    public Task<bool> HandleContainerImageUpdate(
        IReadOnlyCollection<ContainerListResponse> containers,
        ImageDescription imageDescription,
        CancellationToken cancellationToken);
}

public class ContainerUpdater(IDockerClient dockerClient, ILogger<ContainerUpdater> logger): IContainerUpdater
{
    // Update multiple containers running the same image
    // Trust the caller to send good container data
    // Also trust the caller to be concurrent aware
    public async Task<bool> HandleContainerImageUpdate(IReadOnlyCollection<ContainerListResponse> containers,
        ImageDescription imageDescription,
        CancellationToken cancellationToken)
    {
        var result = await PullImage(imageDescription, cancellationToken);

        if (!result)
            return result;

        var tasks = containers
            .Select(container => ReplaceContainer(container, imageDescription, cancellationToken))
            .ToArray();

         var replaceResult = await Task.WhenAll(tasks);
        return !replaceResult.Any(s => s == null);

    }

    private async Task<bool> PullImage(ImageDescription imageDescription, CancellationToken cancellationToken)
    {
        try
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
            }, null, progressHandler, cancellationToken);
        } catch (DockerApiException ex)
        {
            logger.LogError("Failed to pull image {ImageName}:{TagName} - error {Error}", imageDescription.Name, imageDescription.Tag, ex.Message);
            return false;
        }

        return true;
    }

    private async Task<string?> ReplaceContainer(
        ContainerListResponse existingContainer,
        ImageDescription imageDescription,
        CancellationToken cancellationToken)
    {
        var runningContainerInspectResponse = await dockerClient.Containers.InspectContainerAsync(
            existingContainer.ID,
            cancellationToken);

        var createContainerConfig = new CreateContainerParameters(runningContainerInspectResponse.Config)
        {
            // update image config
            Image = $"{imageDescription.Name}:{imageDescription.Tag}"
        };

        string? newContainerId = null;

        try
        {
            var createContainerResponse = await dockerClient.Containers.CreateContainerAsync(
                createContainerConfig,
                cancellationToken);
            logger.LogDebug($"Created new container id: {createContainerResponse.ID}");

            await dockerClient.Containers.KillContainerAsync(
                existingContainer.ID,
                new ContainerKillParameters(),
                cancellationToken);
            logger.LogDebug($"Killed old container id: {existingContainer.ID}");

            var result = await dockerClient.Containers.StartContainerAsync(
                createContainerResponse.ID,
                new ContainerStartParameters(),
                cancellationToken);

            if (!result)
            {
                logger.LogError("Failed to bring up new container");
                logger.LogInformation("Bringing up old container");
                var fallbackResult = await dockerClient.Containers.StartContainerAsync(
                    existingContainer.ID,
                    new ContainerStartParameters(),
                    cancellationToken);

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
                await dockerClient.Containers.RemoveContainerAsync(
                    existingContainer.ID,
                    new ContainerRemoveParameters(),
                    cancellationToken);
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