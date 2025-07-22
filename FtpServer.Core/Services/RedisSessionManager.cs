using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace FtpServer.Core.Services
{
    internal class RedisSessionManager : ISessionManager
    {

        private readonly IDatabase _database;
        private readonly string _key_Prefix = "ftp:session";

        public RedisSessionManager(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }
        #region Interface ISessionManager
        public async Task<FtpSession> GetSessionAsync(string sessionId)
        {
            var key = _key_Prefix + sessionId;
            var json = await _database.StringGetAsync(key);

            if (false == json.HasValue)
            {
                Console.WriteLine($"🔍 Redis: Session not found: {sessionId}");
                return null;
            }

            // TODO: Desiareliaze in FtpSession
            Console.WriteLine($"🔍 Redis: Session found: {sessionId}");
            return null; // stub
        }

        public async Task RemoveSesionAsync(string sessionId)
        {
            var key = _key_Prefix + sessionId;
            var removed = await _database.KeyDeleteAsync(key);
            Console.WriteLine($"🗑️ Redis: Session removed: {sessionId} → {(removed ? "Success" : "Not found")}");
        }

        public async Task SaveSessionAsync(FtpSession session)
        {
            var key = _key_Prefix + session.SessionId;
            var sessionData = new
            {
                SessionId = session.SessionId,
                Username = session.Username,
                IsAuthenticated = session.IsAuthenticated,
                CurrentDirectory = session.CurrentDirectory,
                ClientEndpoint = session.ClientEndpoint
            };

            var json = JsonSerializer.Serialize(sessionData);
            await _database.StringSetAsync(key, json, TimeSpan.FromMinutes(30));

            Console.WriteLine($"💾 Redis: Session saved: {session.SessionId} (user: {session.Username})");
        }
        #endregion
    }
}
