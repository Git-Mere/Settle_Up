using System.Collections.Concurrent;

public sealed class ReceiptSessionStore
{
    private readonly ConcurrentDictionary<string, ReceiptSessionState> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _receiptIdsByBlobUrl = new(StringComparer.Ordinal);

    public ReceiptSessionState AddOrUpdate(
        ReceiptSessionState session,
        string? previousReceiptId = null,
        string? previousBlobUrl = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!string.IsNullOrWhiteSpace(previousReceiptId) &&
            !string.Equals(previousReceiptId, session.ReceiptId, StringComparison.Ordinal))
        {
            _sessions.TryRemove(previousReceiptId, out _);
        }

        _sessions[session.ReceiptId] = session;

        if (!string.IsNullOrWhiteSpace(previousBlobUrl) &&
            !string.Equals(previousBlobUrl, session.BlobUrl, StringComparison.Ordinal))
        {
            _receiptIdsByBlobUrl.TryRemove(previousBlobUrl, out _);
        }

        if (!string.IsNullOrWhiteSpace(session.BlobUrl))
        {
            _receiptIdsByBlobUrl[session.BlobUrl] = session.ReceiptId;
        }

        return session;
    }

    public bool TryGet(string receiptId, out ReceiptSessionState? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);
        return _sessions.TryGetValue(receiptId, out session);
    }

    public bool TryGetByBlobUrl(string blobUrl, out ReceiptSessionState? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            return false;
        }

        if (!_receiptIdsByBlobUrl.TryGetValue(blobUrl, out var receiptId))
        {
            return false;
        }

        return _sessions.TryGetValue(receiptId, out session);
    }
}
