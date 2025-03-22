using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lookout.Tests.Docker;

public class ContainerUpdaterTest
{
    private readonly Mock<IDockerClient> _dockerClientMock = new();
    private readonly Mock<ILogger<ContainerUpdater>> _loggerMock = new();

    private static readonly string _containerId = "5bbfb21e8d4282ed1f51ce103eea";

    public ContainerUpdaterTest()
    {
        SetupCreateImageAsync(true);
        SetupInspectContainerAsync(true, GetContainerInspectResponse(null));
        SetupCreateContainerAsync(true, _containerId);
        SetupKillContainerAsync(true);
        SetupStartContainerAsync(true);
        SetupRemoveContainerAsync(true);
    }

    [Fact]
    public async Task It_can_update_a_container()
    {
        var containersToReplace = CreateContainersToReplace(1);
        var containerIdToReplace= containersToReplace.First().ID;

        var sut = new ContainerUpdater(_dockerClientMock.Object, _loggerMock.Object);
        var imageDescription = new ImageDescription("redis", "7");
        await sut.HandleContainerImageUpdate(containersToReplace, imageDescription, CancellationToken.None);

        _dockerClientMock.Verify(x => x.Images.CreateImageAsync(
            It.IsAny<ImagesCreateParameters>(),
            It.IsAny<AuthConfig>(),
            It.IsAny<Progress<JSONMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _dockerClientMock.Verify(x => x.Containers.InspectContainerAsync(
            containerIdToReplace,
            It.IsAny<CancellationToken>()), Times.Once);

        _dockerClientMock.Verify(x => x.Containers.KillContainerAsync(
            containerIdToReplace,
            It.IsAny<ContainerKillParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _dockerClientMock.Verify(x => x.Containers.StartContainerAsync(
            _containerId,
            It.IsAny<ContainerStartParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _dockerClientMock.Verify(x => x.Containers.RemoveContainerAsync(
            containersToReplace.First().ID,
            It.IsAny<ContainerRemoveParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task It_can_update_many_containers()
    {
        SetupCreateContainerAsync(true, null);

        var times = 100;
        var containersToReplace = CreateContainersToReplace(times);
        var containerIdsToReplace = containersToReplace.Select(x => x.ID);

        var sut = new ContainerUpdater(_dockerClientMock.Object, _loggerMock.Object);
        var imageDescription = new ImageDescription("redis", "7");
        await sut.HandleContainerImageUpdate(containersToReplace, imageDescription, CancellationToken.None);

        _dockerClientMock.Verify(x => x.Images.CreateImageAsync(
            It.IsAny<ImagesCreateParameters>(),
            It.IsAny<AuthConfig>(),
            It.IsAny<Progress<JSONMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _dockerClientMock.Verify(x => x.Containers.InspectContainerAsync(
            It.IsIn(containerIdsToReplace),
            It.IsAny<CancellationToken>()), Times.Exactly(times));

        _dockerClientMock.Verify(x => x.Containers.KillContainerAsync(
            It.IsIn(containerIdsToReplace),
            It.IsAny<ContainerKillParameters>(),
            It.IsAny<CancellationToken>()), Times.Exactly(times));

        _dockerClientMock.Verify(x => x.Containers.StartContainerAsync(
            It.IsAny<string>(),
            It.IsAny<ContainerStartParameters>(),
            It.IsAny<CancellationToken>()), Times.Exactly(times));

        _dockerClientMock.Verify(x => x.Containers.RemoveContainerAsync(
            It.IsIn(containerIdsToReplace),
            It.IsAny<ContainerRemoveParameters>(),
            It.IsAny<CancellationToken>()), Times.Exactly(times));
    }

    private void SetupCreateImageAsync(bool success, int timeSeconds = 2)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        var setup = _dockerClientMock.Setup(x => x.Images.CreateImageAsync(
            It.IsAny<ImagesCreateParameters>(),
            It.IsAny<AuthConfig>(),
            It.IsAny<Progress<JSONMessage>>(),
            It.IsAny<CancellationToken>()));
        if (success)
        {
            setup.Returns(async () => await Task.Delay(time));
        }
        else
        {
            setup.Throws<InvalidOperationException>();
        }
    }

    private void SetupInspectContainerAsync(bool success, ContainerInspectResponse? response, int timeSeconds = 1)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        var setup = _dockerClientMock.Setup(x => x.Containers.InspectContainerAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()));
        setup.Returns(async () =>
        {
            await Task.Delay(time);
            return success ? response : throw new InvalidOperationException();
        });
    }

    private void SetupCreateContainerAsync(bool success, string? containerId, int timeSeconds = 1)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        var setup = _dockerClientMock.Setup(x => x.Containers.CreateContainerAsync(
            It.IsAny<CreateContainerParameters>(),
            It.IsAny<CancellationToken>()));
        setup.Returns(async () =>
        {
            await Task.Delay(time);
            return success ? new CreateContainerResponse { ID = containerId ?? CreateRandomContainerId() } : throw new InvalidOperationException();
        });
    }

    private void SetupKillContainerAsync(bool result, int timeSeconds = 1)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        _dockerClientMock.Setup(x => x.Containers.KillContainerAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerKillParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(time);
                return result ? Task.CompletedTask : throw new InvalidOperationException();
            });
    }
    
    private void SetupStartContainerAsync(bool result, int timeSeconds = 1)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        _dockerClientMock.Setup(x => x.Containers.StartContainerAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerStartParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(time);
                return result;
            });
    }

    private void SetupRemoveContainerAsync(bool result, int timeSeconds = 1)
    {
        var time = TimeSpan.FromSeconds(timeSeconds);
        _dockerClientMock.Setup(x => x.Containers.RemoveContainerAsync(
            It.IsAny<string>(),
            It.IsAny<ContainerRemoveParameters>(),
            It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(time);
                return result ? Task.CompletedTask : throw new InvalidOperationException();
            });
    }

    private static string CreateRandomContainerId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    private static ContainerInspectResponse GetContainerInspectResponse(string? id)
    {
        return new ContainerInspectResponse
        {
            ID = id ?? _containerId
        };
    }

    private static List<ContainerListResponse> CreateContainersToReplace(int count)
    {
        return Enumerable.Range(0, count).Select(x => new ContainerListResponse
        {
            ID = CreateRandomContainerId()
        }).ToList();
    }
}