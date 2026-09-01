namespace Trureturing.Paper.Core;

public static partial class PaperFrontierNodeSelectionService
{
    private static readonly HashSet<string> ProgressDispositions = new(
        [
            "candidate-produced",
            "counterexample",
            "missing-prerequisite",
            "already-known",
            "proof-search-exhausted"
        ],
        StringComparer.Ordinal);

    public static void Validate(PaperFrontierFormalizeTransportCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierFormalizationProgressSchemas.TransportCursor,
            nameof(cursor.Schema));
        RequireDigest(
            cursor.FormalizationRequestRef,
            nameof(cursor.FormalizationRequestRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequireDigest(cursor.SelectionRef, nameof(cursor.SelectionRef));
        RequireDigest(cursor.FrontierRef, nameof(cursor.FrontierRef));
        RequireDigest(cursor.NodeId, nameof(cursor.NodeId));
        RequireClaimId(cursor.ClaimId);
        RequireStoredArtifact(
            cursor.TransportEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.FrontierState,
            PaperFormalizationFrontierSchemas.FrontierState);
        ParseUtc(cursor.RecordedAt, nameof(cursor.RecordedAt));
    }

    public static void Validate(PaperFrontierFormalizationOutcomeCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierFormalizationProgressSchemas.OutcomeCursor,
            nameof(cursor.Schema));
        RequireDigest(
            cursor.FormalizationRequestRef,
            nameof(cursor.FormalizationRequestRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DecisionRef, nameof(cursor.DecisionRef));
        RequireDigest(cursor.SelectionRef, nameof(cursor.SelectionRef));
        RequireDigest(cursor.FrontierRef, nameof(cursor.FrontierRef));
        RequireDigest(cursor.NodeId, nameof(cursor.NodeId));
        RequireClaimId(cursor.ClaimId);
        RequireProgressText(cursor.OutcomeClass, nameof(cursor.OutcomeClass), 128);
        if (!ProgressDispositions.Contains(cursor.OutcomeDisposition))
        {
            throw new InvalidDataException(
                "Frontier outcome cursor has an unsupported disposition.");
        }
        RequireProgressText(cursor.Route, nameof(cursor.Route), 128);
        RequireStoredArtifact(
            cursor.OutcomeEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.FrontierState,
            PaperFormalizationFrontierSchemas.FrontierState);
        ParseUtc(cursor.RecordedAt, nameof(cursor.RecordedAt));
    }

    public static void Validate(PaperFrontierCertifiedClaimManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        RequireExact(
            manifest.Schema,
            PaperFrontierFormalizationProgressSchemas.CertifiedManifest,
            nameof(manifest.Schema));
        PaperFrontierCertifiedClaimManifestContent content =
            manifest.ManifestContent
            ?? throw new InvalidDataException(
                "manifest_content is required.");
        foreach (string digest in new[]
        {
            content.FrontierRef,
            content.NodeId,
            content.FormalizationRequestRef,
            content.SelectionRef,
            content.FormalizationResultRef,
            content.DecisionRef,
            content.CertificationEvaluationRef,
            content.CertifiedClaimRef,
            content.CertifyingReleaseRef,
            content.CertifyingReleaseDigest,
            content.StatementId
        })
        {
            RequireDigest(digest, nameof(content));
        }
        RequireClaimId(content.ClaimId);
        RequirePaperId(content.PaperId);
        RequireGid(content.Gid);
        RequireProgressText(
            content.LeanDeclaration,
            nameof(content.LeanDeclaration),
            1024);
        RequireExact(
            content.DeclarationKind,
            "theorem",
            nameof(content.DeclarationKind));
        if (content.AxiomClosure is null)
        {
            throw new InvalidDataException(
                "Certified frontier manifest axiom_closure is required.");
        }
        string[] normalizedAxioms = content.AxiomClosure
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!content.AxiomClosure.SequenceEqual(
                normalizedAxioms,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Certified frontier manifest axioms must be sorted and unique.");
        }
        foreach (string axiom in content.AxiomClosure)
        {
            RequireProgressText(axiom, nameof(content.AxiomClosure), 1024);
        }
        ParseUtc(content.ManifestedAt, nameof(content.ManifestedAt));
        RequireIdentity(
            manifest.ManifestId,
            content,
            nameof(manifest.ManifestId));
    }

    public static void Validate(PaperFrontierReadySet readySet)
    {
        ArgumentNullException.ThrowIfNull(readySet);
        RequireExact(
            readySet.Schema,
            PaperFrontierFormalizationProgressSchemas.ReadySet,
            nameof(readySet.Schema));
        PaperFrontierReadySetContent content = readySet.ReadySetContent
            ?? throw new InvalidDataException(
                "ready_set_content is required.");
        RequireDigest(content.FrontierRef, nameof(content.FrontierRef));
        RequireDigest(content.TriggerNodeId, nameof(content.TriggerNodeId));
        RequireDigest(
            content.TriggerManifestRef,
            nameof(content.TriggerManifestRef));
        RequireDigest(
            content.FrontierStateRef,
            nameof(content.FrontierStateRef));
        if (content.ReadyNodes is null)
        {
            throw new InvalidDataException("ready_nodes is required.");
        }
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        int previousWave = -1;
        int previousPriority = int.MaxValue;
        string previousNodeId = string.Empty;
        for (int index = 0; index < content.ReadyNodes.Count; index++)
        {
            PaperFrontierReadyNode node = content.ReadyNodes[index]
                ?? throw new InvalidDataException(
                    "ready_nodes cannot contain null.");
            if (node.DispatchOrder != index + 1
                || node.ParallelWave < 1
                || node.Priority is < 0 or > 100
                || !string.Equals(
                    node.NextRoute,
                    "governed-selection",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Ready-set order, wave, priority, or route is invalid.");
            }
            RequireDigest(node.NodeId, nameof(node.NodeId));
            RequireClaimId(node.ClaimId);
            RequireFormalizationKind(node.FormalizationKind);
            if (!nodeIds.Add(node.NodeId) || !claimIds.Add(node.ClaimId))
            {
                throw new InvalidDataException(
                    "Ready-set node and claim identities must be unique.");
            }
            if (node.ParallelWave < previousWave
                || (node.ParallelWave == previousWave
                    && node.Priority > previousPriority)
                || (node.ParallelWave == previousWave
                    && node.Priority == previousPriority
                    && string.CompareOrdinal(
                        node.NodeId,
                        previousNodeId) <= 0))
            {
                throw new InvalidDataException(
                    "Ready-set nodes are not in deterministic wave, priority, and identity order.");
            }
            previousWave = node.ParallelWave;
            previousPriority = node.Priority;
            previousNodeId = node.NodeId;
        }
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        RequireIdentity(
            readySet.ReadySetId,
            content,
            nameof(readySet.ReadySetId));
    }

    public static void Validate(PaperFrontierCertificationCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierFormalizationProgressSchemas.CertificationCursor,
            nameof(cursor.Schema));
        foreach (string digest in new[]
        {
            cursor.FormalizationRequestRef,
            cursor.EvaluationRef,
            cursor.CertifiedClaimRef,
            cursor.CertifyingReleaseRef,
            cursor.CertifyingReleaseDigest,
            cursor.FrontierRef,
            cursor.NodeId
        })
        {
            RequireDigest(digest, nameof(cursor));
        }
        RequireClaimId(cursor.ClaimId);
        RequireStoredArtifact(
            cursor.CertificationEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.CertifiedManifest,
            PaperFrontierFormalizationProgressSchemas.CertifiedManifest);
        RequireStoredArtifact(
            cursor.ManifestEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.FrontierState,
            PaperFormalizationFrontierSchemas.FrontierState);
        RequireStoredArtifact(
            cursor.ReadySet,
            PaperFrontierFormalizationProgressSchemas.ReadySet);
        ParseUtc(cursor.RecordedAt, nameof(cursor.RecordedAt));
    }

    private static void RequireProgressText(
        string value,
        string name,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximum} characters.");
        }
    }
}
