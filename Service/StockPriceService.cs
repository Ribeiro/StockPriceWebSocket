using StockPriceWebSocketApp.Manager;

namespace StockPriceWebSocketApp.Service
{
    public class StockPriceService
    {
        private readonly WebSocketSessionManager _sessionManager;

        public StockPriceService(WebSocketSessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public async Task StartSendingPriceUpdates(Guid sessionId)
        {
            try
            {
                while (true)
                {
                    var stockPrice = GetRandomStockPrice();
                    var message = $"Stock Price Update for session {sessionId}: {stockPrice}";

                    await _sessionManager.SendToSession(sessionId, message);

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar preço de ação: {ex.Message}");
            }
        }

        private static decimal GetRandomStockPrice()
        {
            var random = new Random();
            return Math.Round((decimal)(random.NextDouble() * 1000), 2);
        }
    }

}