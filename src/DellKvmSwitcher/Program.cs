Logging.Init();
await CommandRunner.RunCommand(Inner, args);

static Task Inner(string directory, string? package, bool build)
{
    Log.Information("TargetDirectory: {TargetDirectory}", directory);

    return Task.CompletedTask;
}
