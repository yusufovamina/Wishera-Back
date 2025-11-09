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
            try
            {
                ConnectionFactory factory;
                string hostName = "unknown";
                
                // Check if CloudAMQP connection string is provided (format: amqps://user:pass@host:port/vhost)
                var cloudAmqpUrl = Environment.GetEnvironmentVariable("CLOUDAMQP_URL") 
                    ?? Environment.GetEnvironmentVariable("RABBITMQ_URL")
                    ?? _configuration["RabbitMq:ConnectionString"];
                
                if (!string.IsNullOrWhiteSpace(cloudAmqpUrl))
                {
                    // Parse CloudAMQP URL format: amqps://user:pass@host:port/vhost
                    Console.WriteLine($"[AuthRpcServer] Using CloudAMQP connection string from environment variable");
                    try
                    {
                        var uri = new Uri(cloudAmqpUrl);
                        hostName = uri.Host;
                        factory = new ConnectionFactory
                        {
                            Uri = uri
                        };
                        Console.WriteLine($"[AuthRpcServer] Parsed connection string successfully");
                    }
                    catch (Exception uriEx)
                    {
                        Console.WriteLine($"[AuthRpcServer] Error parsing connection string: {uriEx.Message}");
                        throw new InvalidOperationException($"Invalid RabbitMQ connection string format: {uriEx.Message}", uriEx);
                    }
                }
                else
                {
                    // Support individual environment variables for Render.com/CloudAMQP
                    hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") 
                        ?? _configuration["RabbitMq:HostName"] 
                        ?? "localhost";
                    var userName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
                        ?? _configuration["RabbitMq:UserName"] 
                        ?? "guest";
                    var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
                        ?? _configuration["RabbitMq:Password"] 
                        ?? "guest";
                    var virtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VIRTUALHOST") 
                        ?? _configuration["RabbitMq:VirtualHost"] 
                        ?? "/";
                    
                    // On CloudAMQP/Render.com, if VirtualHost is "/", use the username as virtual host
                    if (virtualHost == "/" && userName != "guest")
                    {
                        virtualHost = userName;
                    }

                    var port = 5672;
                    if (int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? _configuration["RabbitMq:Port"], out var parsedPort))
                    {
                        port = parsedPort;
                    }
                    else if (hostName.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase) || 
                             hostName.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase))
                    {
                        // If hostname contains protocol, try to parse port from URL
                        try
                        {
                            var uri = new Uri(hostName);
                            if (uri.Port > 0)
                            {
                                port = uri.Port;
                            }
                            hostName = uri.Host;
                        }
                        catch
                        {
                            // Ignore parsing errors
                        }
                    }

                    Console.WriteLine($"[AuthRpcServer] Connecting to RabbitMQ at {hostName}:{port} (user: {userName}, vhost: {virtualHost})");
                    
                    factory = new ConnectionFactory
                    {
                        HostName = hostName,
                        UserName = userName,
                        Password = password,
                        VirtualHost = virtualHost,
                        Port = port
                    };
                }
                
                _exchange = _configuration["RabbitMq:Exchange"] ?? _exchange;
                _queue = _configuration["RabbitMq:Queue"] ?? _queue;

                // Try to connect to RabbitMQ, but don't fail the service if it's unavailable
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

                Console.WriteLine($"[AuthRpcServer] Successfully connected to RabbitMQ at {hostName}");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the service startup
                // RabbitMQ is optional - the service can work without it (direct API calls will still work)
                Console.WriteLine($"[AuthRpcServer] Warning: Failed to connect to RabbitMQ: {ex.Message}");
                Console.WriteLine($"[AuthRpcServer] Service will continue without RabbitMQ RPC support. Direct API calls will still work.");
                // Set connection to null to indicate it's not available
                _connection = null;
                _channel = null;
            }

            return Task.CompletedTask;
        }

        private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            // If RabbitMQ is not connected, ignore messages
            if (_channel == null || _connection == null || !_connection.IsOpen)
            {
                Console.WriteLine("[AuthRpcServer] Received message but RabbitMQ is not connected. Ignoring.");
                return;
            }

            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthRpcServer] Error processing message: {ex.Message}");
                // Try to ack the message to prevent it from being redelivered
                try
                {
                    if (_channel != null && _channel.IsOpen)
                    {
                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                }
                catch
                {
                    // Ignore ack errors
                }
            }
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
            try
            {
                if (_channel != null && _channel.IsOpen)
                {
                    _channel.Close();
                }
                _channel?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthRpcServer] Error disposing channel: {ex.Message}");
            }

            try
            {
                if (_connection != null && _connection.IsOpen)
                {
                    _connection.Close();
                }
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthRpcServer] Error disposing connection: {ex.Message}");
            }
        }
    }
}


