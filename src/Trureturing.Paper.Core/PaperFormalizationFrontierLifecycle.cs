using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public sealed record PaperFormalizationFrontierEventContent(
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string ArtifactFamily,
    [property: JsonRequired] string ArtifactSchema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string PredecessorEventRef,
    [property: JsonRequired] string OutcomeDisposition,
    [property: JsonRequired] string CertifiedTruthReleaseDigest,
    [property: JsonRequired] string Detail,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperFormalizationFrontierEvent(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string EventId,
    [property: JsonRequired] PaperFormalizationFrontierEventContent EventContent);

public sealed record PaperFormalizationFrontierNodeState(
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string LatestEventRef,
    [property: JsonRequired] string OutcomeDisposition,
    [property: JsonRequired] string CertifiedTruthReleaseDigest,
    [property: JsonRequired] string UpdatedAt);

public sealed record PaperFormalizationFrontierStateContent(
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] int Version,
    [property: JsonRequired] IReadOnlyList<PaperFormalizationFrontierNodeState> NodeStates,
    [property: JsonRequired] IReadOnlyList<string> AppliedEventRefs,
    [property: JsonRequired] string UpdatedAt);

public sealed record PaperFormalizationFrontierState(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string StateId,
    [property: JsonRequired] PaperFormalizationFrontierStateContent StateContent);

public static class PaperFormalizationFrontierLifecycleService
{
    public const string GovernedSelectionFamily = "governed-selection";
    public const string CanonicalRequestFamily = "canonical-formalization-request";
    public const string FormalizeTransportFamily = "formalize-transport";
    public const string FormalizationOutcomeFamily = "formalization-outcome";
    public const string TruthReleaseCertificationFamily = "truth-release-certification";
    public const string CertifiedClaimManifestFamily = "certified-claim-manifest";

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ArtifactFamilies = new(
        [
            GovernedSelectionFamily,
            CanonicalRequestFamily,
            FormalizeTransportFamily,
            FormalizationOutcomeFamily,
            TruthReleaseCertificationFamily,
            CertifiedClaimManifestFamily
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> OutcomeDispositions = new(
        ["", "candidate-produced", "counterexample", "missing-prerequisite",
         "already-known", "proof-search-exhausted"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> NodeStatuses = new(
        ["selection-pending", "selection-recorded", "request-recorded",
         "transport-recorded", "certification-pending", "certified", "manifested",
         "theory-revision-required", "frontier-revision-required",
         "novelty-reaudit-required", "proof-architecture-revision"],
        StringComparer.Ordinal);

    public static PaperFormalizationFrontierState CreateInitialState(
        PaperFormalizationFrontier frontier,
        string createdAt)
    {
        PaperFormalizationFrontierService.Validate(frontier);
        ParseUtc(createdAt, nameof(createdAt));
        PaperFormalizationFrontierNodeState[] states = frontier.FrontierContent.Nodes
            .Select(node => new PaperFormalizationFrontierNodeState(
                node.NodeId,
                node.ClaimId,
                PaperFormalizationFrontierService.InitialNodeStatus,
                string.Empty,
                string.Empty,
                string.Empty,
                createdAt))
            .OrderBy(state => state.NodeId, StringComparer.Ordinal)
            .ToArray();
        var content = new PaperFormalizationFrontierStateContent(
            frontier.FrontierId,
            0,
            states,
            [],
            createdAt);
        ValidateStateContent(content, frontier);
        return new(
            PaperFormalizationFrontierSchemas.FrontierState,
            Reference(content),
            content);
    }

    public static PaperFormalizationFrontierEvent CreateEvent(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        string nodeId,
        string artifactFamily,
        string artifactSchema,
        string artifactRef,
        string outcomeDisposition,
        string certifiedTruthReleaseDigest,
        string detail,
        string recordedAt)
    {
        PaperFormalizationFrontierService.Validate(frontier);
        Validate(state, frontier);
        PaperFormalizationFrontierNode node =
            PaperFormalizationFrontierService.RequireNode(frontier, nodeId);
        PaperFormalizationFrontierNodeState nodeState = state.StateContent.NodeStates.Single(
            value => string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        RequireArtifactFamily(artifactFamily);
        RequireText(artifactSchema, nameof(artifactSchema), 512);
        RequireDigest(artifactRef, nameof(artifactRef));
        RequireOutcomeDisposition(outcomeDisposition);
        RequireOptionalDigest(
            certifiedTruthReleaseDigest,
            nameof(certifiedTruthReleaseDigest));
        RequireText(detail, nameof(detail), 16384);
        ParseUtc(recordedAt, nameof(recordedAt));
        ValidateTransition(
            frontier,
            state,
            node,
            nodeState,
            artifactFamily,
            outcomeDisposition,
            certifiedTruthReleaseDigest);
        var content = new PaperFormalizationFrontierEventContent(
            frontier.FrontierId,
            node.NodeId,
            node.ClaimId,
            artifactFamily,
            artifactSchema,
            artifactRef,
            nodeState.LatestEventRef,
            outcomeDisposition,
            certifiedTruthReleaseDigest,
            detail,
            recordedAt);
        ValidateEventContent(content, frontier);
        return new(
            PaperFormalizationFrontierSchemas.FrontierEvent,
            Reference(content),
            content);
    }

    public static PaperFormalizationFrontierState ApplyEvent(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontierEvent frontierEvent,
        string appliedAt)
    {
        return ApplyIndependentEvents(
            frontier,
            state,
            [frontierEvent],
            appliedAt);
    }

    public static PaperFormalizationFrontierState ApplyIndependentEvents(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        IReadOnlyList<PaperFormalizationFrontierEvent> events,
        string appliedAt)
    {
        PaperFormalizationFrontierService.Validate(frontier);
        Validate(state, frontier);
        ArgumentNullException.ThrowIfNull(events);
        ParseUtc(appliedAt, nameof(appliedAt));
        if (events.Count < 1)
        {
            throw new InvalidDataException("At least one frontier event is required.");
        }
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperFormalizationFrontierEvent frontierEvent in events)
        {
            Validate(frontierEvent, frontier);
            if (!nodeIds.Add(frontierEvent.EventContent.NodeId))
            {
                throw new InvalidDataException(
                    "An independent event batch may contain at most one event per frontier node.");
            }
            if (state.StateContent.AppliedEventRefs.Contains(
                    frontierEvent.EventId,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier event has already been applied.");
            }
            PaperFormalizationFrontierNodeState current = state.StateContent.NodeStates.Single(
                value => string.Equals(
                    value.NodeId,
                    frontierEvent.EventContent.NodeId,
                    StringComparison.Ordinal));
            if (!string.Equals(
                    current.LatestEventRef,
                    frontierEvent.EventContent.PredecessorEventRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier event was built from a stale predecessor event.");
            }
            PaperFormalizationFrontierNode node =
                PaperFormalizationFrontierService.RequireNode(
                    frontier,
                    frontierEvent.EventContent.NodeId);
            ValidateTransition(
                frontier,
                state,
                node,
                current,
                frontierEvent.EventContent.ArtifactFamily,
                frontierEvent.EventContent.OutcomeDisposition,
                frontierEvent.EventContent.CertifiedTruthReleaseDigest);
        }

        var updates = events.ToDictionary(
            frontierEvent => frontierEvent.EventContent.NodeId,
            StringComparer.Ordinal);
        PaperFormalizationFrontierNodeState[] nextStates = state.StateContent.NodeStates
            .Select(current =>
            {
                if (!updates.TryGetValue(
                        current.NodeId,
                        out PaperFormalizationFrontierEvent? frontierEvent))
                {
                    return current;
                }
                return current with
                {
                    Status = NextStatus(
                        current.Status,
                        frontierEvent.EventContent.ArtifactFamily,
                        frontierEvent.EventContent.OutcomeDisposition),
                    LatestEventRef = frontierEvent.EventId,
                    OutcomeDisposition = string.Equals(
                        frontierEvent.EventContent.ArtifactFamily,
                        FormalizationOutcomeFamily,
                        StringComparison.Ordinal)
                        ? frontierEvent.EventContent.OutcomeDisposition
                        : current.OutcomeDisposition,
                    CertifiedTruthReleaseDigest = string.Equals(
                        frontierEvent.EventContent.ArtifactFamily,
                        TruthReleaseCertificationFamily,
                        StringComparison.Ordinal)
                        ? frontierEvent.EventContent.CertifiedTruthReleaseDigest
                        : current.CertifiedTruthReleaseDigest,
                    UpdatedAt = appliedAt
                };
            })
            .OrderBy(value => value.NodeId, StringComparer.Ordinal)
            .ToArray();
        string[] appliedEvents = state.StateContent.AppliedEventRefs
            .Concat(events.Select(frontierEvent => frontierEvent.EventId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var content = new PaperFormalizationFrontierStateContent(
            frontier.FrontierId,
            state.StateContent.Version + events.Count,
            nextStates,
            appliedEvents,
            appliedAt);
        ValidateStateContent(content, frontier);
        return new(
            PaperFormalizationFrontierSchemas.FrontierState,
            Reference(content),
            content);
    }

    public static void Validate(PaperFormalizationFrontierEvent frontierEvent)
    {
        ArgumentNullException.ThrowIfNull(frontierEvent);
        RequireExact(
            frontierEvent.Schema,
            PaperFormalizationFrontierSchemas.FrontierEvent,
            "schema");
        ValidateEventContent(frontierEvent.EventContent, null);
        RequireIdentity(
            frontierEvent.EventId,
            frontierEvent.EventContent,
            nameof(frontierEvent.EventId));
    }

    public static void Validate(
        PaperFormalizationFrontierEvent frontierEvent,
        PaperFormalizationFrontier frontier)
    {
        Validate(frontierEvent);
        if (!string.Equals(
                frontierEvent.EventContent.FrontierRef,
                frontier.FrontierId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier event does not address the supplied frontier.");
        }
        PaperFormalizationFrontierNode node =
            PaperFormalizationFrontierService.RequireNode(
                frontier,
                frontierEvent.EventContent.NodeId);
        if (!string.Equals(
                node.ClaimId,
                frontierEvent.EventContent.ClaimId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier event changed its node claim identity.");
        }
    }

    public static void Validate(PaperFormalizationFrontierState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        RequireExact(
            state.Schema,
            PaperFormalizationFrontierSchemas.FrontierState,
            "schema");
        ValidateStateContent(state.StateContent, null);
        RequireIdentity(state.StateId, state.StateContent, nameof(state.StateId));
    }

    public static void Validate(
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontier frontier)
    {
        Validate(state);
        ValidateStateContent(state.StateContent, frontier);
    }

    public static bool IsDependencyComplete(string status) =>
        string.Equals(status, "certified", StringComparison.Ordinal)
        || string.Equals(status, "manifested", StringComparison.Ordinal);

    private static void ValidateTransition(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontierNode node,
        PaperFormalizationFrontierNodeState current,
        string artifactFamily,
        string outcomeDisposition,
        string certifiedTruthReleaseDigest)
    {
        string expectedFamily = current.Status switch
        {
            "selection-pending" => GovernedSelectionFamily,
            "selection-recorded" => CanonicalRequestFamily,
            "request-recorded" => FormalizeTransportFamily,
            "transport-recorded" => FormalizationOutcomeFamily,
            "certification-pending" => TruthReleaseCertificationFamily,
            "certified" => CertifiedClaimManifestFamily,
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(expectedFamily)
            || !string.Equals(artifactFamily, expectedFamily, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Frontier node status {current.Status} cannot accept artifact family {artifactFamily}.");
        }

        if (string.Equals(artifactFamily, CanonicalRequestFamily, StringComparison.Ordinal))
        {
            var stateByNode = state.StateContent.NodeStates.ToDictionary(
                value => value.NodeId,
                StringComparer.Ordinal);
            string[] unresolved = node.DependencyNodeIds
                .Where(dependency => !IsDependencyComplete(stateByNode[dependency].Status))
                .ToArray();
            if (unresolved.Length != 0)
            {
                throw new InvalidDataException(
                    "Canonical formalization request is blocked until every dependency node is certified.");
            }
        }

        if (string.Equals(artifactFamily, FormalizationOutcomeFamily, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(outcomeDisposition))
            {
                throw new InvalidDataException(
                    "Formalization outcome requires an explicit disposition.");
            }
        }
        else if (!string.IsNullOrEmpty(outcomeDisposition))
        {
            throw new InvalidDataException(
                "Only a formalization-outcome event may carry outcome_disposition.");
        }

        if (string.Equals(artifactFamily, TruthReleaseCertificationFamily, StringComparison.Ordinal))
        {
            if (string.Equals(
                    certifiedTruthReleaseDigest,
                    frontier.FrontierContent.TruthReleaseDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier certification must identify a later descendant truth release.");
            }
            if (!string.Equals(
                    current.OutcomeDisposition,
                    "candidate-produced",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Only a produced formalization candidate can enter truth-release certification.");
            }
        }
        else if (!string.IsNullOrEmpty(certifiedTruthReleaseDigest))
        {
            throw new InvalidDataException(
                "Only a truth-release-certification event may carry a certified release digest.");
        }
    }

    private static string NextStatus(
        string currentStatus,
        string artifactFamily,
        string outcomeDisposition)
    {
        if (string.Equals(artifactFamily, GovernedSelectionFamily, StringComparison.Ordinal))
        {
            return "selection-recorded";
        }
        if (string.Equals(artifactFamily, CanonicalRequestFamily, StringComparison.Ordinal))
        {
            return "request-recorded";
        }
        if (string.Equals(artifactFamily, FormalizeTransportFamily, StringComparison.Ordinal))
        {
            return "transport-recorded";
        }
        if (string.Equals(artifactFamily, TruthReleaseCertificationFamily, StringComparison.Ordinal))
        {
            return "certified";
        }
        if (string.Equals(artifactFamily, CertifiedClaimManifestFamily, StringComparison.Ordinal))
        {
            return "manifested";
        }
        if (!string.Equals(artifactFamily, FormalizationOutcomeFamily, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported frontier artifact family {artifactFamily}.");
        }
        return outcomeDisposition switch
        {
            "candidate-produced" => "certification-pending",
            "counterexample" => "theory-revision-required",
            "missing-prerequisite" => "frontier-revision-required",
            "already-known" => "novelty-reaudit-required",
            "proof-search-exhausted" => "proof-architecture-revision",
            _ => throw new InvalidDataException(
                $"Unsupported formalization outcome disposition {outcomeDisposition}.")
        };
    }

    private static void ValidateEventContent(
        PaperFormalizationFrontierEventContent content,
        PaperFormalizationFrontier? frontier)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.FrontierRef, "frontier_ref");
        RequireDigest(content.NodeId, "node_id");
        RequireText(content.ClaimId, "claim_id", 256);
        RequireArtifactFamily(content.ArtifactFamily);
        RequireText(content.ArtifactSchema, "artifact_schema", 512);
        RequireDigest(content.ArtifactRef, "artifact_ref");
        RequireOptionalDigest(content.PredecessorEventRef, "predecessor_event_ref");
        RequireOutcomeDisposition(content.OutcomeDisposition);
        RequireOptionalDigest(
            content.CertifiedTruthReleaseDigest,
            "certified_truth_release_digest");
        RequireText(content.Detail, "detail", 16384);
        ParseUtc(content.RecordedAt, "recorded_at");
        if (frontier is not null
            && !string.Equals(content.FrontierRef, frontier.FrontierId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier event changed its frontier reference.");
        }
    }

    private static void ValidateStateContent(
        PaperFormalizationFrontierStateContent content,
        PaperFormalizationFrontier? frontier)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.FrontierRef, "frontier_ref");
        if (content.Version < 0
            || content.NodeStates is null
            || content.NodeStates.Count < 3)
        {
            throw new InvalidDataException(
                "Frontier state version or node-state count is invalid.");
        }
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var claims = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperFormalizationFrontierNodeState nodeState in content.NodeStates)
        {
            RequireDigest(nodeState.NodeId, "node_id");
            RequireText(nodeState.ClaimId, "claim_id", 256);
            if (!NodeStatuses.Contains(nodeState.Status)
                || !nodes.Add(nodeState.NodeId)
                || !claims.Add(nodeState.ClaimId))
            {
                throw new InvalidDataException(
                    "Frontier node state status or identity is invalid.");
            }
            RequireOptionalDigest(nodeState.LatestEventRef, "latest_event_ref");
            RequireOutcomeDisposition(nodeState.OutcomeDisposition);
            RequireOptionalDigest(
                nodeState.CertifiedTruthReleaseDigest,
                "certified_truth_release_digest");
            ParseUtc(nodeState.UpdatedAt, "updated_at");
            if ((string.Equals(nodeState.Status, "certified", StringComparison.Ordinal)
                    || string.Equals(nodeState.Status, "manifested", StringComparison.Ordinal))
                && string.IsNullOrEmpty(nodeState.CertifiedTruthReleaseDigest))
            {
                throw new InvalidDataException(
                    "Certified or manifested frontier nodes require a truth-release digest.");
            }
        }
        RequireDigestList(content.AppliedEventRefs, "applied_event_refs", 0);
        if (content.AppliedEventRefs.Count != content.Version)
        {
            throw new InvalidDataException(
                "Frontier state version must equal the number of applied events.");
        }
        ParseUtc(content.UpdatedAt, "updated_at");
        if (frontier is not null)
        {
            if (!string.Equals(content.FrontierRef, frontier.FrontierId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier state does not address the supplied frontier.");
            }
            string[] expectedNodes = frontier.FrontierContent.Nodes
                .Select(node => node.NodeId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualNodes = content.NodeStates
                .Select(node => node.NodeId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!expectedNodes.SequenceEqual(actualNodes, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier state changed the immutable frontier node set.");
            }
        }
    }

    private static void RequireArtifactFamily(string value)
    {
        if (!ArtifactFamilies.Contains(value))
        {
            throw new InvalidDataException(
                $"Unsupported frontier artifact family {value}.");
        }
    }

    private static void RequireOutcomeDisposition(string value)
    {
        if (!OutcomeDispositions.Contains(value))
        {
            throw new InvalidDataException(
                $"Unsupported outcome disposition {value}.");
        }
    }

    private static void RequireOptionalDigest(string value, string name)
    {
        if (!string.IsNullOrEmpty(value))
        {
            RequireDigest(value, name);
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximumLength} characters.");
        }
    }

    private static string Reference<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Reference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
