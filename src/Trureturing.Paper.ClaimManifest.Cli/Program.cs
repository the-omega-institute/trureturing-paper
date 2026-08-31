using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.ClaimManifest.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "register-plan" => RegisterPlan(args),
                "list-plans" => ListPlans(args),
                "evaluate-plan" => EvaluatePlan(args),
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

    private static int RegisterPlan(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "register-plan",
            "--root",
            "--plan",
            "--cursor");

        PaperManuscriptPlanRegistration registration =
            PaperCertifiedClaimManifestService.RegisterPlan(
                values["--root"],
                File.ReadAllBytes(values["--plan"]),
                values["--cursor"]);

        WriteResult(new PlanRegistrationCliResult(
            "paper-manuscript-plan-registered.v1",
            registration.ManuscriptPlanRef,
            registration.PaperId,
            registration.ManuscriptTruthReleaseRef,
            registration.CursorPath,
            registration.Replayed));
        return 0;
    }

    private static int ListPlans(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "list-plans",
            "--cursor-directory");

        IReadOnlyList<string> planRefs =
            PaperCertifiedClaimManifestService.ListPlanRefs(
                values["--cursor-directory"]);
        WriteResult(new PlanListCliResult(
            "paper-manuscript-plans-listed.v1",
            planRefs));
        return 0;
    }

    private static int EvaluatePlan(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "evaluate-plan",
            "--root",
            "--plan-ref",
            "--evaluation-directory",
            "--resolution-cursor");

        PaperManuscriptClaimEvaluationRegistration registration =
            PaperCertifiedClaimManifestService.Evaluate(
                values["--root"],
                values["--plan-ref"],
                values["--evaluation-directory"],
                values["--resolution-cursor"]);

        WriteResult(new PlanEvaluationCliResult(
            "paper-manuscript-claim-evaluated.v1",
            registration.EvaluationRef,
            registration.ManuscriptPlanRef,
            registration.EvidenceStateRef,
            registration.Outcome,
            registration.Reason,
            registration.ClaimManifestRef,
            registration.EligibilityRef,
            registration.PendingRef,
            registration.IneligibilityRef,
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
            || !string.Equals(
                args[0],
                verb,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(Usage);
        }

        var values = new Dictionary<string, string>(
            StringComparer.Ordinal);
        for (int index = 1;
             index < args.Length;
             index += 2)
        {
            if (!values.TryAdd(
                    args[index],
                    args[index + 1]))
            {
                throw new ArgumentException(
                    $"Duplicate CLI option '{args[index]}'.");
            }
        }

        if (values.Count != expectedOptions.Length
            || expectedOptions.Any(option =>
                !values.ContainsKey(option)))
        {
            throw new ArgumentException(
                "CLI options are incomplete or unknown.\n"
                + Usage);
        }
        return values;
    }

    private static void WriteResult<T>(T result)
    {
        byte[] bytes = CanonicalJson.Serialize(result);
        Console.WriteLine(
            System.Text.Encoding.UTF8.GetString(bytes));
    }

    private sealed record PlanRegistrationCliResult(
        string Schema,
        string ManuscriptPlanRef,
        string PaperId,
        string ManuscriptTruthReleaseRef,
        string CursorPath,
        bool Replayed);

    private sealed record PlanListCliResult(
        string Schema,
        IReadOnlyList<string> ManuscriptPlanRefs);

    private sealed record PlanEvaluationCliResult(
        string Schema,
        string EvaluationRef,
        string ManuscriptPlanRef,
        string EvidenceStateRef,
        string Outcome,
        string Reason,
        string? ClaimManifestRef,
        string? EligibilityRef,
        string? PendingRef,
        string? IneligibilityRef,
        string CursorPath,
        bool Replayed);

    private const string Usage = """
Usage:
  trureturing-paper-claim-manifest register-plan --root <content-addressed-paper-root> --plan <paper-manuscript-plan.v1.json> --cursor <paper-manuscript-plan-cursor.v1.json>
  trureturing-paper-claim-manifest list-plans --cursor-directory <directory>
  trureturing-paper-claim-manifest evaluate-plan --root <content-addressed-paper-root> --plan-ref <sha256> --evaluation-directory <directory> --resolution-cursor <paper-manuscript-claim-resolution-cursor.v1.json>
""";
}
