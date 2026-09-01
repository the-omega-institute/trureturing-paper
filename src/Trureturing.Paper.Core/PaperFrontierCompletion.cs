namespace Trureturing.Paper.Core;

internal sealed record PaperFrontierCompletionMaterial(
    PaperFormalizationFrontierNode Node,
    PaperTheoremPackageClaim PackageClaim,
    PaperFrontierCertificationCursor CertificationCursor,
    PaperFrontierCertifiedClaimManifest FrontierManifest,
    PaperCertifiedClaim CertifiedClaim,
    PaperCertificationRelease OriginalRelease);

internal sealed record PaperFrontierCoherentRelease(
    string ReleaseRef,
    PaperCertificationRelease Release);

public static partial class PaperFrontierNodeSelectionService
{
    private static readonly HashSet<string> ManuscriptFormalKinds = new(
        ["lemma", "proposition", "theorem", "corollary"],
        StringComparer.Ordinal);

    public static IReadOnlyList<string> ListFrontierCompletionCandidates(
        string repositoryRoot)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string directory = Path.Combine(
            root,
            "work",
            "paper-frontier-formalization-progress",
            "certifications");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var frontiers = new List<string>();
        foreach (string child in Directory.EnumerateDirectories(directory)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            string hex = Path.GetFileName(child);
            if (hex.Length != 64
                || hex.Any(character =>
                    character is not ((>= '0' and <= '9')
                        or (>= 'a' and <= 'f'))))
            {
                throw new InvalidDataException(
                    "Frontier completion certification directory has a noncanonical identity.");
            }
            string frontierRef = "sha256:" + hex;
            if (File.Exists(CompletionCursorPath(root, frontierRef)))
            {
                continue;
            }
            PaperFrontierCertificationCursor[] cursors =
                ReadCertificationCursors(root, frontierRef).ToArray();
            if (cursors.Length == 0)
            {
                continue;
            }
            foreach (PaperFrontierCertificationCursor cursor in cursors)
            {
                Validate(cursor);
                if (!string.Equals(
                        cursor.FrontierRef,
                        frontierRef,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Frontier completion candidate directory contains a cross-frontier cursor.");
                }
            }
            frontiers.Add(frontierRef);
        }
        return frontiers;
    }

    public static PaperFrontierCompletionEvaluated EvaluateFrontierCompletion(
        string repositoryRoot,
        string frontierRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(frontierRef, nameof(frontierRef));

        PaperFrontierCertificationCursor[] observed =
            ReadCertificationCursors(root, frontierRef).ToArray();
        if (observed.Length == 0)
        {
            throw new InvalidDataException(
                "Frontier completion requires at least one certified frontier manifest.");
        }
        foreach (PaperFrontierCertificationCursor cursor in observed)
        {
            Validate(cursor);
            if (!string.Equals(
                    cursor.FrontierRef,
                    frontierRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier completion encountered a certification from another frontier.");
            }
        }

        PaperFrontierFormalizationProgressContext context =
            TryLoadProgressContext(root, observed[0].FormalizationRequestRef)
            ?? throw new InvalidDataException(
                "Frontier completion has no governed formalization binding.");
        if (!string.Equals(
                context.Source.Frontier.FrontierId,
                frontierRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion changed the governed frontier identity.");
        }

        using FileStream frontierLock = AcquireFrontierLock(root, frontierRef);
        RecoverProgressState(root, context);
        PaperFrontierCurrentStateCursor currentCursor =
            ReadCurrentStateCursor(CurrentStateCursorPath(root, frontierRef));
        PaperFormalizationFrontierState current =
            ReadStoredState(root, currentCursor.State);
        PaperFormalizationFrontierLifecycleService.Validate(
            current,
            context.Source.Frontier);

        string terminalCursorPath = CompletionCursorPath(root, frontierRef);
        if (File.Exists(terminalCursorPath))
        {
            PaperFrontierCompletionCursor existingCursor =
                ReadCompletionCursor(terminalCursorPath);
            ValidateCompletionReplay(root, context, current, existingCursor);
            PaperFrontierCompletionReceipt existingReceipt =
                ResearchStore(root).Get<PaperFrontierCompletionReceipt>(
                    existingCursor.CompletionRef);
            PaperManuscriptPlan existingPlan =
                ResearchStore(root).Get<PaperManuscriptPlan>(
                    existingCursor.ManuscriptPlanRef);
            Validate(
                existingReceipt,
                context.Source.Frontier,
                LoadStateByReference(
                    root,
                    context.Source.Frontier,
                    existingCursor.FrontierStateRef),
                existingPlan);
            PaperCertifiedClaimManifestService.Validate(existingPlan);
            return CompletedResult(
                existingCursor,
                existingReceipt,
                replayed: true);
        }

        HashSet<string> requiredClaimIds = RequiredClaimIds(
            context.Source.TheoremPackage);
        PaperFormalizationFrontierNode[] requiredNodes =
            context.Source.Frontier.FrontierContent.Nodes
                .Where(node => requiredClaimIds.Contains(node.ClaimId))
                .OrderBy(node => node.ParallelWave)
                .ThenByDescending(node => node.Priority)
                .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                .ToArray();
        if (requiredNodes.Length != requiredClaimIds.Count)
        {
            throw new InvalidDataException(
                "The formalization frontier omitted a required theorem-package claim.");
        }

        var stateByNode = current.StateContent.NodeStates.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);
        string[] missingNodeIds = requiredNodes
            .Where(node => !string.Equals(
                stateByNode[node.NodeId].Status,
                "manifested",
                StringComparison.Ordinal))
            .Select(node => node.NodeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missingNodeIds.Length != 0)
        {
            return PendingResult(
                root,
                frontierRef,
                current.StateId,
                context.Source.Program.ProgramContent.PaperId,
                missingNodeIds,
                [],
                PaperFrontierCompletionReasons.LoadBearingClaimsIncomplete,
                current.StateContent.UpdatedAt);
        }

        PaperFrontierCompletionMaterial[] materials = LoadCompletionMaterials(
            root,
            context,
            requiredNodes);
        PaperFrontierCoherentRelease? selected = SelectCoherentRelease(
            root,
            materials);
        if (selected is null)
        {
            string[] blocking = materials
                .Select(value => value.CertifiedClaim.CertifyingReleaseRef)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return PendingResult(
                root,
                frontierRef,
                current.StateId,
                context.Source.Program.ProgramContent.PaperId,
                [],
                blocking,
                PaperFrontierCompletionReasons.CoherentTruthReleaseAbsent,
                current.StateContent.UpdatedAt);
        }

        PaperManuscriptPlan plan = BuildManuscriptPlan(
            context,
            materials,
            selected.ReleaseRef);
        PaperCertifiedClaimManifestService.Validate(plan);
        byte[] planBytes = CanonicalJson.Serialize(plan);
        string storeRoot = Path.Combine(
            root,
            "artifacts",
            "research-input");
        PaperManuscriptPlanRegistration registration =
            PaperCertifiedClaimManifestService.RegisterPlan(
                storeRoot,
                planBytes,
                ManuscriptPlanCursorPath(root, frontierRef));
        string expectedPlanRef = PaperResearchInputStore.Reference(planBytes);
        if (!string.Equals(
                registration.ManuscriptPlanRef,
                expectedPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                registration.PaperId,
                context.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                registration.ManuscriptTruthReleaseRef,
                selected.ReleaseRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion changed the registered manuscript plan identity.");
        }

        PaperFrontierCompletionClaim[] completedClaims = materials
            .Select((value, index) => CompletionClaim(index + 1, context, value))
            .ToArray();
        var receipt = new PaperFrontierCompletionReceipt(
            PaperFrontierCompletionSchemas.Receipt,
            frontierRef,
            context.Source.PlanningCursor.TaskRef,
            current.StateId,
            context.Source.Program.ProgramContent.PaperId,
            context.Source.Program.TheoryProgramId,
            context.Source.TheoremPackage.TheoremPackageId,
            context.Source.PlanningCursor.TheoryAuditRef,
            context.Source.PlanningCursor.ScorecardRef,
            context.Source.PlanningCursor.PortfolioDecisionRef,
            requiredNodes.Select(value => value.NodeId).ToArray(),
            completedClaims,
            selected.ReleaseRef,
            selected.Release.ReleaseDigest,
            registration.ManuscriptPlanRef,
            plan.FormalClaims.Count,
            plan.InformalExposition.Count,
            current.StateContent.UpdatedAt);
        Validate(receipt, context.Source.Frontier, current, plan);
        string completionRef = ResearchStore(root).Put(receipt);

        var terminalCursor = new PaperFrontierCompletionCursor(
            PaperFrontierCompletionSchemas.Cursor,
            frontierRef,
            current.StateId,
            context.Source.Program.ProgramContent.PaperId,
            completionRef,
            registration.ManuscriptPlanRef,
            selected.ReleaseRef,
            selected.Release.ReleaseDigest,
            current.StateContent.UpdatedAt);
        Validate(terminalCursor);
        PutImmutable(
            terminalCursorPath,
            CanonicalJson.Serialize(terminalCursor));
        return CompletedResult(terminalCursor, receipt, replayed: false);
    }

    public static void Validate(PaperFrontierCompletionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!string.Equals(
                receipt.Schema,
                PaperFrontierCompletionSchemas.Receipt,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion receipt has the wrong schema.");
        }
        foreach (string digest in new[]
        {
            receipt.FrontierRef,
            receipt.FrontierPlanningTaskRef,
            receipt.FrontierStateRef,
            receipt.TheoryProgramRef,
            receipt.TheoremPackageRef,
            receipt.TheoryAuditRef,
            receipt.ScorecardRef,
            receipt.PortfolioDecisionRef,
            receipt.ManuscriptTruthReleaseRef,
            receipt.ManuscriptTruthReleaseDigest,
            receipt.ManuscriptPlanRef
        })
        {
            RequireDigest(digest, nameof(receipt));
        }
        RequirePaperId(receipt.PaperId);
        RequireCompletionDigestList(
            receipt.RequiredNodeIds,
            nameof(receipt.RequiredNodeIds),
            minimum: 1);
        if (receipt.Claims is null
            || receipt.Claims.Count != receipt.RequiredNodeIds.Count
            || receipt.FormalClaimCount < 1
            || receipt.InformalItemCount < 0)
        {
            throw new InvalidDataException(
                "Frontier completion receipt claim counts are invalid.");
        }
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.Ordinal);
        int formalCount = 0;
        int informalCount = 0;
        for (int index = 0; index < receipt.Claims.Count; index++)
        {
            PaperFrontierCompletionClaim claim = receipt.Claims[index]
                ?? throw new InvalidDataException(
                    "Frontier completion claims cannot contain null.");
            if (claim.Order != index + 1
                || !nodeIds.Add(claim.NodeId)
                || !claimIds.Add(claim.ClaimId)
                || !labels.Add(claim.LatexLabel))
            {
                throw new InvalidDataException(
                    "Frontier completion claim order and identities must be unique.");
            }
            foreach (string digest in new[]
            {
                claim.NodeId,
                claim.FrontierManifestRef,
                claim.CertifiedClaimRef,
                claim.FormalizationRequestRef,
                claim.CertifyingReleaseRef,
                claim.CertifyingReleaseDigest
            })
            {
                RequireDigest(digest, nameof(claim));
            }
            RequireClaimId(claim.ClaimId);
            RequireGid(claim.Gid);
            RequireCompletionText(
                claim.TheoremPackageKind,
                nameof(claim.TheoremPackageKind),
                128);
            RequireCompletionText(
                claim.ManuscriptDisposition,
                nameof(claim.ManuscriptDisposition),
                128);
            RequireCompletionText(claim.LatexLabel, nameof(claim.LatexLabel), 256);
            if (string.Equals(
                    claim.ManuscriptDisposition,
                    "formal-claim",
                    StringComparison.Ordinal))
            {
                RequireCompletionText(
                    claim.ManuscriptClaimKind,
                    nameof(claim.ManuscriptClaimKind),
                    128);
                if (!ManuscriptFormalKinds.Contains(
                        claim.ManuscriptClaimKind))
                {
                    throw new InvalidDataException(
                        "Frontier completion formal claim kind is unsupported.");
                }
                formalCount++;
            }
            else
            {
                if (!string.IsNullOrEmpty(claim.ManuscriptClaimKind)
                    || claim.ManuscriptDisposition is not (
                        "informal-definition"
                        or "informal-example"
                        or "informal-remark"))
                {
                    throw new InvalidDataException(
                        "Informal completion claims have an invalid disposition or claim kind.");
                }
                informalCount++;
            }
        }
        if (!receipt.RequiredNodeIds.SequenceEqual(
                receipt.Claims.Select(value => value.NodeId),
                StringComparer.Ordinal)
            || receipt.FormalClaimCount != formalCount
            || receipt.InformalItemCount != informalCount)
        {
            throw new InvalidDataException(
                "Frontier completion receipt arrays and counts disagree.");
        }
        ParseUtc(receipt.CompletedAt, nameof(receipt.CompletedAt));
    }

    public static void Validate(PaperFrontierCompletionPending pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (!string.Equals(
                pending.Schema,
                PaperFrontierCompletionSchemas.Pending,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion pending receipt has the wrong schema.");
        }
        RequireDigest(pending.FrontierRef, nameof(pending.FrontierRef));
        RequireDigest(
            pending.FrontierStateRef,
            nameof(pending.FrontierStateRef));
        RequirePaperId(pending.PaperId);
        RequireCompletionDigestList(
            pending.MissingNodeIds,
            nameof(pending.MissingNodeIds),
            minimum: 0);
        RequireCompletionDigestList(
            pending.BlockingReleaseRefs,
            nameof(pending.BlockingReleaseRefs),
            minimum: 0);
        if (pending.Reason is not (
                PaperFrontierCompletionReasons.LoadBearingClaimsIncomplete
                or PaperFrontierCompletionReasons.CoherentTruthReleaseAbsent)
            || (pending.Reason
                    == PaperFrontierCompletionReasons.LoadBearingClaimsIncomplete
                && pending.MissingNodeIds.Count == 0)
            || (pending.Reason
                    == PaperFrontierCompletionReasons.CoherentTruthReleaseAbsent
                && pending.BlockingReleaseRefs.Count == 0))
        {
            throw new InvalidDataException(
                "Frontier completion pending reason and evidence disagree.");
        }
        ParseUtc(pending.CheckedAt, nameof(pending.CheckedAt));
    }

    public static void Validate(PaperFrontierCompletionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if (!string.Equals(
                cursor.Schema,
                PaperFrontierCompletionSchemas.Cursor,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion cursor has the wrong schema.");
        }
        RequirePaperId(cursor.PaperId);
        foreach (string digest in new[]
        {
            cursor.FrontierRef,
            cursor.FrontierStateRef,
            cursor.PaperId,
            cursor.CompletionRef,
            cursor.ManuscriptPlanRef,
            cursor.ManuscriptTruthReleaseRef,
            cursor.ManuscriptTruthReleaseDigest
        })
        {
            RequireDigest(digest, nameof(cursor));
        }
        ParseUtc(cursor.CompletedAt, nameof(cursor.CompletedAt));
    }

    private static void Validate(
        PaperFrontierCompletionReceipt receipt,
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperManuscriptPlan plan)
    {
        Validate(receipt);
        PaperFormalizationFrontierService.Validate(frontier);
        PaperFormalizationFrontierLifecycleService.Validate(state, frontier);
        PaperCertifiedClaimManifestService.Validate(plan);
        if (!string.Equals(receipt.FrontierRef, frontier.FrontierId, StringComparison.Ordinal)
            || !string.Equals(receipt.FrontierStateRef, state.StateId, StringComparison.Ordinal)
            || !string.Equals(receipt.PaperId, frontier.FrontierContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(receipt.TheoryProgramRef, frontier.FrontierContent.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(receipt.TheoremPackageRef, frontier.FrontierContent.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(receipt.TheoryAuditRef, frontier.FrontierContent.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(receipt.ScorecardRef, frontier.FrontierContent.ScorecardRef, StringComparison.Ordinal)
            || !string.Equals(receipt.PortfolioDecisionRef, frontier.FrontierContent.PortfolioDecisionRef, StringComparison.Ordinal)
            || !string.Equals(receipt.PaperId, plan.PaperId, StringComparison.Ordinal)
            || !string.Equals(receipt.ManuscriptTruthReleaseRef, plan.ManuscriptTruthReleaseRef, StringComparison.Ordinal)
            || !string.Equals(receipt.ManuscriptPlanRef, PaperResearchInputStore.Reference(CanonicalJson.Serialize(plan)), StringComparison.Ordinal)
            || receipt.FormalClaimCount != plan.FormalClaims.Count
            || receipt.InformalItemCount != plan.InformalExposition.Count)
        {
            throw new InvalidDataException(
                "Frontier completion receipt is not exactly bound to its frontier state and manuscript plan.");
        }
    }

    private static PaperFrontierCompletionMaterial[] LoadCompletionMaterials(
        string root,
        PaperFrontierFormalizationProgressContext context,
        IReadOnlyList<PaperFormalizationFrontierNode> requiredNodes)
    {
        PaperFrontierCertificationCursor[] cursors =
            ReadCertificationCursors(
                root,
                context.Source.Frontier.FrontierId)
            .ToArray();
        var cursorByNode = new Dictionary<string, PaperFrontierCertificationCursor>(
            StringComparer.Ordinal);
        foreach (PaperFrontierCertificationCursor cursor in cursors)
        {
            Validate(cursor);
            if (!string.Equals(cursor.FrontierRef, context.Source.Frontier.FrontierId, StringComparison.Ordinal)
                || !cursorByNode.TryAdd(cursor.NodeId, cursor))
            {
                throw new InvalidDataException(
                    "Frontier completion certification cursors are duplicated or cross-frontier.");
            }
        }

        var packageClaims = context.Source.TheoremPackage.TheoremPackageContent.Claims
            .ToDictionary(value => value.ClaimId, StringComparer.Ordinal);
        var store = ResearchStore(root);
        var materials = new List<PaperFrontierCompletionMaterial>(requiredNodes.Count);
        foreach (PaperFormalizationFrontierNode node in requiredNodes)
        {
            if (!cursorByNode.TryGetValue(
                    node.NodeId,
                    out PaperFrontierCertificationCursor? cursor))
            {
                throw new InvalidDataException(
                    "A manifested required frontier node lacks a certification cursor.");
            }
            PaperFrontierCertifiedClaimManifest frontierManifest =
                ReadStoredEnvelope<PaperFrontierCertifiedClaimManifest>(
                    root,
                    cursor.CertifiedManifest,
                    "Frontier completion certified manifest");
            Validate(frontierManifest);
            PaperCertifiedClaim claim =
                store.Get<PaperCertifiedClaim>(cursor.CertifiedClaimRef);
            PaperCertificationWait wait =
                store.Get<PaperCertificationWait>(claim.CertificationWaitRef);
            PaperFormalizationDecision decision =
                store.Get<PaperFormalizationDecision>(wait.DecisionRef);
            PaperFormalizationOutcomeService.Validate(wait, decision);
            PaperCertificationRelease originalRelease =
                store.Get<PaperCertificationRelease>(claim.CertifyingReleaseRef);
            PaperCertificationService.Validate(originalRelease);
            PaperCertificationDeclaration declaration =
                originalRelease.Declarations.SingleOrDefault(value =>
                    string.Equals(value.Gid, claim.Gid, StringComparison.Ordinal)
                    && string.Equals(
                        value.FormalizationRequestRef,
                        claim.FormalizationRequestRef,
                        StringComparison.Ordinal))
                ?? throw new InvalidDataException(
                    "A completed frontier claim is absent from its certifying release.");
            PaperCertificationService.Validate(
                claim,
                wait,
                originalRelease,
                declaration);
            if (!packageClaims.TryGetValue(
                    node.ClaimId,
                    out PaperTheoremPackageClaim? packageClaim)
                || !string.Equals(cursor.ClaimId, node.ClaimId, StringComparison.Ordinal)
                || !string.Equals(frontierManifest.ManifestContent.NodeId, node.NodeId, StringComparison.Ordinal)
                || !string.Equals(frontierManifest.ManifestContent.ClaimId, node.ClaimId, StringComparison.Ordinal)
                || !string.Equals(frontierManifest.ManifestContent.CertifiedClaimRef, cursor.CertifiedClaimRef, StringComparison.Ordinal)
                || !string.Equals(frontierManifest.ManifestContent.FormalizationRequestRef, cursor.FormalizationRequestRef, StringComparison.Ordinal)
                || !string.Equals(claim.FormalizationRequestRef, cursor.FormalizationRequestRef, StringComparison.Ordinal)
                || !string.Equals(claim.ExpectedStatement, node.FormalStatement, StringComparison.Ordinal)
                || !string.Equals(claim.PaperId, context.Source.Program.ProgramContent.PaperId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier completion claim evidence changed the theorem package, node, request, or statement.");
            }
            materials.Add(new(
                node,
                packageClaim,
                cursor,
                frontierManifest,
                claim,
                originalRelease));
        }
        return materials.ToArray();
    }

    private static PaperFrontierCoherentRelease? SelectCoherentRelease(
        string root,
        IReadOnlyList<PaperFrontierCompletionMaterial> materials)
    {
        PaperFrontierCoherentRelease[] coherent = ReadRegisteredReleases(
                root,
                materials)
            .Where(candidate => ReleaseCoversAll(candidate.Release, materials))
            .OrderBy(candidate => candidate.Release.ReleaseDigest, StringComparer.Ordinal)
            .ToArray();
        PaperFrontierCoherentRelease[] maximal = coherent
            .Where(candidate => coherent.All(other =>
                string.Equals(
                    candidate.Release.ReleaseDigest,
                    other.Release.ReleaseDigest,
                    StringComparison.Ordinal)
                || candidate.Release.AncestorReleaseDigests.Contains(
                    other.Release.ReleaseDigest,
                    StringComparer.Ordinal)))
            .ToArray();
        return maximal.Length == 1 ? maximal[0] : null;
    }

    private static PaperFrontierCoherentRelease[] ReadRegisteredReleases(
        string root,
        IReadOnlyList<PaperFrontierCompletionMaterial> materials)
    {
        var byReference = new Dictionary<string, PaperCertificationRelease>(
            StringComparer.Ordinal);
        foreach (PaperFrontierCompletionMaterial material in materials)
        {
            byReference[material.CertifiedClaim.CertifyingReleaseRef] =
                material.OriginalRelease;
        }

        string directory = Path.Combine(
            root,
            "work",
            "research-input",
            "certification-releases");
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.EnumerateFiles(
                directory,
                "*.json",
                SearchOption.TopDirectoryOnly)
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                PaperCertificationReleaseCursor cursor =
                    PaperResearchInputJson.DeserializeStrict<
                        PaperCertificationReleaseCursor>(
                            ReadBoundedFile(
                                path,
                                MaximumControlBytes,
                                "Certification release cursor"));
                if (!string.Equals(
                        cursor.Schema,
                        PaperCertificationSchemas.ReleaseCursor,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Frontier completion encountered an invalid certification release cursor schema.");
                }
                RequireDigest(cursor.ReleaseRef, nameof(cursor.ReleaseRef));
                RequireDigest(cursor.ReleaseDigest, nameof(cursor.ReleaseDigest));
                PaperCertificationRelease release =
                    ResearchStore(root).Get<PaperCertificationRelease>(
                        cursor.ReleaseRef);
                PaperCertificationService.Validate(release);
                if (!string.Equals(
                        cursor.ReleaseDigest,
                        release.ReleaseDigest,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Certification release cursor changed the observed release digest.");
                }
                byReference[cursor.ReleaseRef] = release;
            }
        }

        return byReference
            .Select(value => new PaperFrontierCoherentRelease(
                value.Key,
                value.Value))
            .OrderBy(value => value.Release.ReleaseDigest, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ReleaseCoversAll(
        PaperCertificationRelease release,
        IReadOnlyList<PaperFrontierCompletionMaterial> materials)
    {
        PaperCertificationService.Validate(release);
        foreach (PaperFrontierCompletionMaterial material in materials)
        {
            PaperCertifiedClaim claim = material.CertifiedClaim;
            bool lineage = string.Equals(
                    release.ReleaseDigest,
                    claim.CertifyingReleaseDigest,
                    StringComparison.Ordinal)
                || release.AncestorReleaseDigests.Contains(
                    claim.CertifyingReleaseDigest,
                    StringComparer.Ordinal);
            PaperCertificationDeclaration? declaration =
                release.Declarations.SingleOrDefault(value =>
                    string.Equals(value.Gid, claim.Gid, StringComparison.Ordinal)
                    && string.Equals(
                        value.FormalizationRequestRef,
                        claim.FormalizationRequestRef,
                        StringComparison.Ordinal));
            if (!lineage
                || declaration is null
                || !string.Equals(declaration.LeanDeclaration, claim.LeanDeclaration, StringComparison.Ordinal)
                || !string.Equals(declaration.RequestedStatementDigest, claim.RequestedStatementDigest, StringComparison.Ordinal)
                || !string.Equals(declaration.StatementId, claim.StatementId, StringComparison.Ordinal)
                || !string.Equals(declaration.StatementCorrespondence, "exact", StringComparison.Ordinal)
                || !string.Equals(declaration.Kind, "theorem", StringComparison.Ordinal)
                || !declaration.AxiomClosure.SequenceEqual(
                    claim.AxiomClosure,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static PaperManuscriptPlan BuildManuscriptPlan(
        PaperFrontierFormalizationProgressContext context,
        IReadOnlyList<PaperFrontierCompletionMaterial> materials,
        string selectedReleaseRef)
    {
        PaperTheoremPackage package = context.Source.TheoremPackage;
        string mainClaimId = package.TheoremPackageContent.MainTheoremClaimIds
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "A completed theorem package must identify a main theorem.");
        PaperTheoremPackageClaim mainClaim = package.TheoremPackageContent.Claims
            .Single(value => string.Equals(
                value.ClaimId,
                mainClaimId,
                StringComparison.Ordinal));
        var formal = new List<PaperManuscriptFormalClaim>();
        var informal = new List<PaperManuscriptInformalItem>();
        foreach (PaperFrontierCompletionMaterial material in materials)
        {
            string kind = material.PackageClaim.Kind;
            if (ManuscriptFormalKinds.Contains(kind))
            {
                formal.Add(new PaperManuscriptFormalClaim(
                    material.PackageClaim.ClaimId,
                    ManuscriptLabel(kind, material.PackageClaim.ClaimId),
                    kind,
                    material.FrontierManifest.ManifestContent.CertifiedClaimRef,
                    material.Node.FormalStatement,
                    ManuscriptRole(package, material.PackageClaim)));
                continue;
            }

            string itemKind = kind switch
            {
                "definition" => "definition",
                "counterexample" => "example",
                "proof-interface" => "remark",
                _ => throw new InvalidDataException(
                    $"Completed load-bearing claim kind {kind} has no manuscript representation.")
            };
            informal.Add(new PaperManuscriptInformalItem(
                material.PackageClaim.ClaimId,
                ManuscriptLabel(itemKind, material.PackageClaim.ClaimId),
                itemKind,
                material.Node.FormalStatement,
                PaperCertifiedClaimManifestService.ExplicitlyInformal));
        }
        if (formal.Count == 0)
        {
            throw new InvalidDataException(
                "A completed frontier must contribute at least one formal manuscript claim.");
        }
        return new PaperManuscriptPlan(
            PaperClaimManifestSchemas.ManuscriptPlan,
            context.Source.Program.ProgramContent.PaperId,
            mainClaim.Title,
            selectedReleaseRef,
            formal,
            informal);
    }

    private static PaperFrontierCompletionClaim CompletionClaim(
        int order,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierCompletionMaterial material)
    {
        string kind = material.PackageClaim.Kind;
        bool formal = ManuscriptFormalKinds.Contains(kind);
        string disposition = formal
            ? "formal-claim"
            : kind switch
            {
                "definition" => "informal-definition",
                "counterexample" => "informal-example",
                "proof-interface" => "informal-remark",
                _ => throw new InvalidDataException(
                    $"Completed load-bearing claim kind {kind} has no manuscript disposition.")
            };
        return new PaperFrontierCompletionClaim(
            order,
            material.Node.NodeId,
            material.Node.ClaimId,
            kind,
            material.PackageClaim.LoadBearing,
            material.FrontierManifest.ManifestId,
            material.FrontierManifest.ManifestContent.CertifiedClaimRef,
            material.CertifiedClaim.FormalizationRequestRef,
            material.CertifiedClaim.Gid,
            material.CertifiedClaim.CertifyingReleaseRef,
            material.CertifiedClaim.CertifyingReleaseDigest,
            disposition,
            formal ? kind : string.Empty,
            ManuscriptLabel(formal ? kind : disposition, material.PackageClaim.ClaimId));
    }

    private static HashSet<string> RequiredClaimIds(PaperTheoremPackage package)
    {
        PaperTheoryDeepeningService.Validate(package);
        var required = package.TheoremPackageContent.Claims
            .Where(value => value.LoadBearing)
            .Select(value => value.ClaimId)
            .ToHashSet(StringComparer.Ordinal);
        required.UnionWith(package.TheoremPackageContent.MainTheoremClaimIds);
        required.UnionWith(package.TheoremPackageContent.SharpnessClaimIds);
        required.UnionWith(package.TheoremPackageContent.CorollaryClaimIds);
        if (required.Count == 0)
        {
            throw new InvalidDataException(
                "A theorem package cannot complete without required claims.");
        }
        return required;
    }

    private static string ManuscriptRole(
        PaperTheoremPackage package,
        PaperTheoremPackageClaim claim)
    {
        if (package.TheoremPackageContent.MainTheoremClaimIds.Contains(
                claim.ClaimId,
                StringComparer.Ordinal))
        {
            return "Main theorem in the independently audited theorem package.";
        }
        if (package.TheoremPackageContent.SharpnessClaimIds.Contains(
                claim.ClaimId,
                StringComparer.Ordinal))
        {
            return "Sharpness theorem establishing the exact boundary of the main result.";
        }
        if (package.TheoremPackageContent.CorollaryClaimIds.Contains(
                claim.ClaimId,
                StringComparer.Ordinal))
        {
            return "Corollary derived from the audited main theorem and sharpness chain.";
        }
        return "Load-bearing formal dependency in the audited proof architecture.";
    }

    private static string ManuscriptLabel(string kind, string claimId)
    {
        string prefix = kind switch
        {
            "theorem" => "thm:",
            "lemma" => "lem:",
            "proposition" => "prop:",
            "corollary" => "cor:",
            "definition" or "informal-definition" => "def:",
            "example" or "informal-example" => "ex:",
            "remark" or "informal-remark" => "rem:",
            _ => throw new InvalidDataException(
                $"Unsupported manuscript label kind {kind}.")
        };
        int separator = claimId.IndexOf(':');
        string suffix = separator >= 0
            ? claimId[(separator + 1)..]
            : claimId;
        suffix = new string(suffix.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_').ToArray());
        if (string.IsNullOrEmpty(suffix)
            || !char.IsLetterOrDigit(suffix[0]))
        {
            suffix = "claim_" + suffix;
        }
        return prefix + suffix;
    }

    private static PaperFrontierCompletionEvaluated PendingResult(
        string root,
        string frontierRef,
        string stateRef,
        string paperId,
        IReadOnlyList<string> missingNodeIds,
        IReadOnlyList<string> blockingReleaseRefs,
        string reason,
        string checkedAt)
    {
        var pending = new PaperFrontierCompletionPending(
            PaperFrontierCompletionSchemas.Pending,
            frontierRef,
            stateRef,
            paperId,
            missingNodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            blockingReleaseRefs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            reason,
            checkedAt);
        Validate(pending);
        string pendingRef = ResearchStore(root).Put(pending);
        return new PaperFrontierCompletionEvaluated(
            PaperFrontierCompletionSchemas.Evaluated,
            PaperFrontierCompletionStatuses.Pending,
            frontierRef,
            stateRef,
            paperId,
            string.Empty,
            pendingRef,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            pending.MissingNodeIds,
            reason,
            false);
    }

    private static PaperFrontierCompletionEvaluated CompletedResult(
        PaperFrontierCompletionCursor cursor,
        PaperFrontierCompletionReceipt receipt,
        bool replayed) =>
        new(
            PaperFrontierCompletionSchemas.Evaluated,
            PaperFrontierCompletionStatuses.Completed,
            cursor.FrontierRef,
            cursor.FrontierStateRef,
            cursor.PaperId,
            cursor.CompletionRef,
            string.Empty,
            cursor.ManuscriptPlanRef,
            cursor.ManuscriptTruthReleaseRef,
            cursor.ManuscriptTruthReleaseDigest,
            receipt.FormalClaimCount,
            receipt.InformalItemCount,
            [],
            PaperFrontierCompletionReasons.Complete,
            replayed);

    private static void ValidateCompletionReplay(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFormalizationFrontierState current,
        PaperFrontierCompletionCursor cursor)
    {
        Validate(cursor);
        if (!string.Equals(cursor.FrontierRef, context.Source.Frontier.FrontierId, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, context.Source.Program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion cursor changed the frontier identity.");
        }
        PaperFrontierCompletionReceipt receipt =
            ResearchStore(root).Get<PaperFrontierCompletionReceipt>(
                cursor.CompletionRef);
        PaperManuscriptPlan plan =
            ResearchStore(root).Get<PaperManuscriptPlan>(
                cursor.ManuscriptPlanRef);
        PaperFormalizationFrontierState completedState =
            LoadStateByReference(root, context.Source.Frontier, cursor.FrontierStateRef);
        Validate(receipt, context.Source.Frontier, completedState, plan);
        RequireEventSubset(completedState, current);
        if (!string.Equals(receipt.ManuscriptTruthReleaseRef, cursor.ManuscriptTruthReleaseRef, StringComparison.Ordinal)
            || !string.Equals(receipt.ManuscriptTruthReleaseDigest, cursor.ManuscriptTruthReleaseDigest, StringComparison.Ordinal)
            || !string.Equals(receipt.ManuscriptPlanRef, cursor.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(receipt.CompletedAt, cursor.CompletedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier completion cursor changed its terminal receipt or manuscript plan.");
        }
    }

    private static PaperFormalizationFrontierState LoadStateByReference(
        string root,
        PaperFormalizationFrontier frontier,
        string stateRef)
    {
        PaperFrontierCurrentStateCursor current =
            ReadCurrentStateCursor(CurrentStateCursorPath(root, frontier.FrontierId));
        if (string.Equals(current.State.ArtifactRef, stateRef, StringComparison.Ordinal))
        {
            return ReadStoredState(root, current.State);
        }
        foreach (PaperFrontierCertificationCursor cursor
            in ReadCertificationCursors(root, frontier.FrontierId))
        {
            if (string.Equals(cursor.FrontierState.ArtifactRef, stateRef, StringComparison.Ordinal))
            {
                return ReadStoredState(root, cursor.FrontierState);
            }
        }
        throw new InvalidDataException(
            "Frontier completion state is absent from the current or certification lineage.");
    }

    private static PaperFrontierCompletionCursor ReadCompletionCursor(string path)
    {
        PaperFrontierCompletionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierCompletionCursor>(
                ReadBoundedFile(
                    path,
                    MaximumControlBytes,
                    "Frontier completion cursor"));
        Validate(cursor);
        return cursor;
    }

    private static void RequireCompletionText(
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

    private static void RequireCompletionDigestList(
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
                throw new InvalidDataException(
                    $"{name} must contain unique references.");
            }
        }
    }

    private static string CompletionCursorPath(string root, string frontierRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-completions",
            Hex(frontierRef) + ".json");

    private static string ManuscriptPlanCursorPath(string root, string frontierRef) =>
        Path.Combine(
            root,
            "work",
            "manuscript-plans",
            Hex(frontierRef) + ".json");
}
