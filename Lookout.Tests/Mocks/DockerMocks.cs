using Docker.DotNet.Models;

namespace Lookout.Tests.Mocks;

public class DockerMocks
{
    public static List<ContainerListResponse> GetMockContainerListResponse(string imageName, string containerName, int count = 1)
    {
        return Enumerable.Range(0, count).ToList().Select(x => new ContainerListResponse
        {
            Image = imageName,
            Names = [$"{containerName}-{x}"]
        }).ToList();
    }
}