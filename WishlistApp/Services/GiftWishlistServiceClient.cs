using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WisheraApp.DTO;
using WisheraApp.Models;

namespace WisheraApp.Services
{
    public interface IGiftWishlistServiceClient
    {
        // Wishlist operations
        Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto);
        Task<WishlistResponseDTO> GetWishlistAsync(string id, string currentUserId);
        Task<WishlistResponseDTO> UpdateWishlistAsync(string id, string currentUserId, UpdateWishlistDTO updateDto);
        Task<bool> DeleteWishlistAsync(string id, string currentUserId);
        Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<WishlistFeedDTO>> GetFeedAsync(string currentUserId, int page = 1, int pageSize = 20);
        Task<List<WishlistFeedDTO>> GetLikedWishlistsAsync(string currentUserId, int page = 1, int pageSize = 20);
        Task<bool> LikeWishlistAsync(string id, string currentUserId);
        Task<bool> UnlikeWishlistAsync(string id, string currentUserId);
        Task<CommentDTO> AddCommentAsync(string id, string currentUserId, CreateCommentDTO commentDto);
        Task<CommentDTO> UpdateCommentAsync(string id, string currentUserId, UpdateCommentDTO commentDto);
        Task<bool> DeleteCommentAsync(string id, string currentUserId);
        Task<List<CommentDTO>> GetCommentsAsync(string id, int page = 1, int pageSize = 20);
        Task<string> UploadItemImageAsync(IFormFile file);
        string[] GetCategories();

        // Gift operations
        Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, string userId, IFormFile? imageFile);
        Task<object> UpdateGiftAsync(string id, GiftUpdateDto giftDto);
        Task<object> DeleteGiftAsync(string id);
        Task<object> ReserveGiftAsync(string id, string userId, string username);
        Task<object> CancelReservationAsync(string id, string userId);
        Task<List<Gift>> GetReservedGiftsAsync(string userId);
        Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy);
        Task<Gift> GetGiftByIdAsync(string id);
        Task<List<Gift>> GetSharedWishlistAsync(string userId);
        Task<string> UploadGiftImageAsync(string id, IFormFile imageFile);
        Task<object> AssignGiftToWishlistAsync(string id, string wishlistId, string userId);
        Task<object> RemoveGiftFromWishlistAsync(string id, string userId);
    }

    public class GiftWishlistServiceClient : IGiftWishlistServiceClient, IDisposable
    {
        private readonly IConnection? _connection;
        private readonly IModel? _channel;
        private readonly string _exchange;
        private readonly ILogger<GiftWishlistServiceClient>? _logger;

        public GiftWishlistServiceClient(IConfiguration configuration, ILogger<GiftWishlistServiceClient>? logger = null)
        {
            _logger = logger;
            _exchange = "giftwishlist.exchange";
            
            try
            {
                // Support environment variables for Render.com/CloudAMQP
                var cloudAmqpUrl = Environment.GetEnvironmentVariable("CLOUDAMQP_URL") 
                    ?? Environment.GetEnvironmentVariable("RABBITMQ_URL")
                    ?? configuration["RabbitMq:ConnectionString"];
                
                ConnectionFactory factory;
                
                if (!string.IsNullOrWhiteSpace(cloudAmqpUrl))
                {
                    // Parse CloudAMQP URL format: amqps://user:pass@host:port/vhost
                    Console.WriteLine("[GiftWishlistServiceClient] Using CloudAMQP connection string from environment variable");
                    try
                    {
                        factory = new ConnectionFactory
                        {
                            Uri = new Uri(cloudAmqpUrl)
                        };
                        Console.WriteLine("[GiftWishlistServiceClient] Parsed connection string successfully");
                    }
                    catch (Exception uriEx)
                    {
                        Console.WriteLine($"[GiftWishlistServiceClient] Error parsing connection string: {uriEx.Message}");
                        _logger?.LogWarning(uriEx, "Failed to parse RabbitMQ connection string. Service will continue without RabbitMQ support.");
                        _connection = null;
                        _channel = null;
                        return;
                    }
                }
                else
                {
                    var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") 
                        ?? configuration["RabbitMq:HostName"] 
                        ?? "localhost";
                    var userName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
                        ?? configuration["RabbitMq:UserName"] 
                        ?? "guest";
                    var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
                        ?? configuration["RabbitMq:Password"] 
                        ?? "guest";
                    var virtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VIRTUALHOST") 
                        ?? configuration["RabbitMq:VirtualHost"] 
                        ?? "/";
                    
                    // On CloudAMQP/Render.com, if VirtualHost is "/", use the username as virtual host
                    if (virtualHost == "/" && userName != "guest")
                    {
                        virtualHost = userName;
                    }

                    var port = 5672;
                    if (int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? configuration["RabbitMq:Port"], out var parsedPort))
                    {
                        port = parsedPort;
                    }

                    factory = new ConnectionFactory
                    {
                        HostName = hostName,
                        UserName = userName,
                        Password = password,
                        VirtualHost = virtualHost,
                        Port = port
                    };
                }
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
                Console.WriteLine($"[GiftWishlistServiceClient] Successfully connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GiftWishlistServiceClient] Warning: Failed to connect to RabbitMQ: {ex.Message}");
                Console.WriteLine($"[GiftWishlistServiceClient] Service will continue without RabbitMQ RPC support. API calls may fail.");
                _logger?.LogWarning(ex, "Failed to connect to RabbitMQ. Service will continue without RabbitMQ support.");
                _connection = null;
                _channel = null;
            }
        }

        // Wishlist operations
        public async Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, CreateDto = createDto });
            var response = await SendRpcAsync("wishlist.create", payload);
            return JsonSerializer.Deserialize<WishlistResponseDTO>(response)!;
        }

        public async Task<WishlistResponseDTO> GetWishlistAsync(string id, string currentUserId)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("wishlist.get", payload);
            return JsonSerializer.Deserialize<WishlistResponseDTO>(response)!;
        }

        public async Task<WishlistResponseDTO> UpdateWishlistAsync(string id, string currentUserId, UpdateWishlistDTO updateDto)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId, UpdateDto = updateDto });
            var response = await SendRpcAsync("wishlist.update", payload);
            return JsonSerializer.Deserialize<WishlistResponseDTO>(response)!;
        }

        public async Task<bool> DeleteWishlistAsync(string id, string currentUserId)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("wishlist.delete", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, CurrentUserId = currentUserId, Page = page, PageSize = pageSize });
            var response = await SendRpcAsync("wishlist.userWishlists", payload);
            return JsonSerializer.Deserialize<List<WishlistFeedDTO>>(response)!;
        }

        public async Task<List<WishlistFeedDTO>> GetFeedAsync(string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { CurrentUserId = currentUserId, Page = page, PageSize = pageSize });
            var response = await SendRpcAsync("wishlist.feed", payload);
            return JsonSerializer.Deserialize<List<WishlistFeedDTO>>(response)!;
        }

        public async Task<List<WishlistFeedDTO>> GetLikedWishlistsAsync(string currentUserId, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { CurrentUserId = currentUserId, Page = page, PageSize = pageSize });
            var response = await SendRpcAsync("wishlist.liked", payload);
            return JsonSerializer.Deserialize<List<WishlistFeedDTO>>(response)!;
        }

        public async Task<bool> LikeWishlistAsync(string id, string currentUserId)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("wishlist.like", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<bool> UnlikeWishlistAsync(string id, string currentUserId)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("wishlist.unlike", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<CommentDTO> AddCommentAsync(string id, string currentUserId, CreateCommentDTO commentDto)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, CurrentUserId = currentUserId, CommentDto = commentDto });
            var response = await SendRpcAsync("wishlist.comment", payload);
            return JsonSerializer.Deserialize<CommentDTO>(response)!;
        }

        public async Task<CommentDTO> UpdateCommentAsync(string id, string currentUserId, UpdateCommentDTO commentDto)
        {
            var payload = JsonSerializer.Serialize(new { CommentId = id, CurrentUserId = currentUserId, CommentDto = commentDto });
            var response = await SendRpcAsync("wishlist.updateComment", payload);
            return JsonSerializer.Deserialize<CommentDTO>(response)!;
        }

        public async Task<bool> DeleteCommentAsync(string id, string currentUserId)
        {
            var payload = JsonSerializer.Serialize(new { CommentId = id, CurrentUserId = currentUserId });
            var response = await SendRpcAsync("wishlist.deleteComment", payload);
            return JsonSerializer.Deserialize<bool>(response);
        }

        public async Task<List<CommentDTO>> GetCommentsAsync(string id, int page = 1, int pageSize = 20)
        {
            var payload = JsonSerializer.Serialize(new { WishlistId = id, Page = page, PageSize = pageSize });
            var response = await SendRpcAsync("wishlist.getComments", payload);
            return JsonSerializer.Deserialize<List<CommentDTO>>(response)!;
        }

        public async Task<string> UploadItemImageAsync(IFormFile file)
        {
            var payload = JsonSerializer.Serialize(new { File = file });
            var response = await SendRpcAsync("wishlist.uploadImage", payload);
            var result = JsonSerializer.Deserialize<ImageResponseDTO>(response)!;
            return result.ImageUrl;
        }

        public string[] GetCategories()
        {
            // This is a synchronous operation, so we'll return the categories directly
            return WishlistCategories.Categories;
        }

        // Gift operations
        public async Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, string userId, IFormFile? imageFile)
        {
            var payload = JsonSerializer.Serialize(new { Name = name, Price = price, Category = category, WishlistId = wishlistId, UserId = userId, ImageFile = imageFile });
            var response = await SendRpcAsync("gift.create", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<object> UpdateGiftAsync(string id, GiftUpdateDto giftDto)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, Name = giftDto.Name, Price = giftDto.Price, Category = giftDto.Category });
            var response = await SendRpcAsync("gift.update", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<object> DeleteGiftAsync(string id)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, UserId = "" }); // UserId not needed for delete
            var response = await SendRpcAsync("gift.delete", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<object> ReserveGiftAsync(string id, string userId, string username)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, UserId = userId, Username = username });
            var response = await SendRpcAsync("gift.reserve", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<object> CancelReservationAsync(string id, string userId)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, UserId = userId });
            var response = await SendRpcAsync("gift.cancelReserve", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<List<Gift>> GetReservedGiftsAsync(string userId)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = "", UserId = userId }); // GiftId not needed for this operation
            var response = await SendRpcAsync("gift.getReserved", payload);
            return JsonSerializer.Deserialize<List<Gift>>(response)!;
        }

        public async Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy)
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, Category = category, SortBy = sortBy });
            var response = await SendRpcAsync("gift.getUserWishlist", payload);
            return JsonSerializer.Deserialize<List<Gift>>(response)!;
        }

        public async Task<Gift> GetGiftByIdAsync(string id)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, UserId = "" }); // UserId not needed for this operation
            var response = await SendRpcAsync("gift.getById", payload);
            return JsonSerializer.Deserialize<Gift>(response)!;
        }

        public async Task<List<Gift>> GetSharedWishlistAsync(string userId)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = "", UserId = userId }); // GiftId not needed for this operation
            var response = await SendRpcAsync("gift.getShared", payload);
            return JsonSerializer.Deserialize<List<Gift>>(response)!;
        }

        public async Task<string> UploadGiftImageAsync(string id, IFormFile imageFile)
        {
            var payload = JsonSerializer.Serialize(new { File = imageFile });
            var response = await SendRpcAsync("gift.uploadImage", payload);
            var result = JsonSerializer.Deserialize<ImageResponseDTO>(response)!;
            return result.ImageUrl;
        }

        public async Task<object> AssignGiftToWishlistAsync(string id, string wishlistId, string userId)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, WishlistId = wishlistId });
            var response = await SendRpcAsync("gift.assignToWishlist", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        public async Task<object> RemoveGiftFromWishlistAsync(string id, string userId)
        {
            var payload = JsonSerializer.Serialize(new { GiftId = id, UserId = userId });
            var response = await SendRpcAsync("gift.removeFromWishlist", payload);
            return JsonSerializer.Deserialize<object>(response)!;
        }

        private async Task<string> SendRpcAsync(string routingKey, string payload)
        {
            // Check if RabbitMQ is available
            if (_channel == null || _connection == null || !_connection.IsOpen)
            {
                throw new InvalidOperationException($"RabbitMQ is not available. The gift-wishlist-service may not be running or RabbitMQ is not configured correctly. RPC call to '{routingKey}' cannot be executed.");
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
                        tcs.TrySetException(new TimeoutException($"RPC call to '{routingKey}' timed out after 30 seconds. The gift-wishlist-service may not be running or RabbitMQ is not configured correctly."));
                    }
                });

                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"RPC call to '{routingKey}' timed out after 30 seconds. The gift-wishlist-service may not be running or RabbitMQ is not configured correctly.");
            }
            catch (Exception ex) when (_channel == null || _connection == null || !_connection.IsOpen)
            {
                throw new InvalidOperationException($"RabbitMQ connection lost. RPC call to '{routingKey}' cannot be executed.", ex);
            }
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
                Console.WriteLine($"[GiftWishlistServiceClient] Error disposing channel: {ex.Message}");
                _logger?.LogWarning(ex, "Error disposing RabbitMQ channel");
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
                Console.WriteLine($"[GiftWishlistServiceClient] Error disposing connection: {ex.Message}");
                _logger?.LogWarning(ex, "Error disposing RabbitMQ connection");
            }
        }
    }

    public class ImageResponseDTO
    {
        public string ImageUrl { get; set; } = string.Empty;
    }
}
