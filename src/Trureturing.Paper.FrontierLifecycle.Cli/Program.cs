using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.FrontierLifecycle.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "record-transport" => RecordTransport(args),
                "record-outcome" => RecordOutcome(args),
                "record-certification" => RecordCertification(args),
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

    private static int RecordTransport(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "record-transport",
            "--repository-root",
            "--formalization-request-ref",
            "--dispatch-ref");
        PaperFrontierFormalizeTransportRecorded result =
            PaperFrontierNodeSelectionService.RecordFormalizeTransport(
                values["--repository-root"],
                values["--formalization-request-ref"],
                values["--dispatch-ref"]);
        WriteResult(result);
        return 0;
    }

    private static int RecordOutcome(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "record-outcome",
            "--repository-root",
            "--decision-ref");
        PaperFrontierFormalizationOutcomeRecorded result =
            PaperFrontierNodeSelectionService.RecordFormalizationOutcome(
                values["--repository-root"],
                values["--decision-ref"]);
        WriteResult(result);
        return 0;
    }

    private static int RecordCertification(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "record-certification",
            "--repository-root",
            "--evaluation-ref",
            "--certified-claim-ref");
        PaperFrontierCertificationRecorded result =
            PaperFrontierNodeSelectionService.RecordCertification(
                values["--repository-root"],
                values["--evaluation-ref"],
                values["--certified-claim-ref"]);
        WriteResult(result);
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

    private static void WriteResult<T>(T result)
    {
        byte[] bytes = CanonicalJson.Serialize(result);
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
    }

    private const string Usage = """
Usage:
  trureturing-paper-frontier-lifecycle record-transport --repository-root <path> --formalization-request-ref <sha256:...> --dispatch-ref <sha256:...>
  trureturing-paper-frontier-lifecycle record-outcome --repository-root <path> --decision-ref <sha256:...>
  trureturing-paper-frontier-lifecycle record-certification --repository-root <path> --evaluation-ref <sha256:...> --certified-claim-ref <sha256:...>
""";
}
