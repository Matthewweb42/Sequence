param(
    [switch]$IncludeGodotRuntime
)

if ($IncludeGodotRuntime) {
    dotnet test Sequence.slnx -v minimal
    exit $LASTEXITCODE
}

dotnet test Sequence.slnx -v minimal --filter "TestCategory!=GodotRuntime"
exit $LASTEXITCODE
