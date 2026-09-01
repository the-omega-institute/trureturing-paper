from pathlib import Path


def replace_exact(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text()
    if text.count(old) != 1:
        raise SystemExit(f"expected one target in {path}: {old[:80]!r}")
    file.write_text(text.replace(old, new, 1))


service_path = Path("src/Trureturing.Paper.Core/PaperFrontierCompletion.cs")
service = service_path.read_text()

anchor = """    public static PaperFrontierCompletionEvaluated EvaluateFrontierCompletion(
"""
listing = """    public static IReadOnlyList<string> ListFrontierCompletionCandidates(
        string repositoryRoot)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string directory = Path.Combine(
            root,
            \"work\",
            \"paper-frontier-formalization-progress\",
            \"certifications\");
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
                    \"Frontier completion certification directory has a noncanonical identity.\");
            }
            string frontierRef = \"sha256:\" + hex;
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
                        \"Frontier completion candidate directory contains a cross-frontier cursor.\");
                }
            }
            frontiers.Add(frontierRef);
        }
        return frontiers;
    }

"""
if service.count(anchor) != 1:
    raise SystemExit("EvaluateFrontierCompletion anchor not found")
service = service.replace(anchor, listing + anchor, 1)

service = service.replace(
    """        PaperFrontierCoherentRelease? selected = SelectCoherentRelease(
            materials);""",
    """        PaperFrontierCoherentRelease? selected = SelectCoherentRelease(
            root,
            materials);""",
    1)

service = service.replace(
    """                current.StateId,
                missingNodeIds,""",
    """                current.StateId,
                context.Source.Program.ProgramContent.PaperId,
                missingNodeIds,""",
    1)
service = service.replace(
    """                current.StateId,
                [],
                blocking,""",
    """                current.StateId,
                context.Source.Program.ProgramContent.PaperId,
                [],
                blocking,""",
    1)
service = service.replace(
    """            frontierRef,
            current.StateId,
            completionRef,""",
    """            frontierRef,
            current.StateId,
            context.Source.Program.ProgramContent.PaperId,
            completionRef,""",
    1)

old_method_start = service.index(
    "    private static PaperFrontierCoherentRelease? SelectCoherentRelease(")
old_method_end = service.index(
    "    private static bool ReleaseCoversAll(",
    old_method_start)
new_release_methods = """    private static PaperFrontierCoherentRelease? SelectCoherentRelease(
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
            \"work\",
            \"research-input\",
            \"certification-releases\");
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.EnumerateFiles(
                directory,
                \"*.json\",
                SearchOption.TopDirectoryOnly)
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                PaperCertificationReleaseCursor cursor =
                    PaperResearchInputJson.DeserializeStrict<
                        PaperCertificationReleaseCursor>(
                            ReadBoundedFile(
                                path,
                                MaximumControlBytes,
                                \"Certification release cursor\"));
                if (!string.Equals(
                        cursor.Schema,
                        PaperCertificationSchemas.ReleaseCursor,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        \"Frontier completion encountered an invalid certification release cursor schema.\");
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
                        \"Certification release cursor changed the observed release digest.\");
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

"""
service = service[:old_method_start] + new_release_methods + service[old_method_end:]

service = service.replace(
    """                    material.CertifiedClaim.CertifyingReleaseRef == string.Empty
                        ? throw new InvalidDataException(
                            \"Completed formal claim lacks certification evidence.\")
                        : material.FrontierManifest.ManifestContent.CertifiedClaimRef,""",
    """                    material.FrontierManifest.ManifestContent.CertifiedClaimRef,""",
    1)
service = service.replace(
    """                material.PackageClaim.Statement,
                PaperCertifiedClaimManifestService.ExplicitlyInformal));""",
    """                material.Node.FormalStatement,
                PaperCertifiedClaimManifestService.ExplicitlyInformal));""",
    1)

service = service.replace(
    """    private static PaperFrontierCompletionEvaluated PendingResult(
        string root,
        string frontierRef,
        string stateRef,
        IReadOnlyList<string> missingNodeIds,""",
    """    private static PaperFrontierCompletionEvaluated PendingResult(
        string root,
        string frontierRef,
        string stateRef,
        string paperId,
        IReadOnlyList<string> missingNodeIds,""",
    1)
service = service.replace(
    """            frontierRef,
            stateRef,
            missingNodeIds.OrderBy""",
    """            frontierRef,
            stateRef,
            paperId,
            missingNodeIds.OrderBy""",
    1)
service = service.replace(
    """            frontierRef,
            stateRef,
            string.Empty,
            pendingRef,""",
    """            frontierRef,
            stateRef,
            paperId,
            string.Empty,
            pendingRef,""",
    1)
service = service.replace(
    """            cursor.FrontierRef,
            cursor.FrontierStateRef,
            cursor.CompletionRef,""",
    """            cursor.FrontierRef,
            cursor.FrontierStateRef,
            cursor.PaperId,
            cursor.CompletionRef,""",
    1)

service = service.replace(
    """        RequireDigest(
            pending.FrontierStateRef,
            nameof(pending.FrontierStateRef));""",
    """        RequireDigest(
            pending.FrontierStateRef,
            nameof(pending.FrontierStateRef));
        RequirePaperId(pending.PaperId);""",
    1)
service = service.replace(
    """        foreach (string digest in new[]
        {
            cursor.FrontierRef,""",
    """        RequirePaperId(cursor.PaperId);
        foreach (string digest in new[]
        {
            cursor.FrontierRef,""",
    1)
service = service.replace(
    """        if (!string.Equals(cursor.FrontierRef, context.Source.Frontier.FrontierId, StringComparison.Ordinal))""",
    """        if (!string.Equals(cursor.FrontierRef, context.Source.Frontier.FrontierId, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, context.Source.Program.ProgramContent.PaperId, StringComparison.Ordinal))""",
    1)

old_digest_method = """    private static void RequireCompletionDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($\"{name} is incomplete.\");
        }
        string[] normalized = values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string value in values)
        {
            RequireDigest(value, name);
        }
        if (!values.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $\"{name} must be sorted and unique.\");
        }
    }
"""
new_digest_method = """    private static void RequireCompletionDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($\"{name} is incomplete.\");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException(
                    $\"{name} must contain unique references.\");
            }
        }
    }
"""
if service.count(old_digest_method) != 1:
    raise SystemExit("completion digest validator target not found")
service = service.replace(old_digest_method, new_digest_method, 1)

service_path.write_text(service)

models = Path("src/Trureturing.Paper.Core/PaperFrontierCompletionModels.cs")
text = models.read_text()
text = text.replace(
    '    public const string Ready = "paper-frontier-completion-ready.v1";\n',
    '    public const string Ready = "paper-frontier-completion-ready.v1";\n'
    '    public const string CandidatesListed =\n'
    '        "paper-frontier-completion-candidates-listed.v1";\n',
    1)
text += """

public sealed record PaperFrontierCompletionCandidatesListed(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] IReadOnlyList<string> FrontierRefs);
"""
models.write_text(text)

manifest = Path("src/Trureturing.Paper.Core/CertifiedClaimManifest.cs")
text = manifest.read_text()
text = text.replace(
    '        "theorem",\n        "lemma",\n        "corollary"',
    '        "theorem",\n        "lemma",\n        "proposition",\n        "corollary"',
    1)
text = text.replace(
    '"Formal claim kind must be theorem, lemma, or corollary."',
    '"Formal claim kind must be theorem, lemma, proposition, or corollary."',
    1)
text = text.replace(
    '            "lemma" => "lem:",\n            "corollary" => "cor:",',
    '            "lemma" => "lem:",\n            "proposition" => "prop:",\n            "corollary" => "cor:",',
    1)
manifest.write_text(text)

for schema_name in [
    "contracts/paper-manuscript-plan.v1.schema.json",
    "contracts/paper-certified-claim-manifest.v1.schema.json",
]:
    path = Path(schema_name)
    text = path.read_text()
    old = '\"enum\":[\"theorem\",\"lemma\",\"corollary\"]'
    new = '\"enum\":[\"theorem\",\"lemma\",\"proposition\",\"corollary\"]'
    if text.count(old) != 1:
        raise SystemExit(f"formal claim enum target not found in {schema_name}")
    path.write_text(text.replace(old, new, 1))

cli = Path("src/Trureturing.Paper.FrontierSelection.Cli/Program.cs")
text = cli.read_text()
text = text.replace(
    '          evaluate-frontier-completion --repository-root <path> --frontier-ref <sha256:...>\n',
    '          evaluate-frontier-completion --repository-root <path> --frontier-ref <sha256:...>\n'
    '          list-frontier-completion-candidates --repository-root <path>\n',
    1)
text = text.replace(
    '                "evaluate-frontier-completion" when args.Length == 5\n'
    '                    => EvaluateCompletion(args),',
    '                "evaluate-frontier-completion" when args.Length == 5\n'
    '                    => EvaluateCompletion(args),\n'
    '                "list-frontier-completion-candidates" when args.Length == 3\n'
    '                    => ListCompletionCandidates(args),',
    1)
method_anchor = """    private static Dictionary<string, string> ParseValues(
"""
method = """    private static PaperFrontierCompletionCandidatesListed
        ListCompletionCandidates(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            \"--repository-root\");
        IReadOnlyList<string> frontiers =
            PaperFrontierNodeSelectionService.ListFrontierCompletionCandidates(
                values[\"--repository-root\"]);
        return new(
            PaperFrontierCompletionSchemas.CandidatesListed,
            frontiers);
    }

"""
if text.count(method_anchor) != 1:
    raise SystemExit("CLI ParseValues anchor not found")
cli.write_text(text.replace(method_anchor, method + method_anchor, 1))

Path("tools/frontier_completion_patch.py").unlink()
Path(".github/workflows/frontier-completion-patch.yml").unlink()
