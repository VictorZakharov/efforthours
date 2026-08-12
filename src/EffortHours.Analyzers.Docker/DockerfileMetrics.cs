namespace EffortHours.Analyzers.Docker;

internal sealed class DockerfileMetrics
{
    public int Instructions;
    public int Stages;
    public int ExternalBaseImages;
    public int LocalStageReferences;
    public int BuildSteps;
    public int RunInstructions;
    public int CopyInstructions;
    public int AddInstructions;
    public int Arguments;
    public int EnvironmentEntries;
    public int WorkDirectories;
    public int Users;
    public int ExposedPorts;
    public int Volumes;
    public int HealthChecks;
    public int RuntimeCommands;
    public int MultiStageCopies;
    public int SecretOrSshMounts;
    public int CacheOrBindMounts;
    public int RemoteAdds;
    public int Heredocs;
    public int ParserDirectives;
    public int UnresolvedValues;
    public int UnknownInstructions;
    public int Safeguards;

    public DockerfileAnalysis ToAnalysis()
    {
        decimal units = Instructions == 0 ? 0.25m : 1m;
        units += 0.5m * Math.Min(6, Math.Max(0, Stages - 1));
        units += 0.25m * decimal.Ceiling(BuildSteps / 4m);
        if (Arguments + EnvironmentEntries + WorkDirectories + ExposedPorts + Volumes + RuntimeCommands > 0)
            units += 0.25m;
        if (HealthChecks > 0) units += 0.25m;
        if (Users + SecretOrSshMounts > 0) units += 0.25m;
        if (UnresolvedValues + UnknownInstructions + Heredocs + Safeguards > 0) units += 0.25m;
        string confidence = Safeguards > 0 ? "low" :
            UnresolvedValues + UnknownInstructions + Heredocs > 0 ? "medium" : "high";
        return new DockerfileAnalysis
        {
            Instructions = Instructions,
            Stages = Stages,
            ExternalBaseImages = ExternalBaseImages,
            LocalStageReferences = LocalStageReferences,
            BuildSteps = BuildSteps,
            RunInstructions = RunInstructions,
            CopyInstructions = CopyInstructions,
            AddInstructions = AddInstructions,
            Arguments = Arguments,
            EnvironmentEntries = EnvironmentEntries,
            WorkDirectories = WorkDirectories,
            Users = Users,
            ExposedPorts = ExposedPorts,
            Volumes = Volumes,
            HealthChecks = HealthChecks,
            RuntimeCommands = RuntimeCommands,
            MultiStageCopies = MultiStageCopies,
            SecretOrSshMounts = SecretOrSshMounts,
            CacheOrBindMounts = CacheOrBindMounts,
            RemoteAdds = RemoteAdds,
            Heredocs = Heredocs,
            ParserDirectives = ParserDirectives,
            UnresolvedValues = UnresolvedValues,
            UnknownInstructions = UnknownInstructions,
            Safeguards = Safeguards,
            Confidence = confidence,
            SemanticUnits = decimal.Min(12m, units),
        };
    }
}
