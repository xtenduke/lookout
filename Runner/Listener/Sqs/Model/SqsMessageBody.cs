
namespace Lookout.Runner.Listener;

public class LookoutSqsMessageBody {
    public required string ImageName { get; set; }
    public required string ImageTag { get; set; }
    public string? DeployTimeSeconds { get; set; }
    public string? HostId { get; set; }

    public bool Validate() {
        return !string.IsNullOrWhiteSpace(ImageName) &&
           !string.IsNullOrWhiteSpace(ImageTag);
    }

}
