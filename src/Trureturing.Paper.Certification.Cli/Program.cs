using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Certification.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "register-wait" => RegisterWait(args),
                "observe-release" => ObserveRelease(args),
                "evaluate-release" => EvaluateRelease(args),
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

    private static int RegisterWait(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "register-wait",
            "--root",
            "--wait-ref",
            "--cursor",
            "--release-cursor-directory");

        PaperCertificationWaitRegistration registration =
            PaperCertificationService.RegisterWait(
                values["--root"],
                values["--wait-ref"],
                values["--cursor"],
                values["--release-cursor-directory"]);

        WriteResult(new WaitRegistrationCliResult(
            "paper-certification-wait-registered.v1",
            registration.CertificationWaitRef,
            registration.CursorPath,
            registration.ReleaseRefs,
            registration.Replayed));
        return 0;
    }

    private static int ObserveRelease(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "observe-release",
            "--root",
            "--release",
            "--cursor",
            "--wait-cursor-directory");

        PaperCertificationReleaseRegistration registration =
            PaperCertificationService.RegisterRelease(
                values["--root"],
                File.ReadAllBytes(values["--release"]),
                values["--cursor"],
                values["--wait-cursor-directory"]);

        WriteResult(new ReleaseRegistrationCliResult(
            "paper-certification-release-registered.v1",
            registration.ReleaseRef,
            registration.ReleaseDigest,
            registration.CursorPath,
            registration.CertificationWaitRefs,
            registration.Replayed));
        return 0;
    }

    private static int EvaluateRelease(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "evaluate-release",
            "--root",
            "--wait-ref",
            "--release-ref",
            "--cursor",
            "--resolution-cursor");

        PaperCertificationEvaluationRegistration registration =
            PaperCertificationService.Evaluate(
                values["--root"],
                values["--wait-ref"],
                values["--release-ref"],
                values["--cursor"],
                values["--resolution-cursor"]);

        WriteResult(new EvaluationCliResult(
            "paper-certification-release-evaluated.v1",
            registration.EvaluationRef,
            registration.CertificationWaitRef,
            registration.ReleaseRef,
            registration.Outcome,
            registration.Reason,
            registration.ClaimStatus,
            registration.CertifiedClaimRef,
            registration.MismatchRef,
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

        var values = new Dictionary<string, string>(
            StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
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
                "CLI options are incomplete or unknown.\n" + Usage);
        }
        return values;
    }

    private static void WriteResult<T>(T result)
    {
        byte[] bytes = CanonicalJson.Serialize(result);
        Console.WriteLine(
            System.Text.Encoding.UTF8.GetString(bytes));
    }

    private sealed record WaitRegistrationCliResult(
        string Schema,
        string CertificationWaitRef,
        string CursorPath,
        IReadOnlyList<string> ReleaseRefs,
        bool Replayed);

    private sealed record ReleaseRegistrationCliResult(
        string Schema,
        string ReleaseRef,
        string ReleaseDigest,
        string CursorPath,
        IReadOnlyList<string> CertificationWaitRefs,
        bool Replayed);

    private sealed record EvaluationCliResult(
        string Schema,
        string EvaluationRef,
        string CertificationWaitRef,
        string ReleaseRef,
        string Outcome,
        string Reason,
        string ClaimStatus,
        string? CertifiedClaimRef,
        string? MismatchRef,
        string CursorPath,
        bool Replayed);

    private const string Usage = """
Usage:
  trureturing-paper-certification register-wait --root <content-addressed-paper-root> --wait-ref <sha256> --cursor <paper-certification-wait-cursor.v1.json> --release-cursor-directory <directory>
  trureturing-paper-certification observe-release --root <content-addressed-paper-root> --release <paper-certification-release.v1.json> --cursor <paper-certification-release-cursor.v1.json> --wait-cursor-directory <directory>
  trureturing-paper-certification evaluate-release --root <content-addressed-paper-root> --wait-ref <sha256> --release-ref <sha256> --cursor <paper-certification-evaluation-cursor.v1.json> --resolution-cursor <paper-certification-resolution-cursor.v1.json>
""";
}
