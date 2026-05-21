using Microsoft.Extensions.Logging;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Controllers.Bases;

namespace GolfAssociationCommunity.Services
{
    public class PaymentBillingAddress
    {
        public string FullName { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    public class PaymentResult
    {
        public bool Succeeded { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IAuthorizeNetPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(decimal amount, string cardNumber, string expirationDate, string cvv, PaymentBillingAddress billingAddress);
    }

    public class AuthorizeNetPaymentService : IAuthorizeNetPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthorizeNetPaymentService> _logger;

        public AuthorizeNetPaymentService(IConfiguration configuration, ILogger<AuthorizeNetPaymentService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task<PaymentResult> ProcessPaymentAsync(decimal amount, string cardNumber, string expirationDate, string cvv, PaymentBillingAddress billing)
        {
            try
            {
                var apiLoginId = _configuration["AuthorizeNet:ApiLoginId"];
                var transactionKey = _configuration["AuthorizeNet:TransactionKey"];
                var useSandboxText = _configuration["AuthorizeNet:UseSandbox"];

                if (string.IsNullOrWhiteSpace(apiLoginId) || string.IsNullOrWhiteSpace(transactionKey))
                {
                    _logger.LogWarning(
                        "Authorize.Net configuration missing. ApiLoginId present: {HasApiLoginId}, TransactionKey present: {HasTransactionKey}",
                        !string.IsNullOrWhiteSpace(apiLoginId),
                        !string.IsNullOrWhiteSpace(transactionKey));

                    return Task.FromResult(new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment is temporarily unavailable. Please contact the association administrator."
                    });
                }

                var useSandbox = true;
                if (!string.IsNullOrWhiteSpace(useSandboxText) && bool.TryParse(useSandboxText, out var parsedSandbox))
                {
                    useSandbox = parsedSandbox;
                }

                var runEnvironment = useSandbox
                    ? AuthorizeNet.Environment.SANDBOX
                    : AuthorizeNet.Environment.PRODUCTION;

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = runEnvironment;

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType
                {
                    name = apiLoginId,
                    ItemElementName = ItemChoiceType.transactionKey,
                    Item = transactionKey
                };

                var creditCard = new creditCardType
                {
                    cardNumber = cardNumber,
                    expirationDate = expirationDate,
                    cardCode = cvv
                };

                var (firstName, lastName) = SplitName(billing.FullName);

                var billToAddress = new customerAddressType
                {
                    firstName = firstName,
                    lastName = lastName,
                    address = billing.AddressLine1,
                    city = billing.City,
                    state = billing.State,
                    zip = billing.ZipCode,
                    country = billing.Country
                };

                var transactionRequest = new transactionRequestType
                {
                    transactionType = transactionTypeEnum.authCaptureTransaction.ToString(),
                    amount = amount,
                    payment = new paymentType { Item = creditCard },
                    billTo = billToAddress
                };

                var request = new createTransactionRequest
                {
                    transactionRequest = transactionRequest
                };

                var controller = new createTransactionController(request);
                controller.Execute();

                var response = controller.GetApiResponse();
                if (response == null)
                {
                    _logger.LogWarning(
                        "Authorize.Net returned null response. Environment: {Environment}, ApiLoginIdPresent: {HasApiLoginId}, TransactionKeyPresent: {HasTransactionKey}. This usually indicates outbound connectivity, TLS/proxy interception, or gateway endpoint reachability issues.",
                        useSandbox ? "Sandbox" : "Production",
                        !string.IsNullOrWhiteSpace(apiLoginId),
                        !string.IsNullOrWhiteSpace(transactionKey));

                    return Task.FromResult(new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment gateway did not return a response. Please try again in a moment."
                    });
                }

                if (response.messages.resultCode == messageTypeEnum.Ok &&
                    response.transactionResponse != null &&
                    !string.IsNullOrWhiteSpace(response.transactionResponse.transId))
                {
                    return Task.FromResult(new PaymentResult
                    {
                        Succeeded = true,
                        TransactionId = response.transactionResponse.transId
                    });
                }

                var transactionError = response.transactionResponse?.errors?.FirstOrDefault()?.errorText;
                var apiMessage = response.messages.message?.FirstOrDefault()?.text;
                var errorMessage = transactionError ?? apiMessage ?? "Payment authorization failed.";

                _logger.LogWarning("Authorize.Net payment failed: {ErrorMessage}", errorMessage);
                return Task.FromResult(new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = errorMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing payment through Authorize.Net.");
                return Task.FromResult(new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = "Payment service encountered an error. Please try again."
                });
            }
        }

        private static (string FirstName, string LastName) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return ("", "");
            }

            var parts = fullName
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return (parts[0], "");
            }

            var firstName = parts[0];
            var lastName = string.Join(' ', parts.Skip(1));
            return (firstName, lastName);
        }
    }
}
