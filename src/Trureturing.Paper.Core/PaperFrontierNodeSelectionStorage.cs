namespace Trureturing.Paper.Core;

public static partial class PaperFrontierNodeSelectionService
{
    private static PaperFrontierNodeSelectionSource LoadSource(
        string root,
        string frontierPlanningTaskRef,
        string nodeId)
    {
        string planningCursorPath = PlanningAdmissionCursorPath(
            root,
            frontierPlanningTaskRef);
        PaperFrontierPlanningAgentAdmissionCursor planningCursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentAdmissionCursor>(
                ReadBoundedFile(
                    planningCursorPath,
                    MaximumControlBytes,
                    "Frontier-planning admission cursor"));
        PaperFrontierPlanningAgentService.Validate(planningCursor);
        if (!string.Equals(
                planningCursor.TaskRef,
                frontierPlanningTaskRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source cursor changed the planning task identity.");
        }

        PaperFrontierPlanningNodeRoute route = planningCursor.InitialNodeRoutes
            .SingleOrDefault(value => string.Equals(
                value.NodeId,
                nodeId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning admission did not release the requested node.");
        if (route.ParallelWave != 0
            || !string.Equals(
                route.NextRoute,
                "governed-selection",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an admitted wave-zero governed-selection route may be selected.");
        }

        string planningDispatchPath = PlanningDispatchPath(
            root,
            planningCursor.DispatchRef);
        byte[] planningDispatchBytes = ReadImmutable(
            planningDispatchPath,
            planningCursor.DispatchRef,
            "Frontier-planning dispatch");
        PaperFrontierPlanningAgentDispatch planningDispatch =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentDispatch>(
                planningDispatchBytes);
        PaperFrontierPlanningAgentService.Validate(planningDispatch);

        PaperFormalizationFrontier frontier =
            ReadPlanningStoredEnvelope<PaperFormalizationFrontier>(
                root,
                planningCursor.Frontier,
                PaperFormalizationFrontierSchemas.Frontier,
                "Formalization frontier");
        PaperFormalizationFrontierService.Validate(frontier);
        PaperFormalizationFrontierState initialState =
            ReadPlanningStoredEnvelope<PaperFormalizationFrontierState>(
                root,
                planningCursor.InitialState,
                PaperFormalizationFrontierSchemas.FrontierState,
                "Initial formalization frontier state");
        PaperFormalizationFrontierLifecycleService.Validate(
            initialState,
            frontier);

        PaperAgentInputArtifact programInput = FindExactInput(
            planningDispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            planningDispatch.TheoryProgramRef);
        PaperTheoryProgramContent programContent =
            ReadExactContent<PaperTheoryProgramContent>(root, programInput);
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            planningDispatch.TheoryProgramRef,
            programContent);
        PaperPortfolioService.Validate(program);

        PaperAgentInputArtifact packageInput = FindExactInput(
            planningDispatch.ExactInputs,
            PaperTheoryDeepeningSchemas.TheoremPackage,
            planningDispatch.TheoremPackageRef);
        PaperTheoremPackageContent packageContent =
            ReadExactContent<PaperTheoremPackageContent>(root, packageInput);
        var theoremPackage = new PaperTheoremPackage(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            planningDispatch.TheoremPackageRef,
            packageContent);
        PaperTheoryDeepeningService.Validate(theoremPackage);

        PaperFormalizationFrontierNode node =
            PaperFormalizationFrontierService.RequireNode(frontier, nodeId);
        PaperFormalizationFrontierNodeState initialNodeState =
            initialState.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        PaperTheoremPackageClaim claim = theoremPackage.TheoremPackageContent.Claims
            .SingleOrDefault(value => string.Equals(
                value.ClaimId,
                node.ClaimId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier node claim is absent from the admitted theorem package.");

        var researchStore = new PaperResearchInputStore(
            Path.Combine(root, "artifacts", "research-input"));
        PaperResearchInput researchInput = researchStore.Get<PaperResearchInput>(
            program.ProgramContent.PaperResearchInputRef);
        PaperResearchInputValidation.Validate(researchInput);

        if (!string.Equals(
                planningCursor.ResultRef,
                planningDispatch.PortfolioResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioTaskRef,
                planningDispatch.PortfolioTaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioResultRef,
                planningDispatch.PortfolioResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PaperId,
                planningDispatch.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.TheoryProgramRef,
                planningDispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.TheoremPackageRef,
                planningDispatch.TheoremPackageRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioDecisionRef,
                planningDispatch.PortfolioDecisionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source cursor and planning dispatch disagree.");
        }
        if (!string.Equals(
                planningCursor.Frontier.ArtifactRef,
                frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.InitialState.ArtifactRef,
                initialState.StateId,
                StringComparison.Ordinal)
            || !string.Equals(
                initialState.StateContent.FrontierRef,
                frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.TheoremPackageRef,
                theoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.PortfolioDecisionRef,
                planningCursor.PortfolioDecisionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source artifacts do not form one planning admission.");
        }
        if (!string.Equals(
                program.ProgramContent.PaperId,
                planningCursor.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.CandidatePaperRef,
                planningDispatch.CandidatePaperRef,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.LiteratureResearchRef,
                planningDispatch.LiteratureResearchRef,
                StringComparison.Ordinal)
            || !string.Equals(
                theoremPackage.TheoremPackageContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                theoremPackage.TheoremPackageContent.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection program and theorem package identities disagree.");
        }
        if (!string.Equals(route.ClaimId, node.ClaimId, StringComparison.Ordinal)
            || !string.Equals(
                route.FormalizationKind,
                node.FormalizationKind,
                StringComparison.Ordinal)
            || route.Priority != node.Priority
            || route.DispatchOrder < 1
            || node.ParallelWave != 0
            || node.DependencyNodeIds.Count != 0
            || !string.Equals(
                initialNodeState.Status,
                PaperFormalizationFrontierService.InitialNodeStatus,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.Statement,
                node.InformalStatement,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection route changed the admitted wave-zero node.");
        }
        if (!string.Equals(
                researchInput.TruthReleaseDigest,
                frontier.FrontierContent.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TopologyDigest,
                frontier.FrontierContent.TopologyDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.PaperResearchInputRef,
                frontier.FrontierContent.PaperResearchInputRef,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TruthReleaseDigest,
                program.ProgramContent.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TopologyDigest,
                program.ProgramContent.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier node is not bound to the exact Paper research input release.");
        }

        return new(
            planningCursor,
            planningDispatch,
            frontier,
            initialState,
            program,
            theoremPackage,
            route,
            node,
            researchInput);
    }

    private static PaperFormalizationFrontierState ReadOrInitializeCurrentState(
        string root,
        PaperFrontierNodeSelectionSource source)
    {
        string cursorPath = CurrentStateCursorPath(
            root,
            source.Frontier.FrontierId);
        if (!File.Exists(cursorPath))
        {
            var initialStored = new PaperFrontierNodeSelectionStoredArtifact(
                PaperFormalizationFrontierSchemas.FrontierState,
                source.InitialState.StateId,
                source.PlanningCursor.InitialState.EnvelopeRef,
                source.PlanningCursor.InitialState.EnvelopePath);
            var initialCursor = new PaperFrontierCurrentStateCursor(
                PaperFrontierNodeSelectionSchemas.CurrentStateCursor,
                source.Frontier.FrontierId,
                initialStored,
                source.InitialState.StateContent.Version,
                source.InitialState.StateContent.UpdatedAt);
            Validate(initialCursor);
            PutImmutable(cursorPath, CanonicalJson.Serialize(initialCursor));
            return source.InitialState;
        }

        PaperFrontierCurrentStateCursor current =
            ReadCurrentStateCursor(cursorPath);
        if (!string.Equals(
                current.FrontierRef,
                source.Frontier.FrontierId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Current frontier state cursor changed its frontier identity.");
        }
        PaperFormalizationFrontierState state = ReadStoredState(root, current.State);
        PaperFormalizationFrontierLifecycleService.Validate(
            state,
            source.Frontier);
        if (current.Version != state.StateContent.Version
            || !string.Equals(
                current.UpdatedAt,
                state.StateContent.UpdatedAt,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Current frontier state cursor does not match its immutable state.");
        }
        return state;
    }

    private static void WriteCurrentStateCursor(
        string root,
        PaperFormalizationFrontier frontier,
        PaperFrontierNodeSelectionStoredArtifact storedState,
        PaperFormalizationFrontierState state)
    {
        var cursor = new PaperFrontierCurrentStateCursor(
            PaperFrontierNodeSelectionSchemas.CurrentStateCursor,
            frontier.FrontierId,
            storedState,
            state.StateContent.Version,
            state.StateContent.UpdatedAt);
        Validate(cursor);
        PaperResearchInputStore.WriteAtomic(
            CurrentStateCursorPath(root, frontier.FrontierId),
            CanonicalJson.Serialize(cursor),
            overwrite: true);
    }

    private static void RepairCurrentStatePointer(
        string root,
        PaperFrontierNodeSelectionSource source,
        PaperFrontierNodeSelectionAdmissionCursor replay)
    {
        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, source);
        PaperFormalizationFrontierState replayState =
            ReadStoredState(root, replay.FrontierState);
        PaperFormalizationFrontierLifecycleService.Validate(
            replayState,
            source.Frontier);
        if (current.StateContent.Version < replayState.StateContent.Version)
        {
            RequireEventSubset(current, replayState);
            WriteCurrentStateCursor(
                root,
                source.Frontier,
                replay.FrontierState,
                replayState);
            return;
        }
        if (current.StateContent.Version == replayState.StateContent.Version)
        {
            if (!string.Equals(current.StateId, replayState.StateId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Equal-version frontier states have divergent identities.");
            }
            return;
        }
        RequireEventSubset(replayState, current);
    }

    private static void RequireEventSubset(
        PaperFormalizationFrontierState earlier,
        PaperFormalizationFrontierState later)
    {
        if (earlier.StateContent.AppliedEventRefs.Any(reference =>
                !later.StateContent.AppliedEventRefs.Contains(
                    reference,
                    StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                "Frontier state history diverged from the admitted event lineage.");
        }
    }

    private static FileStream AcquireFrontierLock(
        string root,
        string frontierRef)
    {
        string path = FrontierLockPath(root, frontierRef);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        for (int attempt = 0; attempt < 200; attempt++)
        {
            try
            {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (attempt < 199)
            {
                Thread.Sleep(25);
            }
        }
        throw new InvalidDataException(
            "Could not acquire the frontier state admission lock.");
    }

    private static PaperFrontierNodeSelectionStoredArtifact StoreEnvelope<T>(
        string root,
        string family,
        string schema,
        string artifactRef,
        T envelope)
    {
        RequireSchema(schema, nameof(schema));
        RequireDigest(artifactRef, nameof(artifactRef));
        byte[] bytes = CanonicalJson.Serialize(envelope);
        string blobRef = ByteReference(bytes);
        string path = DomainArtifactPath(root, family, blobRef);
        PutImmutable(path, bytes);
        return new(
            schema,
            artifactRef,
            blobRef,
            RelativePath(root, path));
    }

    private static (string BlobRef, string Path) StoreResearchSelection(
        string root,
        string authorizationRef,
        PaperResearchSelection selection)
    {
        PaperResearchSelectionService.Validate(selection);
        byte[] bytes = PaperResearchSelectionJson.Write(selection);
        string blobRef = ByteReference(bytes);
        string path = ResearchSelectionArtifactPath(
            root,
            authorizationRef,
            "paper-research-selection.v1.json");
        PutImmutable(path, bytes);
        return (blobRef, path);
    }

    private static (string BlobRef, string Path) StoreFormalizationRequest(
        string root,
        string authorizationRef,
        FormalizationRequest request)
    {
        PaperResearchSelectionService.Validate(request);
        byte[] bytes = PaperResearchSelectionJson.Write(request);
        string blobRef = ByteReference(bytes);
        string path = ResearchSelectionArtifactPath(
            root,
            authorizationRef,
            "formalization-request.v1.json");
        PutImmutable(path, bytes);
        return (blobRef, path);
    }

    private static void WriteBindingLookup(
        string root,
        string requestRef,
        PaperFrontierNodeSelectionStoredArtifact binding)
    {
        var lookup = new PaperFrontierFormalizationBindingLookup(
            PaperFrontierNodeSelectionSchemas.BindingLookup,
            requestRef,
            binding.ArtifactRef,
            binding.BlobRef,
            binding.RepositoryRelativePath);
        Validate(lookup);
        PutImmutable(
            BindingLookupPath(root, requestRef),
            CanonicalJson.Serialize(lookup));
    }

    private static PaperFrontierNodeSelectionAdmissionCursor ReadAdmissionCursor(
        string path)
    {
        PaperFrontierNodeSelectionAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierNodeSelectionAdmissionCursor>(
                ReadBoundedFile(
                    path,
                    MaximumControlBytes,
                    "Frontier node selection cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperFrontierCurrentStateCursor ReadCurrentStateCursor(
        string path)
    {
        PaperFrontierCurrentStateCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierCurrentStateCursor>(
                ReadBoundedFile(
                    path,
                    MaximumControlBytes,
                    "Current frontier state cursor"));
        Validate(cursor);
        return cursor;
    }

    private static T ReadPlanningStoredEnvelope<T>(
        string root,
        PaperFrontierPlanningStoredArtifact stored,
        string expectedSchema,
        string name)
    {
        if (!string.Equals(stored.Schema, expectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} has the wrong stored schema.");
        }
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            name);
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static PaperFormalizationFrontierState ReadStoredState(
        string root,
        PaperFrontierNodeSelectionStoredArtifact stored)
    {
        RequireStoredArtifact(
            stored,
            PaperFormalizationFrontierSchemas.FrontierState);
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.RepositoryRelativePath,
            stored.BlobRef,
            "Stored frontier state");
        PaperFormalizationFrontierState state =
            PaperResearchInputJson.DeserializeStrict<PaperFormalizationFrontierState>(
                bytes);
        PaperFormalizationFrontierLifecycleService.Validate(state);
        if (!string.Equals(state.StateId, stored.ArtifactRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored frontier state changed its semantic identity.");
        }
        return state;
    }

    private static T ReadExactContent<T>(
        string root,
        PaperAgentInputArtifact input) =>
        PaperResearchInputJson.DeserializeStrict<T>(
            ReadRepositoryArtifact(
                root,
                input.RepositoryRelativePath,
                input.ArtifactRef,
                $"Exact frontier-planning input {input.Schema}"));

    private static PaperAgentInputArtifact FindExactInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string reference) =>
        inputs.SingleOrDefault(input =>
                string.Equals(input.Schema, schema, StringComparison.Ordinal)
                && string.Equals(input.ArtifactRef, reference, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Frontier selection is missing exact input {schema} at {reference}.");

    private static byte[] ReadRepositoryArtifact(
        string root,
        string relativePath,
        string expectedRef,
        string name)
    {
        RequireRepositoryRelativePath(relativePath, name);
        string first = relativePath.Split('/')[0];
        if (!AllowedRepositoryRoots.Contains(first))
        {
            throw new InvalidDataException(
                $"{name} is outside approved repository roots.");
        }
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RequirePathWithin(root, full, name);
        RejectReparsePointsBetween(root, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static byte[] ReadImmutable(
        string path,
        string expectedRef,
        string name)
    {
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        if (!string.Equals(ByteReference(bytes), expectedRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} failed content-address verification.");
        }
        return bytes;
    }

    private static byte[] ReadBoundedFile(
        string path,
        int maximumBytes,
        string name)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{name} is missing.", path);
        }
        var info = new FileInfo(path);
        if (info.Length < 1 || info.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"{name} must contain between one and {maximumBytes} bytes.");
        }
        return File.ReadAllBytes(path);
    }

    private static void PutImmutable(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {path}.");
            }
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new InvalidDataException("Paper repository root is required.");
        }
        string root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Paper repository root does not exist: {root}");
        }
        return root;
    }

    private static string PlanningAdmissionCursorPath(
        string root,
        string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-planning",
            "cursors",
            Hex(taskRef) + ".json");

    private static string PlanningDispatchPath(
        string root,
        string dispatchRef)
    {
        string hex = Hex(dispatchRef);
        return Path.Combine(
            root,
            "artifacts",
            "paper-frontier-planning",
            "dispatches",
            "raw",
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string AdmissionCursorPath(
        string root,
        string frontierRef,
        string nodeId) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-node-selections",
            "cursors",
            Hex(frontierRef),
            Hex(nodeId) + ".json");

    private static string CurrentStateCursorPath(
        string root,
        string frontierRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontiers",
            "current-state",
            Hex(frontierRef) + ".json");

    private static string FrontierLockPath(
        string root,
        string frontierRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontiers",
            "locks",
            Hex(frontierRef) + ".lock");

    private static string DomainArtifactPath(
        string root,
        string family,
        string blobRef)
    {
        RequireDigest(blobRef, nameof(blobRef));
        string hex = Hex(blobRef);
        return Path.Combine(
            root,
            "artifacts",
            "paper-frontier-node-selections",
            family,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string ResearchSelectionArtifactPath(
        string root,
        string authorizationRef,
        string fileName) =>
        Path.Combine(
            root,
            "artifacts",
            "research-selections",
            Hex(authorizationRef),
            fileName);

    private static string BindingLookupPath(
        string root,
        string requestRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-formalization-bindings",
            "by-request",
            Hex(requestRef) + ".json");

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Replace(Path.DirectorySeparatorChar, '/');

    private static void RequirePathWithin(
        string root,
        string path,
        string name)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} escapes its owned filesystem boundary.");
        }
    }

    private static void RejectReparsePointsBetween(
        string boundaryRoot,
        string path,
        string name)
    {
        string root = Path.GetFullPath(boundaryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(path);
        RequirePathWithin(root, full, name);
        string relative = Path.GetRelativePath(root, full);
        string current = root;
        if (Directory.Exists(current) || File.Exists(current))
        {
            RejectReparsePoint(current, name);
        }
        foreach (string segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                RejectReparsePoint(current, name);
            }
        }
    }

    private static void RejectReparsePoint(string path, string name)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"{name} cannot traverse a symbolic link.");
        }
    }

    private static string ByteReference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string ContentReference<T>(T content) =>
        ByteReference(CanonicalJson.Serialize(content));

    private static string TextReference(string text) =>
        ByteReference(System.Text.Encoding.UTF8.GetBytes(text));

    private static string Hex(string reference) =>
        reference["sha256:".Length..];
}
