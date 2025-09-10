using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;
using auth_service.DTO;
using auth_service.Services;

namespace auth_service.Services
{
    public class AuthRpcServer : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private IConnection? _connection;
        private IModel? _channel;
        private string _exchange = "auth.exchange";
        private string _queue = "auth.requests";

        public AuthRpcServer(IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMq:HostName"],
                UserName = _configuration["RabbitMq:UserName"],
                Password = _configuration["RabbitMq:Password"],
                VirtualHost = _configuration["RabbitMq:VirtualHost"],
                Port = int.TryParse(_configuration["RabbitMq:Port"], out var port) ? port : 5672
            };
            _exchange = _configuration["RabbitMq:Exchange"] ?? _exchange;
            _queue = _configuration["RabbitMq:Queue"] ?? _queue;

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.register");
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.login");
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.checkEmail");
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.checkUsername");
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.forgot");
            _channel.QueueBind(_queue, _exchange, routingKey: "auth.reset");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnReceivedAsync;
            _channel.BasicConsume(queue: _queue, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            if (_channel == null) return;
            var replyProps = _channel.CreateBasicProperties();
            replyProps.CorrelationId = ea.BasicProperties.CorrelationId;

            string responseJson;
            try
            {
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                responseJson = await HandleMessageAsync(ea.RoutingKey, payload);
            }
            catch (Exception ex)
            {
                responseJson = JsonSerializer.Serialize(new { error = ex.Message });
            }
            finally
            {
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }

            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            _channel.BasicPublish(exchange: string.Empty,
                                  routingKey: ea.BasicProperties.ReplyTo,
                                  basicProperties: replyProps,
                                  body: responseBytes);
        }

        private async Task<string> HandleMessageAsync(string routingKey, string payload)
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            switch (routingKey)
            {
                case "auth.register":
                    var reg = JsonSerializer.Deserialize<RegisterDTO>(payload)!;
                    return JsonSerializer.Serialize(await authService.RegisterAsync(reg));
                case "auth.login":
                    var login = JsonSerializer.Deserialize<LoginDTO>(payload)!;
                    return JsonSerializer.Serialize(await authService.LoginAsync(login));
                case "auth.checkEmail":
                    return JsonSerializer.Serialize(await authService.IsEmailUniqueAsync(payload));
                case "auth.checkUsername":
                    return JsonSerializer.Serialize(await authService.IsUsernameUniqueAsync(payload));
                case "auth.forgot":
                    var forgot = JsonSerializer.Deserialize<ForgotPasswordDTO>(payload)!;
                    await authService.ForgotPasswordAsync(forgot.Email);
                    return JsonSerializer.Serialize(new { ok = true });
                case "auth.reset":
                    var reset = JsonSerializer.Deserialize<ResetPasswordDTO>(payload)!;
                    await authService.ResetPasswordAsync(reset.Token, reset.NewPassword);
                    return JsonSerializer.Serialize(new { ok = true });
                default:
                    throw new InvalidOperationException($"Unknown routing key: {routingKey}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}


