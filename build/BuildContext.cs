namespace Build;

internal sealed class BuildContext
{
    public required string ImageName { get; init; }
    public required bool Push { get; init; }
    public required string GitSha { get; init; }
    public required string CreatedIso { get; init; }

    public string ShortSha => GitSha[..Math.Min(7, GitSha.Length)];

    public string LocalTag => "dev";

    public IReadOnlyList<string> ComputeTags()
    {
        if (!Push)
        {
            return [LocalTag];
        }

        return ["latest", $"sha-{ShortSha}"];
    }
}
