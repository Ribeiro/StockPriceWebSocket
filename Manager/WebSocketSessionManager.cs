using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace StockPriceWebSocketApp.Manager
{
    public class WebSocketSessionManager
    {
        private readonly ConcurrentDictionary<Guid, (WebSocket Socket, DateTime ExpirationTime)> _sessions = new();

        private readonly TimeSpan _defaultSessionTimeout = TimeSpan.FromMinutes(1);

        public WebSocketSessionManager() { }

        public async Task<Guid> AddSession(WebSocket webSocket, TimeSpan? customTimeout = null)
        {
            var sessionId = Guid.NewGuid();
            var expirationTime = DateTime.UtcNow + (customTimeout ?? _defaultSessionTimeout);

            _sessions[sessionId] = (webSocket, expirationTime);
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

                Console.WriteLine($"Session {sessionId} removed.");
            }
        }

        public async Task SendToSession(Guid sessionId, string message)
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.Socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await session.Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
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
                await Task.WhenAll(sendTasks);
            }
        }

        public async Task MonitorSessionExpiration()
        {
            Console.WriteLine($"Starting MonitorSessionExpiration..");
            foreach (var session in _sessions.Keys)
            {
                var currentSession = _sessions[session];
                var expirationTime = currentSession.ExpirationTime;
                var timeToExpire = expirationTime - DateTime.UtcNow;

                if (timeToExpire <= TimeSpan.Zero)
                {
                    Console.WriteLine($"Session {session} expired!");
                    await RemoveSession(session);
                    break;
                }
            }
        }
    }
}