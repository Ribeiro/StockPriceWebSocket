using StockPriceWebSocketApp.Manager;

namespace StockPriceWebSocketApp.Service
{
    public class StockPriceService
    {
        private readonly WebSocketSessionManager _sessionManager;
        private readonly ILogger<StockPriceService> _logger;

        public StockPriceService(WebSocketSessionManager sessionManager, ILogger<StockPriceService> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task StartSendingPriceUpdates(Guid sessionId)
        {
            try
            {
                while (true)
                {
                    var stockPrice = GetRandomStockPrice();
                    var message = $"Stock Price Update for session {sessionId}: {stockPrice}";

                    _logger.LogInformation("Sending stock price update: {StockPrice} to session: {SessionId}", stockPrice, sessionId);
                    await _sessionManager.SendToSession(sessionId, message);

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending stock price to session: {SessionId}", sessionId);
            }
        }

        private static decimal GetRandomStockPrice()
        {
            var random = new Random();
            return Math.Round((decimal)(random.NextDouble() * 1000), 2);
        }
    }
}