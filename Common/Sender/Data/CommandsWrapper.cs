using Tyr.Common.Data;

namespace Tyr.Common.Sender.Data;

public record CommandsWrapper
{
    public Timestamp Time { get; init; }
    public TeamColor Color { get; init; }
    public List<Command> Commands { get; init; } = [];
}