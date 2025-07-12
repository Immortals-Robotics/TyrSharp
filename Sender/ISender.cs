using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

public interface ISender : IDisposable
{
    bool Send(CommandsWrapper commands);
}