using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WisheraApp.DTO;

namespace WisheraApp.Services
{
    public interface IUserServiceClient
    {
        Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId);
        Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto);
        Task<string> UpdateAvatarAsync(string userId, IFormFile file);
        Task<bool> FollowUserAsync(string followerId, string followingId);
        Task<bool> UnfollowUserAsync(string followerId, string followingId);
        Task<List<UserSearchDTO>> SearchUsersAsync(string query, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
    }

    public class UserServiceClient : IUserServiceClient, IDisposable
    {
        private readonly IConnection? _connection;
        private readonly IModel? _channel;
        private readonly string _exchange;
        private readonly HttpClient _httpClient;
        private readonly string _userServiceUrl;
        private readonly bool _useRabbitMq;

        public UserServiceClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _exchange = "user.exchange";
            _userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.BaseAddress = new Uri(_userServiceUrl);

            // Support environment variables for Render.com/CloudAMQP
            var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") 
                ?? configuration["RabbitMq:HostName"] 
                ?? "localhost";
            var userName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
                ?? configuration["RabbitMq:UserName"] 
                ?? "guest";
            var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
                ?? configuration["RabbitMq:Password"] 
                ?? "guest";
            
            // Only use RabbitMQ if credentials are provided (not localhost/guest)
            _useRabbitMq = hostName != "localhost" || userName != "guest";
            
            if (_useRabbitMq)
            {
                try
                {
                    var virtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VIRTUALHOST") 
                        ?? configuration["RabbitMq:VirtualHost"] 
                        ?? "/";
                    
                    // On CloudAMQP/Render.com, if VirtualHost is "/", use the username as virtual host
                    if (virtualHost == "/" && userName != "guest")
                    {
                        virtualHost = userName;
                    }

                    var factory = new ConnectionFactory
                    {
                        HostName = hostName,
                        UserName = userName,
                        Password = password,
                        VirtualHost = virtualHost,
                        Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? configuration["RabbitMq:Port"], out var port) ? port : 5672
                    };
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
                    Console.WriteLine("UserServiceClient: RabbitMQ connection established");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UserServiceClient: Failed to connect to RabbitMQ: {ex.Message}. Falling back to HTTP.");
                    _useRabbitMq = false;
                }
            }
            else
            {
                Console.WriteLine("UserServiceClient: Using HTTP fallback (RabbitMQ not configured)");
            }
        }

        public async Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId)
        {
            if (!_useRabbitMq || _channel == null)
            {
                throw new InvalidOperationException("RabbitMQ is not available. Use HTTP fallback in controller.");
            }
            
            var payload = JsonSerializer.Serialize(new { UserId = userId, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("user.profile", payload);
            return JsonSerializer.Deserialize<UserProfileDTO>(response)!;
        }

        public async Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, UpdateDto = updateDto });
            var response = await SendRpcAsync("user.updateProfile", payload);
            return JsonSerializer.Deserialize<UserProfileDTO>(response)!;
        }

        public async Task<string> UpdateAvatarAsync(string userId, IFormFile file)
        {
            // For file uploads, we'll need to handle this differently
            // For now, we'll serialize the file info and handle it in the microservice
            var payload = JsonSerializer.Serialize(new { UserId = userId, File = file });
            var response = await SendRpcAsync("user.avatar", payload);
            var result = JsonSerializer.Deserialize<AvatarResponseDTO>(response)!;
            return result.AvatarUrl;
        }

        public async Task<bool> FollowUserAsync(string followerId, string followingId)
        {
            var payload = JsonSerializer.Serialize(new { FollowerId = followerId, FollowingId = followingId });
            var response = await SendRpcAsync("user.follow", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<bool> UnfollowUserAsync(string followerId, string followingId)
        {
            var payload = JsonSerializer.Serialize(new { FollowerId = followerId, FollowingId = followingId });
            var response = await SendRpcAsync("user.unfollow", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<List<UserSearchDTO>> SearchUsersAsync(string query, string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { Query = query, CurrentUserId = currentUserId, Page = page, PageSize = pageSize });
            var response = await SendRpcAsync("user.search", payload);
            return JsonSerializer.Deserialize<List<UserSearchDTO>>(response)!;
        }

        public async Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("user.followers", payload);
            return JsonSerializer.Deserialize<List<UserSearchDTO>>(response)!;
        }

        public async Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("user.following", payload);
            return JsonSerializer.Deserialize<List<UserSearchDTO>>(response)!;
        }

        private async Task<string> SendRpcAsync(string routingKey, string payload)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("RabbitMQ channel is not available");
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout

            try
            {
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

                // Register timeout cancellation
                cts.Token.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetException(new TimeoutException($"RPC call to '{routingKey}' timed out after 30 seconds. The user-service may not be running or RabbitMQ is not configured correctly."));
                    }
                });

                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"RPC call to '{routingKey}' timed out after 30 seconds. The user-service may not be running or RabbitMQ is not configured correctly.");
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }

    public class AvatarResponseDTO
    {
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
