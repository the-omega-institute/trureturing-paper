using System.Globalization;
using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.ResearchSelection.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "select" => Select(args),
                "prepare-dispatch" => PrepareDispatch(args),
                "record-result" => RecordResult(args),
                "classify-result" => ClassifyResult(args),
                _ => throw new ArgumentException(Usage)
            };
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or InvalidDataException
            or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static int Select(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "select",
            "--content",
            "--research-input-root",
            "--selection-out",
            "--request-out");

        PaperResearchSelectionContent content =
            PaperResearchSelectionJson.ReadContent(
                File.ReadAllBytes(values["--content"]));
        var store = new PaperResearchInputStore(
            values["--research-input-root"]);
        PaperResearchInput researchInput =
            store.Get<PaperResearchInput>(content.PaperResearchInputRef);
        PaperResearchInputValidation.Validate(researchInput);

        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                researchInput);

        byte[] selectionBytes = PaperResearchSelectionJson.Write(selection);
        byte[] requestBytes =
            PaperResearchSelectionJson.Write(request);
        string selectionPath = WriteFile(
            values["--selection-out"],
            selectionBytes);
        string requestPath = WriteFile(
            values["--request-out"],
            requestBytes);

        WriteResult(new SelectionCliResult(
            "paper-formalization-handoff.v1",
            selection.SelectionId,
            request.RequestId,
            request.TruthRelease.ReleaseDigest,
            request.TruthRelease.SourceCommit,
            request.TruthRelease.SourceTree,
            selectionPath,
            requestPath));
        return 0;
    }

    private static int PrepareDispatch(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "prepare-dispatch",
            "--selection",
            "--request",
            "--root",
            "--selection-ref",
            "--request-ref",
            "--cursor");

        byte[] selectionBytes =
            File.ReadAllBytes(values["--selection"]);
        byte[] requestBytes =
            File.ReadAllBytes(values["--request"]);
        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(selectionBytes);
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(requestBytes);

        PaperFormalizationDispatchRegistration registration =
            PaperFormalizationTransportService.PrepareDispatch(
                values["--root"],
                selection,
                selectionBytes,
                request,
                requestBytes,
                values["--selection-ref"],
                values["--request-ref"],
                values["--cursor"]);

        WriteResult(new DispatchCliResult(
            "paper-formalization-dispatch-ready.v1",
            registration.DispatchRef,
            registration.FormalizationRequestRef,
            registration.SelectionRef,
            registration.SourceRepo,
            registration.SourceCommit,
            registration.SourceTree,
            registration.TruthReleaseDigest,
            registration.PaperId,
            registration.ResearchCandidateId,
            registration.Gid,
            Path.GetFullPath(values["--request"]),
            registration.CursorPath,
            registration.Replayed));
        return 0;
    }

    private static int RecordResult(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "record-result",
            "--root",
            "--dispatch-cursor",
            "--result-cursor",
            "--id",
            "--formalization-request-ref",
            "--observed-request-id",
            "--selection-ref",
            "--source-repo",
            "--source-commit",
            "--source-tree",
            "--truth-release-digest",
            "--paper-id",
            "--research-candidate-id",
            "--gid",
            "--status",
            "--rounds",
            "--verdict",
            "--error-class",
            "--dedup-key");

        if (!int.TryParse(
                values["--rounds"],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int rounds))
        {
            throw new InvalidDataException(
                "Formalize result rounds must be a positive integer.");
        }

        var incoming = new FormalizeSolveResultWire(
            values["--id"],
            values["--formalization-request-ref"],
            values["--observed-request-id"],
            values["--selection-ref"],
            values["--source-repo"],
            values["--source-commit"],
            values["--source-tree"],
            values["--truth-release-digest"],
            values["--paper-id"],
            values["--research-candidate-id"],
            values["--gid"],
            values["--status"],
            rounds,
            values["--verdict"],
            values["--error-class"],
            values["--dedup-key"]);

        PaperFormalizationResultRegistration registration =
            PaperFormalizationTransportService.RecordResult(
                values["--root"],
                values["--dispatch-cursor"],
                incoming,
                values["--result-cursor"]);

        WriteResult(new ResultCliResult(
            "paper-formalization-result-recorded.v1",
            registration.ResultRef,
            registration.DispatchRef,
            registration.FormalizationRequestRef,
            registration.SelectionRef,
            registration.Status,
            registration.BindingStatus,
            registration.CursorPath,
            registration.Replayed));
        return 0;
    }

    private static int ClassifyResult(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "classify-result",
            "--root",
            "--result-ref",
            "--cursor");

        PaperFormalizationOutcomeRegistration registration =
            PaperFormalizationOutcomeService.Classify(
                values["--root"],
                values["--result-ref"],
                values["--cursor"]);

        WriteResult(new OutcomeCliResult(
            "paper-formalization-outcome-classified.v1",
            registration.DecisionRef,
            registration.ResultRef,
            registration.DispatchRef,
            registration.FormalizationRequestRef,
            registration.SelectionRef,
            registration.PaperResearchInputRef,
            registration.IntuitionProposalRef,
            registration.CandidatePaperRef,
            registration.LiteratureResearchRef,
            registration.VerificationBudgetRef,
            registration.Route,
            registration.OutcomeClass,
            registration.ClaimStatus,
            registration.CertificationWaitRef,
            registration.CursorPath,
            registration.Replayed));
        return 0;
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        string verb,
        params string[] expectedOptions)
    {
        if (args.Length != 1 + (expectedOptions.Length * 2)
            || !string.Equals(args[0], verb, StringComparison.Ordinal))
        {
            throw new ArgumentException(Usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException(
                    $"Duplicate CLI option '{args[index]}'.");
            }
        }
        if (values.Count != expectedOptions.Length
            || expectedOptions.Any(option => !values.ContainsKey(option)))
        {
            throw new ArgumentException(
                "CLI options are incomplete or unknown.\n" + Usage);
        }
        return values;
    }

    private static string WriteFile(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary =
            fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        return fullPath;
    }

    private static void WriteResult<T>(T result)
    {
        byte[] bytes = CanonicalJson.Serialize(result);
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
    }

    private sealed record SelectionCliResult(
        string Schema,
        string SelectionRef,
        string FormalizationRequestRef,
        string TruthReleaseDigest,
        string SourceCommit,
        string SourceTree,
        string SelectionPath,
        string FormalizationRequestPath);

    private sealed record DispatchCliResult(
        string Schema,
        string DispatchRef,
        string FormalizationRequestRef,
        string SelectionRef,
        string SourceRepo,
        string SourceCommit,
        string SourceTree,
        string TruthReleaseDigest,
        string PaperId,
        string ResearchCandidateId,
        string Gid,
        string RequestPath,
        string CursorPath,
        bool Replayed);

    private sealed record ResultCliResult(
        string Schema,
        string ResultRef,
        string DispatchRef,
        string FormalizationRequestRef,
        string SelectionRef,
        string Status,
        string BindingStatus,
        string CursorPath,
        bool Replayed);

    private sealed record OutcomeCliResult(
        string Schema,
        string DecisionRef,
        string ResultRef,
        string DispatchRef,
        string FormalizationRequestRef,
        string SelectionRef,
        string PaperResearchInputRef,
        string IntuitionProposalRef,
        string CandidatePaperRef,
        string LiteratureResearchRef,
        string VerificationBudgetRef,
        string Route,
        string OutcomeClass,
        string ClaimStatus,
        string? CertificationWaitRef,
        string CursorPath,
        bool Replayed);

    private const string Usage = """
Usage:
  trureturing-paper-research-selection select --content <paper-selection-content.json> --research-input-root <content-addressed-paper-research-input-root> --selection-out <paper-research-selection.v1.json> --request-out <formalization-request.v1.json>
  trureturing-paper-research-selection prepare-dispatch --selection <paper-research-selection.v1.json> --request <formalization-request.v1.json> --root <content-addressed-paper-research-input-root> --selection-ref <sha256> --request-ref <sha256> --cursor <paper-formalization-dispatch-cursor.v1.json>
  trureturing-paper-research-selection record-result --root <content-addressed-paper-research-input-root> --dispatch-cursor <paper-formalization-dispatch-cursor.v1.json> --result-cursor <paper-formalization-result-cursor.v1.json> --id <sha256> --formalization-request-ref <sha256> --observed-request-id <sha256-or-empty> --selection-ref <sha256> --source-repo <text-or-empty> --source-commit <git-sha-or-empty> --source-tree <git-sha-or-empty> --truth-release-digest <sha256-or-empty> --paper-id <text-or-empty> --research-candidate-id <text-or-empty> --gid <gid-or-empty> --status <accepted|abstained> --rounds <positive-int> --verdict <text> --error-class <class-or-empty> --dedup-key <text>
  trureturing-paper-research-selection classify-result --root <content-addressed-paper-research-input-root> --result-ref <sha256> --cursor <paper-formalization-decision-cursor.v1.json>
""";
}
