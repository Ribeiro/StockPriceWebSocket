using StockPriceWebSocketApp.Manager;
using StockPriceWebSocketApp.Service;

namespace StockPriceWebSocketApp
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<WebSocketSessionManager>();
            builder.Services.AddSingleton<StockPriceService>();

            var app = builder.Build();

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
            };

            app.UseWebSockets(webSocketOptions);

            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var sessionManager = app.Services.GetRequiredService<WebSocketSessionManager>();
                    var stockPriceService = app.Services.GetRequiredService<StockPriceService>();

                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                    var sessionId = await sessionManager.AddSession(webSocket);
                    Console.WriteLine($"New WebSocket connection established: SessionId={sessionId}");

                    await stockPriceService.StartSendingPriceUpdates(sessionId);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });

            var sessionManagerMonitorTask = Task.Run(async () =>
            {
                var sessionManager = app.Services.GetRequiredService<WebSocketSessionManager>();
                
                while (true)
                {
                    await sessionManager.MonitorSessionExpiration();
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
            });

            await app.RunAsync();

            await sessionManagerMonitorTask;
        }
    }
}