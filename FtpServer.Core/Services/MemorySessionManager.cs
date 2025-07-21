
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FtpServer.Core.Services;

/// <summary>
/// Why start without Redis?
///  - Testing the architecture—checking that the interface works
///  - Gradual complication—first Dictionary, then Redis
///  - Fallback solution—if Redis is unavailable, use memory
/// </summary>
internal class MemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, FtpSession> _session = new();

    #region Interface
    public Task<FtpSession> GetSessionAsync(string sessionId)
    {
        var found = _session.TryGetValue(sessionId, out var session);
        Console.WriteLine($"🔍 Session lookup: {sessionId} → {(found ? "Found" : "Not found")}");
        return Task.FromResult(session);
    }

    public Task RemoveSesionAsync(string sessionId)
    {
        var removed = _session.TryRemove(sessionId, out _);
        Console.WriteLine($"🗑️ Session removed: {sessionId} → {(removed ? "Success" : "Not found")}");
        return Task.CompletedTask;
    }

    public Task SaveSessionAsync(FtpSession session)
    {
        _session[session.SessionId] = session;
        Console.WriteLine($"💾 Session saved: {session.SessionId} (user: {session.Username})");
        return Task.CompletedTask;
    }
    #endregion
}
