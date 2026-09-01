namespace Trureturing.Paper.Core;

public static partial class PaperFrontierPlanningAgentService
{
    public static PaperFrontierPlanningContext ReopenContext(
        string repositoryRoot,
        PaperFrontierPlanningAgentDispatch dispatch)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        Validate(dispatch);
        return LoadContext(root, dispatch);
    }
}
