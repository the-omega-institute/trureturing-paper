from pathlib import Path

path = Path("src/Trureturing.Paper.Core/PaperFrontierCompletion.cs")
text = path.read_text()

start = text.index("        if (File.Exists(terminalCursorPath))")
end = text.index("        HashSet<string> requiredClaimIds", start)
text = text[:start] + """        if (File.Exists(terminalCursorPath))
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

""" + text[end:]

pending_start = text.index(
    "    private static PaperFrontierCompletionEvaluated PendingResult(")
pending_end = text.index(
    "    private static PaperFrontierCompletionEvaluated CompletedResult(",
    pending_start)
text = text[:pending_start] + """    private static PaperFrontierCompletionEvaluated PendingResult(
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

""" + text[pending_end:]

completed_start = text.index(
    "    private static PaperFrontierCompletionEvaluated CompletedResult(")
completed_end = text.index(
    "    private static void ValidateCompletionReplay(",
    completed_start)
text = text[:completed_start] + """    private static PaperFrontierCompletionEvaluated CompletedResult(
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

""" + text[completed_end:]

text = text.replace("RequireText(", "RequireCompletionText(")
helper_anchor = "    private static void RequireCompletionDigestList(\n"
helper = """    private static void RequireCompletionText(
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

"""
if text.count(helper_anchor) != 1:
    raise SystemExit("completion validation helper anchor not found")
text = text.replace(helper_anchor, helper + helper_anchor, 1)

text = text.replace(
    """        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        int formalCount = 0;""",
    """        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.Ordinal);
        int formalCount = 0;""",
    1)
text = text.replace(
    """                || !nodeIds.Add(claim.NodeId)
                || !claimIds.Add(claim.ClaimId))""",
    """                || !nodeIds.Add(claim.NodeId)
                || !claimIds.Add(claim.ClaimId)
                || !labels.Add(claim.LatexLabel))""",
    1)
text = text.replace(
    """                RequireCompletionText(
                    claim.ManuscriptClaimKind,
                    nameof(claim.ManuscriptClaimKind),
                    128);
                formalCount++;""",
    """                RequireCompletionText(
                    claim.ManuscriptClaimKind,
                    nameof(claim.ManuscriptClaimKind),
                    128);
                if (!ManuscriptFormalKinds.Contains(
                        claim.ManuscriptClaimKind))
                {
                    throw new InvalidDataException(
                        "Frontier completion formal claim kind is unsupported.");
                }
                formalCount++;""",
    1)
text = text.replace(
    """                if (!string.IsNullOrEmpty(claim.ManuscriptClaimKind))
                {
                    throw new InvalidDataException(
                        "Informal completion claims cannot carry a manuscript claim kind.");
                }
                informalCount++;""",
    """                if (!string.IsNullOrEmpty(claim.ManuscriptClaimKind)
                    || claim.ManuscriptDisposition is not (
                        "informal-definition"
                        or "informal-example"
                        or "informal-remark"))
                {
                    throw new InvalidDataException(
                        "Informal completion claims have an invalid disposition or claim kind.");
                }
                informalCount++;""",
    1)

path.write_text(text)
Path("tools/frontier_completion_patch_fix.py").unlink()
