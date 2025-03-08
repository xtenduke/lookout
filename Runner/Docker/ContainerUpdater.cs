using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Listener;
using Lookout.Runner.Util;

namespace Lookout.Runner.Docker;

public interface IContainerUpdater
{
    public Task HandleContainerImageUpdate(ContainerListResponse container, ImageDescription imageDescription);
}

public class ContainerUpdater(DockerClient dockerClient): IContainerUpdater
{
    public async Task HandleContainerImageUpdate(ContainerListResponse container, ImageDescription imageDescription)
    {
        var progressHandler = new Progress<JSONMessage>(message =>
        {
            if (!string.IsNullOrEmpty(message.ProgressMessage))
            {
                Logger.Debug("Image Pull Progress: {0}", message.ProgressMessage);
            }
            else if (!string.IsNullOrEmpty(message.Status))
            {
                Logger.Debug("Image Pull Status: {0}", message.Status);
            }
        });

        await dockerClient.Images.CreateImageAsync(new ImagesCreateParameters()
        {
            FromImage = imageDescription.Name,
            Tag = imageDescription.Tag,
        }, null, progressHandler);

        var newContainerId = await ReplaceContainer(container, imageDescription);

        Logger.Info($"Started new container {newContainerId} running {imageDescription.Name}:{imageDescription.Tag}");
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
            Logger.Debug($"Created new container id: {createContainerResponse.ID}");
            dockerClient.Containers.KillContainerAsync(existingContainer.ID, new ContainerKillParameters());
            Logger.Debug($"Killed old container id: {existingContainer.ID}");
            var result = await dockerClient.Containers.StartContainerAsync(createContainerResponse.ID, new ContainerStartParameters());
            if (!result)
            {
                Logger.Error("Failed to bring up new container");
                Logger.Info("Bringing up old container");
                var fallbackResult = await dockerClient.Containers.StartContainerAsync(existingContainer.ID, new ContainerStartParameters());
                if (!fallbackResult)
                {
                    Logger.Error("Failed to bring up old container... Oops");
                }
            }
            else
            {
                newContainerId = createContainerResponse.ID;
                Logger.Debug($"Container {existingContainer.ID} killed");
                Logger.Debug($"Removing container {existingContainer.ID}");
                dockerClient.Containers.RemoveContainerAsync(existingContainer.ID, new ContainerRemoveParameters());
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create new container with message: {ex.Message}");
        }

        return newContainerId;
    }
}