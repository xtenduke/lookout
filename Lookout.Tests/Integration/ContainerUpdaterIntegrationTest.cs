using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Lookout.Runner.Docker;
using Lookout.Runner.Listener;
using Lookout.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lookout.Tests.Integration.Docker;

public class ContainerUpdaterIntegrationTest : IDisposable
{
    private readonly DockerClient _dockerClient;
    private readonly Mock<ILogger<ContainerUpdater>> _loggerMock = new();
    private readonly Config _configMock = ConfigMock.GetConfigNull();
    private readonly ContainerUpdater _sut;
    private List<string> _containerIds = []; 

    private static readonly string ContainerNameSuffix = "-LOOKOUT-TEST";

    public ContainerUpdaterIntegrationTest()
    {
        _dockerClient = new DockerClientConfiguration().CreateClient();
        _sut = new ContainerUpdater(_dockerClient, _configMock, _loggerMock.Object);
    }

    public void Dispose()
    {
        Task.Run(async () => await CleanUp()).Wait();
    }

    [Fact]
    public async Task It_can_start_a_container()
    {
        var containerName = GetRandomWord(10);
        var tempFilePath = await CreateTmpFileAsync();
        var containerId = await StartTestContainer(containerName, tempFilePath);
        var containers = await GetRunningContainersFiltered();
        Assert.Single(containers);
        Assert.NotNull(containerId);
    }

    [Fact]
    public async Task It_can_update_a_container()
    {
        var containerName = GetRandomWord(10);
        var tempFilePath = await CreateTmpFileAsync();
        var containerId = await StartTestContainer(containerName, tempFilePath);
        Assert.NotNull(containerId);

        // Get the list of containers to update
        // find only the test container
        var containersToReplace = await GetRunningContainersFiltered();

        // new image
        var imageDescription = new ImageDescription("redis", "7");

        // Update the container
        var updateResult = await _sut.HandleContainerImageUpdate(
            containersToReplace,
            imageDescription,
            CancellationToken.None);

        Assert.True(updateResult.Success);
        Assert.Equal(1, updateResult.Ids?.Count);

        if (updateResult.Ids != null) {
            _containerIds.Add(updateResult.Ids.First()!);
        }

        var inspectResult = await _dockerClient.Containers.InspectContainerAsync(updateResult.Ids?.First());

        // Assert everything was copied over correctly
        Assert.Multiple(() =>
        {
            Assert.Equal("redis:7", inspectResult.Config.Image);
            Assert.Equal("6379/tcp", inspectResult.Config.ExposedPorts.First().Key);
            Assert.True(inspectResult.Mounts.Where(x => x.Destination == "/data/testfile.txt").Any());
            Assert.True(inspectResult.Mounts.Where(x => x.Source == tempFilePath).Any());
        });
    }

    private async Task<string?> StartTestContainer(string containerName, string? tempFilePath, string? image = "redis:6")
    {

        var progressHandler = new Progress<JSONMessage>(message => {});
        var imageCreateParameters = new ImagesCreateParameters()
        {
            FromImage = "redis",
            Tag = "6",
        };

        await _dockerClient.Images.CreateImageAsync(
            imageCreateParameters,
            null,
            progressHandler,
            CancellationToken.None);

        List<Mount> mounts = new();
        if (tempFilePath != null)
        {
            mounts.Add(new Mount
            {
                Type = "bind",
                Source = tempFilePath,
                Target = "/data/testfile.txt"
            });
        }

        // use the dockerclient to start a redis container
        var createContainerParameters = new CreateContainerParameters
        {
            Image = "redis:6",
            Name = containerName,
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                { "6379/tcp", default }
            },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    { "6379/tcp", new List<PortBinding> { new PortBinding { HostPort = "6379" } } }
                },
                Mounts = mounts,
            }
        };

        var res = await _dockerClient.Containers.CreateContainerAsync(createContainerParameters);
        var started = await _dockerClient.Containers.StartContainerAsync(res.ID, new ContainerStartParameters(), CancellationToken.None);
        if (!started)
        {
            throw new Exception("Failed to bring up container");
        }

        _containerIds.Add(res.ID);

        return res.ID;
    }

    private async static Task<string> CreateTmpFileAsync()
    {
        var content = "Test data";
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, content);
        return tempFilePath;
    }

    private async Task<List<ContainerListResponse>> GetRunningContainersFiltered()
    {
        var res = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters()
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>> {
                     {
                        "name", new Dictionary<string, bool> {
                             { ContainerNameSuffix, false }
                         }
                    }
                }
            });
        return res.ToList();
    }

    private async Task CleanUp()
    {
        foreach (var containerId in _containerIds)
        {
            try
            {
                await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), CancellationToken.None);
            }
            catch { }

            try
            {
                await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters(), CancellationToken.None);
            }
            catch { }
        }

        _containerIds = [];
    }

    private static string GetRandomWord(int length)
    {
        var random = new Random();
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            char letter = (char)random.Next('a', 'z' + 1);
            sb.Append(letter);
        }
        return sb.ToString() + ContainerNameSuffix;
    }
}
