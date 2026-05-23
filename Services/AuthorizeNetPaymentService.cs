using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Controllers.Bases;
using GolfAssociationCommunity.Data;

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

    public class GatewayTransactionDetails
    {
        public string TransactionId { get; set; } = string.Empty;
        public string? TransactionStatus { get; set; }
        public string? TransactionType { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public string? BatchId { get; set; }
        public string? CardType { get; set; }
        public string? CardLast4 { get; set; }
        public string? ResponseDescription { get; set; }
    }

    public interface IAuthorizeNetPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(int associationId, decimal amount, string cardNumber, string expirationDate, string cvv, PaymentBillingAddress billingAddress);
        Task<GatewayTransactionDetails?> GetTransactionDetailsAsync(int associationId, string transactionId);
        Task<PaymentResult> RefundTransactionAsync(int associationId, string transactionId, decimal amount, string cardLast4);
    }

    public class AuthorizeNetPaymentService : IAuthorizeNetPaymentService
    {
        private sealed class AssociationCredentials
        {
            public string? AuthorizeNetApiLoginId { get; init; }
            public string? AuthorizeNetTransactionKey { get; init; }
            public bool? AuthorizeNetUseSandbox { get; init; }
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthorizeNetPaymentService> _logger;

        public AuthorizeNetPaymentService(ApplicationDbContext context, ILogger<AuthorizeNetPaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int associationId, decimal amount, string cardNumber, string expirationDate, string cvv, PaymentBillingAddress billing)
        {
            try
            {
                var associationCredentials = await GetAssociationCredentialsAsync(associationId);

                var apiLoginId = associationCredentials?.AuthorizeNetApiLoginId;
                var transactionKey = associationCredentials?.AuthorizeNetTransactionKey;
                var useSandbox = associationCredentials?.AuthorizeNetUseSandbox ?? true;

                if (string.IsNullOrWhiteSpace(apiLoginId) || string.IsNullOrWhiteSpace(transactionKey))
                {
                    _logger.LogWarning(
                        "Authorize.Net configuration missing for association {AssociationId}. ApiLoginId present: {HasApiLoginId}, TransactionKey present: {HasTransactionKey}",
                        associationId,
                        !string.IsNullOrWhiteSpace(apiLoginId),
                        !string.IsNullOrWhiteSpace(transactionKey));

                    return new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment is temporarily unavailable. Please contact the association administrator."
                    };
                }

                var runEnvironment = useSandbox
                    ? AuthorizeNet.Environment.SANDBOX
                    : AuthorizeNet.Environment.PRODUCTION;

                var xmlBaseUrl = runEnvironment.getXmlBaseUrl();
                var requestUrl = $"{xmlBaseUrl}/xml/v1/request.api";

                _logger.LogInformation(
                    "Authorize.Net request setup. AssociationId: {AssociationId}, UseSandbox: {UseSandbox}, RequestUrl: {RequestUrl}, ApiLoginId: {MaskedApiLoginId}, HasTransactionKey: {HasTransactionKey}",
                    associationId,
                    useSandbox,
                    requestUrl,
                    MaskCredential(apiLoginId),
                    !string.IsNullOrWhiteSpace(transactionKey));

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
                        "Authorize.Net returned null response. AssociationId: {AssociationId}, Environment: {Environment}, ApiLoginIdPresent: {HasApiLoginId}, TransactionKeyPresent: {HasTransactionKey}. This usually indicates outbound connectivity, TLS/proxy interception, or gateway endpoint reachability issues.",
                        associationId,
                        useSandbox ? "Sandbox" : "Production",
                        !string.IsNullOrWhiteSpace(apiLoginId),
                        !string.IsNullOrWhiteSpace(transactionKey));

                    return new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment gateway did not return a response. Please try again in a moment."
                    };
                }

                if (response.messages.resultCode == messageTypeEnum.Ok &&
                    response.transactionResponse != null &&
                    !string.IsNullOrWhiteSpace(response.transactionResponse.transId))
                {
                    return new PaymentResult
                    {
                        Succeeded = true,
                        TransactionId = response.transactionResponse.transId
                    };
                }

                var transactionError = response.transactionResponse?.errors?.FirstOrDefault()?.errorText;
                var apiMessage = response.messages.message?.FirstOrDefault()?.text;
                var errorMessage = transactionError ?? apiMessage ?? "Payment authorization failed.";

                _logger.LogWarning("Authorize.Net payment failed for association {AssociationId}: {ErrorMessage}", associationId, errorMessage);
                return new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = errorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing payment through Authorize.Net for association {AssociationId}.", associationId);
                return new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = "Payment service encountered an error. Please try again."
                };
            }
        }

        public async Task<GatewayTransactionDetails?> GetTransactionDetailsAsync(int associationId, string transactionId)
        {
            try
            {
                var associationCredentials = await GetAssociationCredentialsAsync(associationId);
                var apiLoginId = associationCredentials?.AuthorizeNetApiLoginId;
                var transactionKey = associationCredentials?.AuthorizeNetTransactionKey;
                var useSandbox = associationCredentials?.AuthorizeNetUseSandbox ?? true;

                if (string.IsNullOrWhiteSpace(apiLoginId) || string.IsNullOrWhiteSpace(transactionKey))
                {
                    _logger.LogWarning("Authorize.Net configuration missing for association {AssociationId} while retrieving transaction {TransactionId}.", associationId, transactionId);
                    return null;
                }

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = useSandbox
                    ? AuthorizeNet.Environment.SANDBOX
                    : AuthorizeNet.Environment.PRODUCTION;

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType
                {
                    name = apiLoginId,
                    ItemElementName = ItemChoiceType.transactionKey,
                    Item = transactionKey
                };

                var request = new getTransactionDetailsRequest
                {
                    transId = transactionId
                };

                var controller = new getTransactionDetailsController(request);
                controller.Execute();

                var response = controller.GetApiResponse();
                if (response?.messages?.resultCode != messageTypeEnum.Ok || response.transaction is null)
                {
                    _logger.LogWarning("Authorize.Net transaction details request failed for association {AssociationId}, transaction {TransactionId}.", associationId, transactionId);
                    return null;
                }

                return new GatewayTransactionDetails
                {
                    TransactionId = response.transaction.transId ?? transactionId,
                    TransactionStatus = response.transaction.transactionStatus,
                    TransactionType = response.transaction.transactionType,
                    Amount = ParseDecimal(response.transaction.settleAmount) ?? ParseDecimal(response.transaction.authAmount) ?? ParseDecimal(response.transaction.requestedAmount),
                    SubmittedAtUtc = ParseUtcDate(response.transaction.submitTimeUTC),
                    BatchId = response.transaction.batch?.batchId,
                    ResponseDescription = response.transaction.responseReasonDescription
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving Authorize.Net transaction details for association {AssociationId} and transaction {TransactionId}.", associationId, transactionId);
                return null;
            }
        }

        public async Task<PaymentResult> RefundTransactionAsync(int associationId, string transactionId, decimal amount, string cardLast4)
        {
            try
            {
                var associationCredentials = await GetAssociationCredentialsAsync(associationId);
                var apiLoginId = associationCredentials?.AuthorizeNetApiLoginId;
                var transactionKey = associationCredentials?.AuthorizeNetTransactionKey;
                var useSandbox = associationCredentials?.AuthorizeNetUseSandbox ?? true;

                if (string.IsNullOrWhiteSpace(apiLoginId) || string.IsNullOrWhiteSpace(transactionKey))
                {
                    return new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment refund is temporarily unavailable. Please contact the association administrator."
                    };
                }

                if (string.IsNullOrWhiteSpace(cardLast4) || cardLast4.Trim().Length < 4)
                {
                    return new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "A saved card reference is required to process this refund."
                    };
                }

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = useSandbox
                    ? AuthorizeNet.Environment.SANDBOX
                    : AuthorizeNet.Environment.PRODUCTION;

                ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType
                {
                    name = apiLoginId,
                    ItemElementName = ItemChoiceType.transactionKey,
                    Item = transactionKey
                };

                var refundRequest = new transactionRequestType
                {
                    transactionType = transactionTypeEnum.refundTransaction.ToString(),
                    amount = amount,
                    refTransId = transactionId,
                    payment = new paymentType
                    {
                        Item = new creditCardType
                        {
                            cardNumber = cardLast4.Trim()[^4..],
                            expirationDate = "XXXX"
                        }
                    }
                };

                var request = new createTransactionRequest
                {
                    transactionRequest = refundRequest
                };

                var controller = new createTransactionController(request);
                controller.Execute();

                var response = controller.GetApiResponse();
                if (response == null)
                {
                    return new PaymentResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Payment gateway did not return a response. Please try again in a moment."
                    };
                }

                if (response.messages.resultCode == messageTypeEnum.Ok &&
                    response.transactionResponse != null &&
                    !string.IsNullOrWhiteSpace(response.transactionResponse.transId))
                {
                    return new PaymentResult
                    {
                        Succeeded = true,
                        TransactionId = response.transactionResponse.transId
                    };
                }

                var transactionError = response.transactionResponse?.errors?.FirstOrDefault()?.errorText;
                var apiMessage = response.messages.message?.FirstOrDefault()?.text;
                var errorMessage = transactionError ?? apiMessage ?? "Refund failed.";

                _logger.LogWarning("Authorize.Net refund failed for association {AssociationId}, transaction {TransactionId}: {ErrorMessage}", associationId, transactionId, errorMessage);
                return new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = errorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while refunding Authorize.Net transaction {TransactionId} for association {AssociationId}.", transactionId, associationId);
                return new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = "Refund service encountered an error. Please try again."
                };
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

        private static string MaskCredential(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<empty>";
            }

            var trimmed = value.Trim();
            if (trimmed.Length <= 4)
            {
                return new string('*', trimmed.Length);
            }

            return $"{new string('*', trimmed.Length - 4)}{trimmed[^4..]}";
        }

        private async Task<AssociationCredentials?> GetAssociationCredentialsAsync(int associationId)
        {
            return await _context.GolfAssociations
                .AsNoTracking()
                .Where(ga => ga.Id == associationId)
                .Select(ga => new AssociationCredentials
                {
                    AuthorizeNetApiLoginId = ga.AuthorizeNetApiLoginId,
                    AuthorizeNetTransactionKey = ga.AuthorizeNetTransactionKey,
                    AuthorizeNetUseSandbox = ga.AuthorizeNetUseSandbox
                })
                .FirstOrDefaultAsync();
        }

        private static DateTime? ParseUtcDate(object? value)
        {
            var text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return DateTime.TryParse(text, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : null;
        }

        private static decimal? ParseDecimal(object? value)
        {
            var text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return decimal.TryParse(text, out var parsed) ? parsed : null;
        }

        private static string? ExtractLast4(object? cardNumber)
        {
            var text = cardNumber?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : digits;
        }
    }
}
