using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Lookout.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lookout.Tests;
using Runner;

public class MessageProcessorTest
{
    private readonly Mock<IDockerClient> _dockerClient = new();
    private readonly Mock<IQueueListener<TestProviderData>> _queueListener = new();
    private readonly Mock<IContainerUpdater> _containerUpdater = new();
    private readonly Mock<ILogger<MessageProcessor<TestProviderData>>> _logger = new();

    private readonly string _containerNameOne = "containerOne";
    private readonly string _containerImageOne = "container:9";
    private readonly ImageDescription _containerImageDescriptionOne = new("container", "10");
    private readonly string _containerIdOne = "5bbfb21e8d4282ed1f51ce103eea";

    private readonly string _containerNameTwo = "containerTwo";
    private readonly string _containerImageTwo = "containerTwo:3";
    private readonly ImageDescription _containerImageDescriptionTwo = new("containerTwo", "4");
    private readonly string _containerIdTwo = "eebydeebydeeby";

    private void SetupMocks()
    {
        SetupListContainersAsync(DockerMocks.GetMockContainerListResponse(_containerImageOne, _containerNameOne, 10));
        SetupContainerUpdater(TimeSpan.FromSeconds(1));
    }

    // Test cases
    // Multiple messages coming in with the same container
    // Mupltiple messages coming in with different containers
    // Multiple containers matching the same message

    [Fact]
    public async Task It_doesnt_attempt_to_update_one_image_multiple_times()
    {
        SetupMocks();

        var lookout = new MessageProcessor<TestProviderData>(
            _dockerClient.Object,
            _queueListener.Object,
            _containerUpdater.Object,
            _logger.Object);

        lookout.OnReceived(CreateQueueMessage(_containerImageDescriptionOne));
        lookout.OnReceived(CreateQueueMessage(_containerImageDescriptionOne));

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task It_can_update_multiple_containers_from_different_messages()
    {
        SetupMocks();

        var lookout = new MessageProcessor<TestProviderData>(
            _dockerClient.Object,
            _queueListener.Object,
            _containerUpdater.Object,
            _logger.Object);

        lookout.OnReceived(CreateQueueMessage(_containerImageDescriptionOne));
        lookout.OnReceived(CreateQueueMessage(_containerImageDescriptionOne));

        await Task.Delay(TimeSpan.FromSeconds(5));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task It_sends_completion_receipt()
    {
        SetupMocks();

        var lookout = new MessageProcessor<TestProviderData>(
            _dockerClient.Object,
            _queueListener.Object,
            _containerUpdater.Object,
            _logger.Object);

        var queueMessage = CreateQueueMessage(_containerImageDescriptionOne);

        lookout.OnReceived(queueMessage);

        await Task.Delay(TimeSpan.FromSeconds(5));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>(), It.IsAny<CancellationToken>()), Times.Once);

        _queueListener.Verify(x => x.ConfirmReceipt(queueMessage), Times.Once);
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
                It.IsAny<ImageDescription>(), CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Returns(async () => await Task.Delay(delay, CancellationToken.None));
    }

    private QueueMessage<TestProviderData> CreateQueueMessage(
        ImageDescription imageDescription,
        int count = 1, TimeSpan? deployTime = null)
    {
        return new QueueMessage<TestProviderData>(
            imageDescription,
            new TestProviderData(count),
            deployTime);
    }
}