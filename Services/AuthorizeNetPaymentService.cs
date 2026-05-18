using Microsoft.Extensions.Logging;

namespace GolfAssociationCommunity.Services
{
    public interface IAuthorizeNetPaymentService
    {
        Task<bool> ProcessPaymentAsync(decimal amount, string cardNumber, string expirationDate, string cvv);
    }

    public class AuthorizeNetPaymentService : IAuthorizeNetPaymentService
    {
        private readonly ILogger<AuthorizeNetPaymentService> _logger;

        public AuthorizeNetPaymentService(ILogger<AuthorizeNetPaymentService> logger)
        {
            _logger = logger;
        }

        public Task<bool> ProcessPaymentAsync(decimal amount, string cardNumber, string expirationDate, string cvv)
        {
            _logger.LogInformation("Processing payment via stubbed Authorize.Net service for amount {Amount}", amount);
            return Task.FromResult(true);
        }
    }
}
