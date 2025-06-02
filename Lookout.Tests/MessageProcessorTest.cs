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
    private readonly Mock<IQueueListener<TestProviderData>> _queueListener = new();
    private readonly Mock<IContainerUpdater> _containerUpdater = new();
    private readonly Mock<ILogger<MessageProcessor<TestProviderData>>> _logger = new();

    private readonly string _containerNameOne = "containerOne";
    private readonly string _containerImageOne = "container:9";
    private readonly ImageDescription _containerImageDescriptionOne = new("container", "10");
    private readonly string _containerIdOne = "5bbfb21e8d4282ed1f51ce103eea";

    private readonly ImageDescription _containerImageDescriptionTwo = new("containerTwo", "4");
    private readonly string _containerIdTwo = "eebydeebydeeby";
    private readonly Config _configMock;

    public MessageProcessorTest()
    {
        SetupListContainersAsync(new RunningContainersResult(DockerMocks.GetMockContainerListResponse(_containerImageOne, _containerNameOne, 10), []));
        SetupContainerUpdater(TimeSpan.FromSeconds(1), true);
        _configMock = new Config()
        {
            SqsQueueUrl = "testqueueurl",
        };
    }

    [Fact]
    public async Task It_doesnt_attempt_to_update_one_image_multiple_times()
    {
        var lookout = new MessageProcessor<TestProviderData>(
            _configMock,
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
        var lookout = new MessageProcessor<TestProviderData>(
            _configMock,
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
        var lookout = new MessageProcessor<TestProviderData>(
            _configMock,
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

    [Fact]
    public async Task It_doesnt_send_completion_if_not_all_succeeded()
    {
        SetupContainerUpdater(TimeSpan.FromSeconds(1), false);

        var lookout = new MessageProcessor<TestProviderData>(
            _configMock,
            _queueListener.Object,
            _containerUpdater.Object,
            _logger.Object);

        var queueMessage = CreateQueueMessage(_containerImageDescriptionOne);

        lookout.OnReceived(queueMessage);

        await Task.Delay(TimeSpan.FromSeconds(5));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>(), It.IsAny<CancellationToken>()), Times.Once);

        _queueListener.Verify(x => x.ConfirmReceipt(queueMessage), Times.Never);
    }

    [Theory]
    [InlineData("1234", null, true)]
    [InlineData(null, null, true)]
    [InlineData(null, "1234", false)]
    [InlineData("1234", "4567", false)]
    public async Task It_handles_host_id_config(string? configHostId, string? messageHostId, bool expected)
    {
        var sut = new MessageProcessor<TestProviderData>(
            _configMock,
            _queueListener.Object,
            _containerUpdater.Object,
            _logger.Object);

        var config = _configMock with
        {
            HostId = configHostId
        };

        var queueMessage = CreateQueueMessage(_containerImageDescriptionOne, 1, null, messageHostId);

        sut.OnReceived(queueMessage);

        await Task.Delay(TimeSpan.FromSeconds(5));

        _containerUpdater.Verify(x => x.HandleContainerImageUpdate(
            It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
            It.IsAny<ImageDescription>(), It.IsAny<CancellationToken>()), Times.Exactly(expected ? 1 : 0));

        _queueListener.Verify(x => x.ConfirmReceipt(queueMessage), Times.Exactly(expected ? 1 : 0));
    }

    private void SetupListContainersAsync(RunningContainersResult containers)
    {
        _containerUpdater.Setup(x => x.ListRunningContainers(
            It.IsAny<ImageDescription>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(containers);
    }

    private void SetupContainerUpdater(TimeSpan delay, bool success = true)
    {
        var ids = success
            ? new List<string?> { _containerIdOne, _containerIdTwo }
            : new List<string?> { null, null };
        var result = new ContainerImageUpdateResult(success, ids);

        _containerUpdater.SetupSequence(s => s.HandleContainerImageUpdate(
                It.IsAny<IReadOnlyCollection<ContainerListResponse>>(),
                It.IsAny<ImageDescription>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result)
            .Returns(async () =>
            {
                await Task.Delay(delay, CancellationToken.None);
                return result;
            });
    }

    private QueueMessage<TestProviderData> CreateQueueMessage(
        ImageDescription imageDescription,
        int count = 1,
        TimeSpan? deployTime = null,
        string? hostId = null)
    {
        return new QueueMessage<TestProviderData>(
            imageDescription,
            new TestProviderData(count),
            deployTime,
            hostId);
    }
}