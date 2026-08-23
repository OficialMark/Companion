using System.Collections.Concurrent;
using Companion.Application.Contracts;

namespace Companion.Api.Realtime;

public sealed class CampaignPresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _participants = new();

    public bool IsOnline(Guid participantId) =>
        _participants.TryGetValue(participantId, out var connections) && !connections.IsEmpty;

    public bool Connected(Guid participantId, string connectionId)
    {
        _connections[connectionId] = participantId;
        var connections = _participants.GetOrAdd(participantId, _ => new ConcurrentDictionary<string, byte>());
        var wasOffline = connections.IsEmpty;
        connections[connectionId] = 0;
        return wasOffline;
    }

    public bool Disconnected(string connectionId, out Guid participantId)
    {
        if (!_connections.TryRemove(connectionId, out participantId) ||
            !_participants.TryGetValue(participantId, out var connections))
        {
            return false;
        }

        connections.TryRemove(connectionId, out _);
        if (!connections.IsEmpty)
        {
            return false;
        }

        _participants.TryRemove(participantId, out _);
        return true;
    }
}
