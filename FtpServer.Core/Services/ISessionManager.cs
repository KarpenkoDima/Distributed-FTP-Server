namespace FtpServer.Core.Services;

internal interface ISessionManager
{
    // Save session (to Redis)
    Task SaveSessionAsync(FtpSession session);
    
    // Get session by ID (from Redis)
    Task<FtpSession> GetSessionAsync(string sessionId);

    // Remove session (from Redis)
    Task RemoveSesionAsync(string sessionId);
}
