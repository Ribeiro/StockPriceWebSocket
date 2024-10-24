using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace StockPriceWebSocketApp.Manager
{
    public class WebSocketSessionManager
    {
        private readonly ConcurrentDictionary<Guid, (WebSocket Socket, DateTime ExpirationTime)> _sessions = new();
        private readonly TimeSpan _defaultSessionTimeout = TimeSpan.FromMinutes(1);
        private readonly ILogger<WebSocketSessionManager> _logger;

        public WebSocketSessionManager(ILogger<WebSocketSessionManager> logger)
        {
            _logger = logger;
        }

        public async Task<Guid> AddSession(WebSocket webSocket, TimeSpan? customTimeout = null)
        {
            var sessionId = Guid.NewGuid();
            var expirationTime = DateTime.UtcNow + (customTimeout ?? _defaultSessionTimeout);

            _sessions[sessionId] = (webSocket, expirationTime);
            _logger.LogInformation("Session {SessionId} added with expiration time {ExpirationTime}.", sessionId, expirationTime);
            return sessionId;
        }

        public async Task RemoveSession(Guid sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                if (session.Socket.State != WebSocketState.Closed)
                {
                    await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                }

                _logger.LogInformation("Session {SessionId} removed.", sessionId);
            }
            else
            {
                _logger.LogWarning("Attempted to remove non-existent session {SessionId}.", sessionId);
            }
        }

        public async Task SendToSession(Guid sessionId, string message)
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.Socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await session.Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                _logger.LogInformation("Sent message to session {SessionId}: {Message}", sessionId, message);
            }
            else
            {
                _logger.LogWarning("Failed to send message to session {SessionId}: session not found or WebSocket closed.", sessionId);
            }
        }

        public async Task Broadcast(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var arraySegment = new ArraySegment<byte>(buffer);

            List<Task> sendTasks = new List<Task>(_sessions.Count);

            foreach (var session in _sessions.Values)
            {
                if (session.Socket.State == WebSocketState.Open)
                {
                    sendTasks.Add(session.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            if (sendTasks.Count > 0)
            {
                _logger.LogInformation("Broadcasting message to {Count} sessions.", sendTasks.Count);
                await Task.WhenAll(sendTasks);
            }
            else
            {
                _logger.LogWarning("No open WebSocket sessions to broadcast to.");
            }
        }

        public async Task MonitorSessionExpiration()
        {
            _logger.LogInformation("Running session expiration check...");
            foreach (var sessionId in _sessions.Keys)
            {
                var (_, expirationTime) = _sessions[sessionId];
                var timeToExpire = expirationTime - DateTime.UtcNow;

                if (timeToExpire <= TimeSpan.Zero)
                {
                    _logger.LogInformation("Session {SessionId} expired.", sessionId);
                    await RemoveSession(sessionId);
                }
            }
        }
    }
}