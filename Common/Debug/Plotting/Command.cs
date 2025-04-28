namespace Tyr.Common.Debug.Plotting;

public readonly record struct Command(
    string Id,
    object Value,
    Meta Meta
);