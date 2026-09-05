using System.Text.Json.Serialization;

namespace Tyr.Control.GameController;

// ── Outbound (client → GC) ────────────────────────────────────────────────────

public sealed class GcInput
{
    [JsonPropertyName("change")]        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GcChange? Change { get; set; }

    [JsonPropertyName("continueAction")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GcContinueActionRef? ContinueAction { get; set; }

    [JsonPropertyName("resetMatch")]    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ResetMatch { get; set; }

    // ── factories ────────────────────────────────────────────────────────────

    public static GcInput Command(string type, string forTeam = "UNKNOWN") => new()
    {
        Change = new GcChange
        {
            NewCommandChange = new GcNewCommand
            {
                Command = new GcCommandRef { Type = type, ForTeam = forTeam }
            }
        }
    };

    public static GcInput ChangeStage(string newStage) => new()
    {
        Change = new GcChange { ChangeStageChange = new GcChangeStage { NewStage = newStage } }
    };

    public static GcInput Continue(string type, string forTeam = "UNKNOWN") => new()
    {
        ContinueAction = new GcContinueActionRef { Type = type, ForTeam = forTeam }
    };

    public static GcInput Reset() => new() { ResetMatch = true };
}

public sealed class GcChange
{
    [JsonPropertyName("origin")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Origin { get; set; } = "TyrSharp2";

    [JsonPropertyName("newCommandChange")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GcNewCommand? NewCommandChange { get; set; }

    [JsonPropertyName("changeStageChange")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GcChangeStage? ChangeStageChange { get; set; }
}

public sealed class GcNewCommand
{
    [JsonPropertyName("command")]
    public GcCommandRef? Command { get; set; }
}

public sealed class GcChangeStage
{
    [JsonPropertyName("newStage")]
    public string? NewStage { get; set; }
}

public sealed class GcCommandRef
{
    [JsonPropertyName("type")]    public string Type    { get; set; } = "UNKNOWN";
    [JsonPropertyName("forTeam")] public string ForTeam { get; set; } = "UNKNOWN";
}

public sealed class GcContinueActionRef
{
    [JsonPropertyName("type")]    public string Type    { get; set; } = "TYPE_UNKNOWN";
    [JsonPropertyName("forTeam")] public string ForTeam { get; set; } = "UNKNOWN";
}

// ── Inbound (GC → client) ─────────────────────────────────────────────────────

public sealed class GcOutput
{
    [JsonPropertyName("matchState")] public GcMatchState? MatchState { get; set; }
    [JsonPropertyName("gcState")]    public GcStateData?  GcState    { get; set; }
}

public sealed class GcMatchState
{
    [JsonPropertyName("stage")]                    public string? Stage                   { get; set; }
    [JsonPropertyName("command")]                  public GcCommandRef? Command           { get; set; }
    [JsonPropertyName("nextCommand")]              public GcCommandRef? NextCommand       { get; set; }
    [JsonPropertyName("gameState")]                public GcGameStateData? GameState      { get; set; }
    [JsonPropertyName("stageTimeLeft")]            public string? StageTimeLeft           { get; set; }
    [JsonPropertyName("stageTimeElapsed")]         public string? StageTimeElapsed        { get; set; }
    [JsonPropertyName("currentActionTimeRemaining")] public string? ActionTimeRemaining   { get; set; }
    [JsonPropertyName("teamState")]                public Dictionary<string, GcTeamInfo>? TeamState { get; set; }
    [JsonPropertyName("statusMessage")]            public string? StatusMessage           { get; set; }
}

public sealed class GcGameStateData
{
    [JsonPropertyName("type")]    public string? Type    { get; set; }
    [JsonPropertyName("forTeam")] public string? ForTeam { get; set; }
}

public sealed class GcTeamInfo
{
    [JsonPropertyName("name")]          public string? Name         { get; set; }
    [JsonPropertyName("goals")]         public int     Goals        { get; set; }
    [JsonPropertyName("goalkeeper")]    public int     Goalkeeper   { get; set; }
    [JsonPropertyName("timeoutsLeft")]  public int     TimeoutsLeft { get; set; }
    [JsonPropertyName("yellowCards")]   public GcYellowCard[]? YellowCards { get; set; }
    [JsonPropertyName("redCards")]      public object[]?       RedCards    { get; set; }
    [JsonPropertyName("onPositiveHalf")] public bool   OnPositiveHalf { get; set; }
}

public sealed class GcYellowCard
{
    [JsonPropertyName("timeRemaining")] public string? TimeRemaining { get; set; }
}

public sealed class GcStateData
{
    [JsonPropertyName("continueActions")] public GcContinueAction[]? ContinueActions { get; set; }
    [JsonPropertyName("continueHints")]   public GcContinueHint[]?   ContinueHints   { get; set; }
}

public sealed class GcContinueAction
{
    [JsonPropertyName("type")]               public string?   Type               { get; set; }
    [JsonPropertyName("forTeam")]            public string?   ForTeam            { get; set; }
    [JsonPropertyName("state")]              public string?   State              { get; set; }
    [JsonPropertyName("continuationIssues")] public string[]? ContinuationIssues { get; set; }
}

public sealed class GcContinueHint
{
    [JsonPropertyName("message")] public string? Message { get; set; }
}
