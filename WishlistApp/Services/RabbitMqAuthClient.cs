using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WishlistApp.Services
{
    public interface IRabbitMqAuthClient
    {
        Task<string> SendRpcAsync(string routingKey, string payload, CancellationToken cancellationToken = default);
    }

    public class RabbitMqAuthClient : IRabbitMqAuthClient, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchange;

        public RabbitMqAuthClient(IConfiguration configuration)
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMq:HostName"],
                UserName = configuration["RabbitMq:UserName"],
                Password = configuration["RabbitMq:Password"],
                VirtualHost = configuration["RabbitMq:VirtualHost"],
                Port = int.TryParse(configuration["RabbitMq:Port"], out var port) ? port : 5672
            };
            _exchange = configuration["RabbitMq:Exchange"] ?? "auth.exchange";
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
        }

        public Task<string> SendRpcAsync(string routingKey, string payload, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            var replyQueue = _channel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
            var consumer = new EventingBasicConsumer(_channel);

            var correlationId = Guid.NewGuid().ToString();
            consumer.Received += (model, ea) =>
            {
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                    tcs.TrySetResult(response);
                }
            };
            _channel.BasicConsume(consumer: consumer, queue: replyQueue.QueueName, autoAck: true);

            var props = _channel.CreateBasicProperties();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueue.QueueName;

            var body = Encoding.UTF8.GetBytes(payload);
            _channel.BasicPublish(exchange: _exchange, routingKey: routingKey, basicProperties: props, body: body);

            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}


