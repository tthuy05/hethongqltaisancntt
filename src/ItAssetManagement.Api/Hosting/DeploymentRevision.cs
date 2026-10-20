using System.Reflection;

namespace ItAssetManagement.Api.Hosting;

public sealed record DeploymentRevisionInfo(string? ArtifactRevision, string? RenderRevision,
    string Provenance, string WorkflowCompatibility, bool AssignmentApiEnabled);

public static class DeploymentRevision
{
    public const string WorkflowCompatibility = "asset-workflow-per-asset-v1";

    // The assembly stamp identifies the published artifact. Runtime environment
    // variables can corroborate it, but must never substitute a claimed revision.
    public static DeploymentRevisionInfo Create(IConfiguration configuration)
    {
        var embedded = typeof(DeploymentRevision).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "BuildRevision")?.Value;
        return Evaluate(embedded, configuration["RENDER_GIT_COMMIT"], AssignmentRollout.IsEnabled(configuration));
    }

    public static string? Parse(string? revision) => revision is { Length: 40 } &&
        revision.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
            ? revision.ToLowerInvariant() : null;

    public static DeploymentRevisionInfo Evaluate(string? embeddedRevision, string? renderRevision, bool assignmentApiEnabled = false)
    {
        var artifact = Parse(embeddedRevision);
        var platform = Parse(renderRevision);
        var provenance = artifact is null ? "UnknownArtifactRevision" : platform is null
            ? "PlatformRevisionUnavailable" : artifact == platform ? "Matched" : "Mismatch";
        // This is the process configuration, not a claim that schema/permissions
        // are ready. The request-time rollout readiness gate remains independent.
        return new(artifact, platform, provenance, WorkflowCompatibility, assignmentApiEnabled);
    }
}
