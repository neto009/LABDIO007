using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace fnSBRentProcess
{
    public class ProcessaLocacao
    {
        private readonly ILogger<ProcessaLocacao> _logger;
        private readonly IConfiguration _configuration;

        public ProcessaLocacao(ILogger<ProcessaLocacao> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function(nameof(ProcessaLocacao))]
        public async Task Run(
            [ServiceBusTrigger("fila-locacao-auto", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message ID: {id}", message.MessageId);
            var body = message.Body.ToString();
            _logger.LogInformation("Message Body: {body}", message.Body);
            _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

            RentModel rentModel = null;

            try
            {
                rentModel = JsonSerializer.Deserialize<RentModel>(body, new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                });

                if (rentModel == null)
                {
                    _logger.LogError("Deserialization returned null");
                    await messageActions.DeadLetterMessageAsync(message, null, "Mensagem mal formatada");
                    return;
                }

                var connectionString = _configuration.GetConnectionString("SqlConnectionString");
                using var connection = new SqlConnection(connectionString);

                await connection.OpenAsync();
                var command = new SqlCommand("INSERT INTO Locacao (Nome, Email, Modelo, Ano, TempoAluguel, Data) VALUES (@Nome, @Email, @Modelo, @Ano, @TempoAluguel, @Data)", connection);
                command.Parameters.AddWithValue("@Nome", rentModel.nome);
                command.Parameters.AddWithValue("@Email", rentModel.email);
                command.Parameters.AddWithValue("@Modelo", rentModel.modelo);
                command.Parameters.AddWithValue("@Ano", rentModel.ano);
                command.Parameters.AddWithValue("@TempoAluguel", rentModel.tempoAluguel);
                command.Parameters.AddWithValue("@Data", rentModel.data);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                connection.Close();

                var serviceBusConnection = _configuration.GetSection("ServiceBusConnection").Value.ToString();
                var serviceBusQueue = _configuration.GetSection("ServiceBusQueue").Value.ToString();

                sendMessageToPay(serviceBusConnection, serviceBusQueue, rentModel);

                await messageActions.CompleteMessageAsync(message);
            } catch(Exception ex)
            {
                _logger.LogError("Failed to deserialize message body: {error}", ex.Message);
                await messageActions.DeadLetterMessageAsync(message, null, $"Erro ao processar a mensagem: {ex.Message}");
                return;
            }
        }

        private void sendMessageToPay(string serviceBusConnection, string serviceBusQueue, RentModel rentModel)
        {
            ServiceBusClient client = new ServiceBusClient(serviceBusConnection);
            ServiceBusSender sender = client.CreateSender(serviceBusQueue);
            ServiceBusMessage message = new ServiceBusMessage("Mensagem de pagamento");
            message.ContentType = "application/json";
            message.ApplicationProperties.Add("Tipo", "Pagamento");
            message.ApplicationProperties.Add("Nome", rentModel.nome);
            message.ApplicationProperties.Add("Email", rentModel.email);
            message.ApplicationProperties.Add("Modelo", rentModel.modelo);
            message.ApplicationProperties.Add("Ano", rentModel.ano);
            message.ApplicationProperties.Add("TempoAluguel", rentModel.tempoAluguel);
            message.ApplicationProperties.Add("Data", rentModel.data.ToString("yyyy-MM-ddTHH:mm:ss"));

            sender.SendMessageAsync(message).Wait();
            sender.DisposeAsync();
        }
    }
}
