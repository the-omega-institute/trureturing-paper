using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.FrontierSelection.Cli;

internal static class Program
{
    private const string Usage = """
        Usage:
          admit-frontier-node-selection --repository-root <path> --frontier-task-ref <sha256:...> --node-id <sha256:...>
          admit-frontier-ready-wave --repository-root <path> --frontier-ref <sha256:...> --ready-set-ref <sha256:...>
          evaluate-frontier-completion --repository-root <path> --frontier-ref <sha256:...>
        """;

    public static int Main(string[] args)
    {
        try
        {
            object result = args.FirstOrDefault() switch
            {
                "admit-frontier-node-selection" when args.Length == 7
                    => AdmitNode(args),
                "admit-frontier-ready-wave" when args.Length == 7
                    => AdmitReadyWave(args),
                "evaluate-frontier-completion" when args.Length == 5
                    => EvaluateCompletion(args),
                _ => throw new ArgumentException(Usage)
            };
            Console.WriteLine(
                Encoding.UTF8.GetString(CanonicalJson.Serialize(result)));
            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static PaperFrontierNodeSelectionAdmitted AdmitNode(
        string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "--repository-root",
            "--frontier-task-ref",
            "--node-id");
        return PaperFrontierNodeSelectionService.Admit(
            values["--repository-root"],
            values["--frontier-task-ref"],
            values["--node-id"]);
    }

    private static PaperFrontierReadyWaveSelectionAdmitted AdmitReadyWave(
        string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "--repository-root",
            "--frontier-ref",
            "--ready-set-ref");
        return PaperFrontierNodeSelectionService.AdmitReadyWave(
            values["--repository-root"],
            values["--frontier-ref"],
            values["--ready-set-ref"]);
    }

    private static PaperFrontierCompletionEvaluated EvaluateCompletion(
        string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "--repository-root",
            "--frontier-ref");
        return PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
            values["--repository-root"],
            values["--frontier-ref"]);
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        params string[] expectedOptions)
    {
        var expected = expectedOptions.ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            if (!expected.Contains(args[index])
                || !values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException(Usage);
            }
        }
        if (values.Count != expected.Count)
        {
            throw new ArgumentException(Usage);
        }
        return values;
    }
}
