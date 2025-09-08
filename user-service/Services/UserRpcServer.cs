using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WishlistApp.DTO;

namespace user_service.Services
{
    public class UserRpcServer : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
<<<<<<< HEAD
        private readonly IServiceScopeFactory _scopeFactory;
=======
        private readonly WishlistApp.Services.IUserService _userService;
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
        private IConnection? _connection;
        private IModel? _channel;
        private string _exchange = "user.exchange";
        private string _queue = "user.requests";
<<<<<<< HEAD
        private CancellationTokenSource? _cts;
        private Task? _runnerTask;

        public UserRpcServer(IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
=======

        public UserRpcServer(IConfiguration configuration, WishlistApp.Services.IUserService userService)
        {
            _configuration = configuration;
            _userService = userService;
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
<<<<<<< HEAD
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;

            _runnerTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var factory = new ConnectionFactory
                        {
                            HostName = _configuration["RabbitMq:HostName"] ?? "localhost",
                            UserName = _configuration["RabbitMq:UserName"] ?? "guest",
                            Password = _configuration["RabbitMq:Password"] ?? "guest",
                            VirtualHost = _configuration["RabbitMq:VirtualHost"] ?? "/",
                            Port = int.TryParse(_configuration["RabbitMq:Port"], out var port) ? port : 5672
                        };
                        _exchange = _configuration["RabbitMq:Exchange"] ?? _exchange;
                        _queue = _configuration["RabbitMq:Queue"] ?? _queue;

                        _connection = factory.CreateConnection();
                        _channel = _connection.CreateModel();
                        _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
                        _channel.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.profile");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.updateProfile");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.avatar");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.follow");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.unfollow");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.search");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.followers");
                        _channel.QueueBind(_queue, _exchange, routingKey: "user.following");

                        var consumer = new AsyncEventingBasicConsumer(_channel);
                        consumer.Received += OnReceivedAsync;
                        _channel.BasicConsume(queue: _queue, autoAck: false, consumer: consumer);
                        break; // connected successfully
                    }
                    catch
                    {
                        try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch { }
                    }
                }
            }, token);
=======
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
            _channel.QueueBind(_queue, _exchange, routingKey: "user.profile");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.updateProfile");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.avatar");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.follow");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.unfollow");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.search");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.followers");
            _channel.QueueBind(_queue, _exchange, routingKey: "user.following");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnReceivedAsync;
            _channel.BasicConsume(queue: _queue, autoAck: false, consumer: consumer);
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684

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
<<<<<<< HEAD
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<user_service.Services.IUserService>();

=======
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
            switch (routingKey)
            {
                case "user.profile":
                    var profileData = JsonSerializer.Deserialize<ProfileRequestDTO>(payload)!;
<<<<<<< HEAD
                    var profile = await userService.GetUserProfileAsync(profileData.UserId, profileData.CurrentUserId);
                    return JsonSerializer.Serialize(profile);
                case "user.updateProfile":
                    var updateData = JsonSerializer.Deserialize<UpdateProfileRequestDTO>(payload)!;
                    var updatedProfile = await userService.UpdateUserProfileAsync(updateData.UserId, updateData.UpdateDto);
                    return JsonSerializer.Serialize(updatedProfile);
                case "user.avatar":
                    var avatarData = JsonSerializer.Deserialize<AvatarRequestDTO>(payload)!;
                    var avatarUrl = await userService.UpdateAvatarAsync(avatarData.UserId, avatarData.File);
                    return JsonSerializer.Serialize(new { avatarUrl });
                case "user.follow":
                    var followData = JsonSerializer.Deserialize<FollowRequestDTO>(payload)!;
                    var followResult = await userService.FollowUserAsync(followData.FollowerId, followData.FollowingId);
                    return JsonSerializer.Serialize(followResult);
                case "user.unfollow":
                    var unfollowData = JsonSerializer.Deserialize<FollowRequestDTO>(payload)!;
                    var unfollowResult = await userService.UnfollowUserAsync(unfollowData.FollowerId, unfollowData.FollowingId);
                    return JsonSerializer.Serialize(unfollowResult);
                case "user.search":
                    var searchData = JsonSerializer.Deserialize<SearchRequestDTO>(payload)!;
                    var searchResult = await userService.SearchUsersAsync(searchData.Query, searchData.CurrentUserId, searchData.Page, searchData.PageSize);
                    return JsonSerializer.Serialize(searchResult);
                case "user.followers":
                    var followersData = JsonSerializer.Deserialize<ProfileRequestDTO>(payload)!;
                    var followers = await userService.GetFollowersAsync(followersData.UserId, followersData.CurrentUserId, 1, 20);
                    return JsonSerializer.Serialize(followers);
                case "user.following":
                    var followingData = JsonSerializer.Deserialize<ProfileRequestDTO>(payload)!;
                    var following = await userService.GetFollowingAsync(followingData.UserId, followingData.CurrentUserId, 1, 20);
=======
                    var profile = await _userService.GetUserProfileAsync(profileData.UserId, profileData.CurrentUserId);
                    return JsonSerializer.Serialize(profile);
                case "user.updateProfile":
                    var updateData = JsonSerializer.Deserialize<UpdateProfileRequestDTO>(payload)!;
                    var updatedProfile = await _userService.UpdateUserProfileAsync(updateData.UserId, updateData.UpdateDto);
                    return JsonSerializer.Serialize(updatedProfile);
                case "user.avatar":
                    var avatarData = JsonSerializer.Deserialize<AvatarRequestDTO>(payload)!;
                    var avatarUrl = await _userService.UpdateAvatarAsync(avatarData.UserId, avatarData.File);
                    return JsonSerializer.Serialize(new { avatarUrl });
                case "user.follow":
                    var followData = JsonSerializer.Deserialize<FollowRequestDTO>(payload)!;
                    var followResult = await _userService.FollowUserAsync(followData.FollowerId, followData.FollowingId);
                    return JsonSerializer.Serialize(followResult);
                case "user.unfollow":
                    var unfollowData = JsonSerializer.Deserialize<FollowRequestDTO>(payload)!;
                    var unfollowResult = await _userService.UnfollowUserAsync(unfollowData.FollowerId, unfollowData.FollowingId);
                    return JsonSerializer.Serialize(unfollowResult);
                case "user.search":
                    var searchData = JsonSerializer.Deserialize<SearchRequestDTO>(payload)!;
                    var searchResult = await _userService.SearchUsersAsync(searchData.Query, searchData.CurrentUserId, searchData.Page, searchData.PageSize);
                    return JsonSerializer.Serialize(searchResult);
                case "user.followers":
                    var followersData = JsonSerializer.Deserialize<ProfileRequestDTO>(payload)!;
                    var followers = await _userService.GetFollowersAsync(followersData.UserId, followersData.CurrentUserId, 1, 20);
                    return JsonSerializer.Serialize(followers);
                case "user.following":
                    var followingData = JsonSerializer.Deserialize<ProfileRequestDTO>(payload)!;
                    var following = await _userService.GetFollowingAsync(followingData.UserId, followingData.CurrentUserId, 1, 20);
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
                    return JsonSerializer.Serialize(following);
                default:
                    throw new InvalidOperationException($"Unknown routing key: {routingKey}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
<<<<<<< HEAD
            try { _cts?.Cancel(); } catch { }
=======
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }

    // DTOs for RPC communication
    public class ProfileRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class UpdateProfileRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public UpdateUserProfileDTO UpdateDto { get; set; } = new();
    }

    public class AvatarRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public IFormFile File { get; set; } = null!;
    }

    public class FollowRequestDTO
    {
        public string FollowerId { get; set; } = string.Empty;
        public string FollowingId { get; set; } = string.Empty;
    }

    public class SearchRequestDTO
    {
        public string Query { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
