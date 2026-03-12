using System.Collections.Concurrent;

public sealed class ReceiptSessionStore
{
    private readonly ConcurrentDictionary<string, ReceiptSessionState> _sessions = new(StringComparer.Ordinal);

    public ReceiptSessionState AddOrUpdate(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.ReceiptId] = session;
        return session;
    }

    public bool TryGet(string receiptId, out ReceiptSessionState? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);
        return _sessions.TryGetValue(receiptId, out session);
    }
}
