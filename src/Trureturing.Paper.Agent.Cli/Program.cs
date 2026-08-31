using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Agent.Cli;

internal static class Program
{
    private const string Usage = """
        Usage:
          register-task --repository-root <path> --task <path>
          prepare-run --repository-root <path> --task-ref <sha256:...>
          record-result --repository-root <path> --task-ref <sha256:...> --stdout <path> --run-id <id-or-empty> --provenance <produced|adopted>
          stage-foundation-task --repository-root <path> --dispatch <path>
          admit-foundation-result --repository-root <path> --task-ref <sha256:...>
          stage-deepening-task --repository-root <path> --dispatch <path>
          admit-deepening-result --repository-root <path> --task-ref <sha256:...>
        """;

    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "register-task" => RegisterTask(args),
                "prepare-run" => PrepareRun(args),
                "record-result" => RecordResult(args),
                "stage-foundation-task" => StageFoundationTask(args),
                "admit-foundation-result" => AdmitFoundationResult(args),
                "stage-deepening-task" => StageDeepeningTask(args),
                "admit-deepening-result" => AdmitDeepeningResult(args),
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

    private static int RegisterTask(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "register-task",
            "--repository-root",
            "--task");
        PaperAgentTaskRegistration result =
            PaperAgentRuntimeService.RegisterTask(
                values["--repository-root"],
                values["--task"]);
        WriteResult(result);
        return 0;
    }

    private static int PrepareRun(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "prepare-run",
            "--repository-root",
            "--task-ref");
        PaperAgentRunPrepared result =
            PaperAgentRuntimeService.PrepareRun(
                values["--repository-root"],
                values["--task-ref"]);
        WriteResult(result);
        return 0;
    }

    private static int RecordResult(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "record-result",
            "--repository-root",
            "--task-ref",
            "--stdout",
            "--run-id",
            "--provenance");
        PaperAgentResultRecorded result =
            PaperAgentRuntimeService.RecordResult(
                values["--repository-root"],
                values["--task-ref"],
                values["--stdout"],
                values["--run-id"],
                values["--provenance"]);
        WriteResult(result);
        return 0;
    }

    private static int StageFoundationTask(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "stage-foundation-task",
            "--repository-root",
            "--dispatch");
        PaperTheoryFoundationAgentTaskStaged result =
            PaperTheoryFoundationAgentService.StageTask(
                values["--repository-root"],
                values["--dispatch"]);
        WriteResult(result);
        return 0;
    }

    private static int AdmitFoundationResult(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "admit-foundation-result",
            "--repository-root",
            "--task-ref");
        PaperTheoryFoundationAgentResultAdmitted result =
            PaperTheoryFoundationAgentService.AdmitResult(
                values["--repository-root"],
                values["--task-ref"]);
        WriteResult(result);
        return 0;
    }

    private static int StageDeepeningTask(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "stage-deepening-task",
            "--repository-root",
            "--dispatch");
        PaperTheoryDeepeningAgentTaskStaged result =
            PaperTheoryDeepeningAgentService.StageTask(
                values["--repository-root"],
                values["--dispatch"]);
        WriteResult(result);
        return 0;
    }

    private static int AdmitDeepeningResult(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "admit-deepening-result",
            "--repository-root",
            "--task-ref");
        PaperTheoryDeepeningAgentResultAdmitted result =
            PaperTheoryDeepeningAgentService.AdmitResult(
                values["--repository-root"],
                values["--task-ref"]);
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

    private static void WriteResult<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }));
    }
}
