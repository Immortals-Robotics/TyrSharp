namespace Tyr.Common.Sender.Data;

public record CommandsWrapper
{
    public Timestamp Time { get; init; }
    public List<Command> Commands { get; init; } = [];
}