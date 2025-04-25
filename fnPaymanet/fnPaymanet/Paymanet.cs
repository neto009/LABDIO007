using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using fnPaymanet.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace fnPaymanet
{
    public class Paymanet
    {
        private readonly ILogger<Paymanet> _logger;
        private readonly IConfiguration _configuration;
        private readonly string[] StatusList = { "Aprovado", "Reprovado", "Em analise"};
        private readonly Random random = new Random();

        public Paymanet(ILogger<Paymanet> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function(nameof(Paymanet))]
        [CosmosDBOutput("%CosmosDb%", "%CosmosContainer%", Connection = "CosmosDBConnection", CreateIfNotExists = true)]
        public async Task<object?> Run(
            [ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message ID: {id}", message.MessageId);
            _logger.LogInformation("Message Body: {body}", message.Body);
            _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

            PaymantModel paymantModel = null;

            try
            {
                paymantModel = JsonSerializer.Deserialize<PaymantModel>(message.Body.ToString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymantModel == null)
                {
                    _logger.LogError("Deserialization failed. PaymantModel is null.");
                    await messageActions.DeadLetterMessageAsync(message, null, "PaymantModel is null");
                }
                int index = random.Next(StatusList.Length);
                string status = StatusList[index];
                paymantModel.Status = status;

                if (status == "Aprovado")
                {
                    paymantModel.DataAprovacao = DateTime.UtcNow;
                    await SentToNotificationQueue(paymantModel);
                }
                return paymantModel;
            } catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await messageActions.DeadLetterMessageAsync(message, null, $"Error processing message: {ex.Message}");
                return null;
            } finally
            {
                await messageActions.CompleteMessageAsync(message);
            }
        }

        private async Task SentToNotificationQueue(PaymantModel paymantModel)
        {
            var connectionString = _configuration.GetConnectionString("ServiceBus");
            var queueName = _configuration.GetSection("NotificationQueue").Value.ToString();
            var client = new ServiceBusClient(connectionString);
            var sender = client.CreateSender(queueName);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(paymantModel))
            {
                ContentType = "application/json",
            };

            message.ApplicationProperties["idPayment"] = paymantModel.idPayment;
            message.ApplicationProperties["type"] = "notification";
            message.ApplicationProperties["message"] = "Pagamento Aprovado";
        }
    }
}
