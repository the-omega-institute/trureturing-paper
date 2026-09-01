namespace Trureturing.Paper.Core;

internal sealed record PaperManuscriptAuthoringContext(
    PaperManuscriptClaimEvaluation Evaluation,
    PaperCertifiedClaimManifest ClaimManifest,
    PaperManuscriptEligibility Eligibility,
    PaperManuscriptPlan Plan,
    PaperFrontierCompletionCursor CompletionCursor,
    PaperFrontierCompletionReceipt Completion,
    PaperFrontierFormalizationProgressContext Progress,
    PaperFrontierPlanningContext Planning,
    PaperCertificationRelease SelectedRelease,
    IReadOnlyDictionary<string, PaperCertifiedClaim> CertifiedClaims,
    IReadOnlyList<PaperAgentInputArtifact> ExactInputs);

public static partial class PaperFrontierNodeSelectionService
{
    internal static PaperManuscriptAuthoringContext
        LoadManuscriptAuthoringContext(
            string repositoryRoot,
            string evaluationRef,
            string claimManifestRef,
            string eligibilityRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(evaluationRef, nameof(evaluationRef));
        RequireDigest(claimManifestRef, nameof(claimManifestRef));
        RequireDigest(eligibilityRef, nameof(eligibilityRef));

        PaperResearchInputStore store = ResearchStore(root);
        PaperManuscriptClaimEvaluation evaluation =
            store.Get<PaperManuscriptClaimEvaluation>(evaluationRef);
        PaperCertifiedClaimManifestService.Validate(evaluation);
        if (!string.Equals(
                evaluation.Outcome,
                PaperClaimManifestOutcomes.Eligible,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.ClaimManifestRef,
                claimManifestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.EligibilityRef,
                eligibilityRef,
                StringComparison.Ordinal)
            || evaluation.PendingRef is not null
            || evaluation.IneligibilityRef is not null)
        {
            throw new InvalidDataException(
                "Manuscript authoring requires one eligible claim-manifest evaluation.");
        }

        PaperCertifiedClaimManifest manifest =
            store.Get<PaperCertifiedClaimManifest>(claimManifestRef);
        PaperManuscriptEligibility eligibility =
            store.Get<PaperManuscriptEligibility>(eligibilityRef);
        PaperManuscriptPlan plan =
            store.Get<PaperManuscriptPlan>(evaluation.ManuscriptPlanRef);
        PaperCertifiedClaimManifestService.Validate(plan);
        PaperCertificationRelease selectedRelease =
            store.Get<PaperCertificationRelease>(
                plan.ManuscriptTruthReleaseRef);
        PaperCertificationService.Validate(selectedRelease);

        var certifiedClaims = new Dictionary<string, PaperCertifiedClaim>(
            StringComparer.Ordinal);
        foreach (PaperManuscriptFormalClaim planned in plan.FormalClaims)
        {
            PaperCertifiedClaim claim =
                store.Get<PaperCertifiedClaim>(planned.CertifiedClaimRef);
            if (!certifiedClaims.TryAdd(planned.CertifiedClaimRef, claim))
            {
                throw new InvalidDataException(
                    "Manuscript plan repeats a certified claim reference.");
            }
        }
        PaperCertifiedClaimManifestService.Validate(
            manifest,
            plan,
            selectedRelease,
            certifiedClaims);
        PaperCertifiedClaimManifestService.Validate(
            eligibility,
            manifest);
        if (!string.Equals(
                manifest.ManuscriptPlanRef,
                evaluation.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                eligibility.ManuscriptPlanRef,
                evaluation.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                eligibility.ClaimManifestRef,
                claimManifestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.ManuscriptTruthReleaseRef,
                selectedRelease.ReleaseDigest == string.Empty
                    ? string.Empty
                    : plan.ManuscriptTruthReleaseRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Eligible manuscript evidence changed its plan, manifest, or selected release.");
        }

        PaperFrontierCompletionCursor completionCursor =
            FindCompletionCursor(
                root,
                evaluation.ManuscriptPlanRef);
        PaperFrontierCompletionEvaluated replay =
            EvaluateFrontierCompletion(
                root,
                completionCursor.FrontierRef);
        if (!string.Equals(
                replay.Status,
                PaperFrontierCompletionStatuses.Completed,
                StringComparison.Ordinal)
            || !string.Equals(
                replay.CompletionRef,
                completionCursor.CompletionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                replay.ManuscriptPlanRef,
                evaluation.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                replay.ManuscriptTruthReleaseRef,
                plan.ManuscriptTruthReleaseRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript authoring source is not a replayable completed frontier.");
        }

        PaperFrontierCompletionReceipt completion =
            store.Get<PaperFrontierCompletionReceipt>(
                completionCursor.CompletionRef);
        Validate(completion);
        PaperFrontierCompletionClaim firstClaim =
            completion.Claims.FirstOrDefault()
            ?? throw new InvalidDataException(
                "Completed frontier contains no manuscript claim evidence.");
        PaperFrontierFormalizationProgressContext progress =
            TryLoadProgressContext(
                root,
                firstClaim.FormalizationRequestRef)
            ?? throw new InvalidDataException(
                "Completed frontier claim is not bound to a governed Formalize request.");
        PaperFrontierPlanningContext planning =
            PaperFrontierPlanningAgentService.ReopenContext(
                root,
                progress.Source.PlanningDispatch);

        if (!string.Equals(
                completion.FrontierRef,
                progress.Source.Frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.PaperId,
                planning.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.TheoryProgramRef,
                planning.Program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.TheoremPackageRef,
                planning.TheoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.TheoryAuditRef,
                planning.Audit.AuditId,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.ManuscriptPlanRef,
                evaluation.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.ManuscriptTruthReleaseRef,
                plan.ManuscriptTruthReleaseRef,
                StringComparison.Ordinal)
            || !string.Equals(
                completion.ManuscriptTruthReleaseDigest,
                selectedRelease.ReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.PaperId,
                completion.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                eligibility.PaperId,
                completion.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript authoring evidence does not form one completed paper lineage.");
        }

        PaperAgentInputArtifact[] inputs = BuildManuscriptAuthoringInputs(
            root,
            evaluationRef,
            claimManifestRef,
            eligibilityRef,
            evaluation,
            completionCursor,
            completion,
            progress,
            planning,
            selectedRelease);
        return new(
            evaluation,
            manifest,
            eligibility,
            plan,
            completionCursor,
            completion,
            progress,
            planning,
            selectedRelease,
            certifiedClaims,
            inputs);
    }

    private static PaperFrontierCompletionCursor FindCompletionCursor(
        string root,
        string manuscriptPlanRef)
    {
        string directory = Path.Combine(
            root,
            "work",
            "paper-frontier-completions");
        if (!Directory.Exists(directory))
        {
            throw new InvalidDataException(
                "Eligible manuscript plan has no frontier completion directory.");
        }
        PaperFrontierCompletionCursor[] matches = Directory.EnumerateFiles(
                directory,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(ReadCompletionCursor)
            .Where(value => string.Equals(
                value.ManuscriptPlanRef,
                manuscriptPlanRef,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException(
                "Eligible manuscript plan is not backed by a frontier completion cursor."),
            _ => throw new InvalidDataException(
                "Eligible manuscript plan is backed by multiple frontier completions.")
        };
    }

    private static PaperAgentInputArtifact[] BuildManuscriptAuthoringInputs(
        string root,
        string evaluationRef,
        string claimManifestRef,
        string eligibilityRef,
        PaperManuscriptClaimEvaluation evaluation,
        PaperFrontierCompletionCursor completionCursor,
        PaperFrontierCompletionReceipt completion,
        PaperFrontierFormalizationProgressContext progress,
        PaperFrontierPlanningContext planning,
        PaperCertificationRelease selectedRelease)
    {
        PaperFrontierPlanningAgentDispatch dispatch =
            progress.Source.PlanningDispatch;
        var inputs = new List<PaperAgentInputArtifact>
        {
            ResearchStoreInput(
                root,
                PaperClaimManifestSchemas.Evaluation,
                evaluationRef),
            ResearchStoreInput(
                root,
                PaperClaimManifestSchemas.CertifiedClaimManifest,
                claimManifestRef),
            ResearchStoreInput(
                root,
                PaperClaimManifestSchemas.ManuscriptEligibility,
                eligibilityRef),
            ResearchStoreInput(
                root,
                PaperClaimManifestSchemas.ManuscriptPlan,
                evaluation.ManuscriptPlanRef),
            ResearchStoreInput(
                root,
                PaperFrontierCompletionSchemas.Receipt,
                completionCursor.CompletionRef),
            ResearchStoreInput(
                root,
                PaperCertificationSchemas.ReleaseObservation,
                completion.ManuscriptTruthReleaseRef),
            RequiredPlanningInput(
                dispatch,
                PaperPortfolioSchemas.TheoryProgram,
                planning.Program.TheoryProgramId),
            RequiredPlanningInput(
                dispatch,
                PaperTheoryFoundationSchemas.Scope,
                planning.Scope.ScopeId),
            RequiredPlanningInput(
                dispatch,
                PaperTheoryFoundationSchemas.Inventory,
                planning.Inventory.InventoryId),
            RequiredPlanningInput(
                dispatch,
                PaperTheoryDeepeningSchemas.TheoremPackage,
                planning.TheoremPackage.TheoremPackageId),
            RequiredPlanningInput(
                dispatch,
                PaperTheoryAuditSchemas.Audit,
                planning.Audit.AuditId),
            RequiredPlanningInput(
                dispatch,
                CandidateArtifactSchemas.CandidatePaper,
                planning.Program.ProgramContent.CandidatePaperRef),
            RequiredPlanningInput(
                dispatch,
                CandidateArtifactSchemas.LiteratureResearch,
                planning.Program.ProgramContent.LiteratureResearchRef),
            new PaperAgentInputArtifact(
                PaperFormalizationFrontierSchemas.Frontier,
                progress.Source.PlanningCursor.Frontier.EnvelopeRef,
                progress.Source.PlanningCursor.Frontier.EnvelopePath)
        };
        string[] refs = inputs.Select(value => value.ArtifactRef).ToArray();
        string[] paths = inputs.Select(value => value.RepositoryRelativePath).ToArray();
        if (refs.Distinct(StringComparer.Ordinal).Count() != refs.Length
            || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
        {
            throw new InvalidDataException(
                "Manuscript authoring exact input closure contains duplicate refs or paths.");
        }
        if (!string.Equals(
                selectedRelease.ReleaseDigest,
                completion.ManuscriptTruthReleaseDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript authoring selected release changed during input construction.");
        }
        return inputs
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
    }

    private static PaperAgentInputArtifact ResearchStoreInput(
        string root,
        string schema,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
        string hex = reference["sha256:".Length..];
        string relative = Path.Combine(
                "artifacts",
                "research-input",
                "sha256",
                hex[..2],
                hex + ".json")
            .Replace(Path.DirectorySeparatorChar, '/');
        string full = Path.Combine(
            root,
            relative.Replace('/', Path.DirectorySeparatorChar));
        byte[] bytes = ReadBoundedFile(
            full,
            MaximumArtifactBytes,
            $"Manuscript authoring input {schema}");
        if (!string.Equals(
                PaperResearchInputStore.Reference(bytes),
                reference,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Manuscript authoring input {schema} failed content-address verification.");
        }
        return new(schema, reference, relative);
    }

    private static PaperAgentInputArtifact RequiredPlanningInput(
        PaperFrontierPlanningAgentDispatch dispatch,
        string schema,
        string reference)
    {
        return dispatch.ExactInputs.SingleOrDefault(value =>
                string.Equals(value.Schema, schema, StringComparison.Ordinal)
                && string.Equals(
                    value.ArtifactRef,
                    reference,
                    StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Frontier planning evidence lacks exact manuscript input {schema}:{reference}.");
    }
}
