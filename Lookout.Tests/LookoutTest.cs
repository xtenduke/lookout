using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Moq;

namespace Lookout.Tests;
using Runner;

public class LookoutTest
{
    private readonly Mock<IDockerClient> _dockerClient = new();
    private readonly Mock<IQueueListener> _queueListener = new();
    private readonly Mock<IContainerUpdater> _containerUpdater = new();

    private readonly string _containerNameOne = "containerOne";
    private readonly string _containerImageOne = "container:9";
    private readonly ImageDescription _containerImageDescriptionOne = new("container", "10");
    private readonly string _containerIdOne = "5bbfb21e8d4282ed1f51ce103eea";

    private readonly string _containerNameTwo = "containerTwo";
    private readonly string _containerImageTwo = "containerTwo:3";
    private readonly ImageDescription _containerImageDescriptionTwo = new("containerTwo", "4");
    private readonly string _containerIdTwo = "eebydeebydeeby";

    public LookoutTest()
    {
        SetupListContainersAsync(GetMockContainerListResponse(_containerImageOne, _containerNameOne, 10));
        SetupContainerUpdater(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task It_doesnt_attempt_to_update_one_container_multiple_times()
    {
        var lookout = new Lookout(
            _dockerClient.Object,
            _queueListener.Object,
            _containerUpdater.Object);

        lookout.OnReceived(new QueueMessage(_containerImageDescriptionOne));
        lookout.OnReceived(new QueueMessage(_containerImageDescriptionOne));

        await Task.Delay(TimeSpan.FromSeconds(5));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>()), Times.Once);
    }

    private void SetupListContainersAsync(List<ContainerListResponse> containers)
    {
        _dockerClient
            .Setup(x => x.Containers.ListContainersAsync(It.IsAny<ContainersListParameters>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(containers);
    }

    private void SetupContainerUpdater(TimeSpan delay)
    {
       _containerUpdater.SetupSequence(s => s.HandleContainerImageUpdate(
               It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
               It.IsAny<ImageDescription>()))
            .Returns(Task.CompletedTask)
            .Returns(async () => await Task.Delay(delay, CancellationToken.None));
    }

    private List<ContainerListResponse> GetMockContainerListResponse(string imageName, string containerName, int count = 1)
    {
        return Enumerable.Range(0, count).ToList().Select(x => new ContainerListResponse
        {
            Image = imageName,
            Names = [$"{containerName}-{x}"]
        }).ToList();
    }
}