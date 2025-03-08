namespace Lookout.Runner.Util;

public class DockerUtil
{
    // These methods assume tags have only one : character in them. This may prove to be false
    public static string GetImageNameFromImageDescription(string imageDescription)
    {
        return imageDescription.Split(':').First();
    }

    public static string GetImageTagFromImageDescription(string imageDescription)
    {
        return imageDescription.Split(':').Last();
    }
}