namespace PCL.Core.Minecraft.Launch.State;

public enum LaunchState {
    Idle,
    Validating,
    Authenticating,
    BuildingArguments,
    PreLaunching,
    Launching,
    WaitingForWindow,
    Finished,
    Failed
}

