using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WishlistApp.DTO;
using WishlistApp.Models;
using MongoDB.Driver;

namespace gift_wishlist_service.Services
{
    public class GiftWishlistRpcServer : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly WishlistApp.Services.IWishlistService _wishlistService;
        private readonly gift_wishlist_service.Services.ICloudinaryService _cloudinaryService;
        private readonly gift_wishlist_service.Services.MongoDbContext _dbContext;
        private IConnection? _connection;
        private IModel? _channel;
<<<<<<< HEAD
        private CancellationTokenSource? _cts;
        private Task? _runnerTask;
=======
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
        private string _exchange = "giftwishlist.exchange";
        private string _queue = "giftwishlist.requests";

        public GiftWishlistRpcServer(
            IConfiguration configuration, 
            WishlistApp.Services.IWishlistService wishlistService,
            gift_wishlist_service.Services.ICloudinaryService cloudinaryService,
            gift_wishlist_service.Services.MongoDbContext dbContext)
        {
            _configuration = configuration;
            _wishlistService = wishlistService;
            _cloudinaryService = cloudinaryService;
            _dbContext = dbContext;
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

                        // Bindings
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.create");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.get");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.update");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.delete");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.userWishlists");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.feed");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.like");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.unlike");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.comment");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.updateComment");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.deleteComment");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.getComments");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.uploadImage");
                        _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.categories");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.create");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.update");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.delete");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.reserve");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.cancelReserve");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.getReserved");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.getUserWishlist");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.getById");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.getShared");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.uploadImage");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.assignToWishlist");
                        _channel.QueueBind(_queue, _exchange, routingKey: "gift.removeFromWishlist");

                        var consumer = new AsyncEventingBasicConsumer(_channel);
                        consumer.Received += OnReceivedAsync;
                        _channel.BasicConsume(queue: _queue, autoAck: false, consumer: consumer);
                        // Connected, exit retry loop
                        break;
                    }
                    catch
                    {
                        // Retry after delay without crashing host
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
            
            // Wishlist operations
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.create");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.get");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.update");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.delete");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.userWishlists");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.feed");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.like");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.unlike");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.comment");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.updateComment");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.deleteComment");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.getComments");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.uploadImage");
            _channel.QueueBind(_queue, _exchange, routingKey: "wishlist.categories");
            
            // Gift operations
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.create");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.update");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.delete");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.reserve");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.cancelReserve");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.getReserved");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.getUserWishlist");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.getById");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.getShared");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.uploadImage");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.assignToWishlist");
            _channel.QueueBind(_queue, _exchange, routingKey: "gift.removeFromWishlist");

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
            switch (routingKey)
            {
                // Wishlist operations
                case "wishlist.create":
                    var createData = JsonSerializer.Deserialize<CreateWishlistRequestDTO>(payload)!;
                    var createdWishlist = await _wishlistService.CreateWishlistAsync(createData.UserId, createData.CreateDto);
                    return JsonSerializer.Serialize(createdWishlist);
                case "wishlist.get":
                    var getData = JsonSerializer.Deserialize<WishlistRequestDTO>(payload)!;
                    var wishlist = await _wishlistService.GetWishlistAsync(getData.WishlistId, getData.CurrentUserId);
                    return JsonSerializer.Serialize(wishlist);
                case "wishlist.update":
                    var updateData = JsonSerializer.Deserialize<UpdateWishlistRequestDTO>(payload)!;
                    var updatedWishlist = await _wishlistService.UpdateWishlistAsync(updateData.WishlistId, updateData.CurrentUserId, updateData.UpdateDto);
                    return JsonSerializer.Serialize(updatedWishlist);
                case "wishlist.delete":
                    var deleteData = JsonSerializer.Deserialize<WishlistRequestDTO>(payload)!;
                    var deleteResult = await _wishlistService.DeleteWishlistAsync(deleteData.WishlistId, deleteData.CurrentUserId);
                    return JsonSerializer.Serialize(deleteResult);
                case "wishlist.userWishlists":
                    var userWishlistsData = JsonSerializer.Deserialize<UserWishlistsRequestDTO>(payload)!;
                    var userWishlists = await _wishlistService.GetUserWishlistsAsync(userWishlistsData.UserId, userWishlistsData.CurrentUserId, userWishlistsData.Page, userWishlistsData.PageSize);
                    return JsonSerializer.Serialize(userWishlists);
                case "wishlist.feed":
                    var feedData = JsonSerializer.Deserialize<FeedRequestDTO>(payload)!;
                    var feed = await _wishlistService.GetFeedAsync(feedData.CurrentUserId, feedData.Page, feedData.PageSize);
                    return JsonSerializer.Serialize(feed);
                case "wishlist.like":
                    var likeData = JsonSerializer.Deserialize<WishlistActionRequestDTO>(payload)!;
                    var likeResult = await _wishlistService.LikeWishlistAsync(likeData.WishlistId, likeData.CurrentUserId);
                    return JsonSerializer.Serialize(likeResult);
                case "wishlist.unlike":
                    var unlikeData = JsonSerializer.Deserialize<WishlistActionRequestDTO>(payload)!;
                    var unlikeResult = await _wishlistService.UnlikeWishlistAsync(unlikeData.WishlistId, unlikeData.CurrentUserId);
                    return JsonSerializer.Serialize(unlikeResult);
                case "wishlist.comment":
                    var commentData = JsonSerializer.Deserialize<CommentRequestDTO>(payload)!;
                    var comment = await _wishlistService.AddCommentAsync(commentData.WishlistId, commentData.CurrentUserId, commentData.CommentDto);
                    return JsonSerializer.Serialize(comment);
                case "wishlist.updateComment":
                    var updateCommentData = JsonSerializer.Deserialize<UpdateCommentRequestDTO>(payload)!;
                    var updatedComment = await _wishlistService.UpdateCommentAsync(updateCommentData.CommentId, updateCommentData.CurrentUserId, updateCommentData.CommentDto);
                    return JsonSerializer.Serialize(updatedComment);
                case "wishlist.deleteComment":
                    var deleteCommentData = JsonSerializer.Deserialize<CommentActionRequestDTO>(payload)!;
                    var deleteCommentResult = await _wishlistService.DeleteCommentAsync(deleteCommentData.CommentId, deleteCommentData.CurrentUserId);
                    return JsonSerializer.Serialize(deleteCommentResult);
                case "wishlist.getComments":
                    var getCommentsData = JsonSerializer.Deserialize<GetCommentsRequestDTO>(payload)!;
                    var comments = await _wishlistService.GetCommentsAsync(getCommentsData.WishlistId, getCommentsData.Page, getCommentsData.PageSize);
                    return JsonSerializer.Serialize(comments);
                case "wishlist.uploadImage":
                    var uploadImageData = JsonSerializer.Deserialize<UploadImageRequestDTO>(payload)!;
                    var imageUrl = await _wishlistService.UploadItemImageAsync(uploadImageData.File);
                    return JsonSerializer.Serialize(new { imageUrl });
                case "wishlist.categories":
                    var categories = WishlistCategories.Categories;
                    return JsonSerializer.Serialize(categories);

                // Gift operations
                case "gift.create":
                    var createGiftData = JsonSerializer.Deserialize<CreateGiftRequestDTO>(payload)!;
                    var gift = new Gift
                    {
                        Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                        Name = createGiftData.Name,
                        Price = createGiftData.Price,
                        Category = createGiftData.Category,
                        WishlistId = createGiftData.WishlistId
                    };
                    if (createGiftData.ImageFile != null)
                    {
                        var uploadedUrl = await _cloudinaryService.UploadImageAsync(createGiftData.ImageFile);
                        gift.ImageUrl = uploadedUrl;
                    }
                    await _dbContext.Gifts.InsertOneAsync(gift);
                    return JsonSerializer.Serialize(new { id = gift.Id, message = "Gift created successfully" });
                case "gift.update":
                    var updateGiftData = JsonSerializer.Deserialize<UpdateGiftRequestDTO>(payload)!;
                    var existingGift = await _dbContext.Gifts.Find(g => g.Id == updateGiftData.GiftId).FirstOrDefaultAsync();
                    if (existingGift == null) throw new KeyNotFoundException("Gift not found");
                    
                    existingGift.Name = updateGiftData.Name ?? existingGift.Name;
                    existingGift.Price = updateGiftData.Price ?? existingGift.Price;
                    existingGift.Category = updateGiftData.Category ?? existingGift.Category;
                    
                    await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == updateGiftData.GiftId, existingGift);
                    return JsonSerializer.Serialize(new { message = "Gift updated successfully" });
                case "gift.delete":
                    var deleteGiftData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    await _dbContext.Gifts.DeleteOneAsync(g => g.Id == deleteGiftData.GiftId);
                    return JsonSerializer.Serialize(new { message = "Gift deleted successfully" });
                case "gift.reserve":
                    var reserveData = JsonSerializer.Deserialize<ReserveGiftRequestDTO>(payload)!;
                    var giftToReserve = await _dbContext.Gifts.Find(g => g.Id == reserveData.GiftId).FirstOrDefaultAsync();
                    if (giftToReserve == null) throw new KeyNotFoundException("Gift not found");
                    if (!string.IsNullOrEmpty(giftToReserve.ReservedByUserId))
                        throw new InvalidOperationException("Gift is already reserved!");
                    
                    giftToReserve.ReservedByUserId = reserveData.UserId;
                    giftToReserve.ReservedByUsername = reserveData.Username;
                    await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == reserveData.GiftId, giftToReserve);
                    return JsonSerializer.Serialize(new { message = "Gift reserved successfully", reservedBy = reserveData.Username });
                case "gift.cancelReserve":
                    var cancelReserveData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    var giftToCancel = await _dbContext.Gifts.Find(g => g.Id == cancelReserveData.GiftId).FirstOrDefaultAsync();
                    if (giftToCancel == null) throw new KeyNotFoundException("Gift not found");
                    if (giftToCancel.ReservedByUserId != cancelReserveData.UserId) 
                        throw new UnauthorizedAccessException("You cannot cancel this reservation");
                    
                    giftToCancel.ReservedByUserId = null;
                    giftToCancel.ReservedByUsername = null;
                    await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == cancelReserveData.GiftId, giftToCancel);
                    return JsonSerializer.Serialize(new { message = "Reservation cancelled successfully" });
                case "gift.getReserved":
                    var getReservedData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    var reservedGifts = await _dbContext.Gifts.Find(g => g.ReservedByUserId == getReservedData.UserId).ToListAsync();
                    return JsonSerializer.Serialize(reservedGifts);
                case "gift.getUserWishlist":
                    var getUserWishlistData = JsonSerializer.Deserialize<GetUserWishlistRequestDTO>(payload)!;
                    var userWishlistsList = await _dbContext.Wishlists.Find(w => w.UserId == getUserWishlistData.UserId).ToListAsync();
                    var wishlistIds = userWishlistsList.Select(w => w.Id).ToList();
                    
                    var filter = MongoDB.Driver.Builders<Gift>.Filter.Or(
                        MongoDB.Driver.Builders<Gift>.Filter.In(g => g.WishlistId, wishlistIds),
                        MongoDB.Driver.Builders<Gift>.Filter.Eq(g => g.WishlistId, (string)null)
                    );
                    
                    if (!string.IsNullOrEmpty(getUserWishlistData.Category))
                    {
                        filter &= MongoDB.Driver.Builders<Gift>.Filter.Regex(g => g.Category, new MongoDB.Bson.BsonRegularExpression(getUserWishlistData.Category, "i"));
                    }
                    
                    var giftsQuery = _dbContext.Gifts.Find(filter);
                    if (!string.IsNullOrEmpty(getUserWishlistData.SortBy))
                    {
                        giftsQuery = getUserWishlistData.SortBy switch
                        {
                            "price-asc" => giftsQuery.SortBy(g => g.Price),
                            "price-desc" => giftsQuery.SortByDescending(g => g.Price),
                            "name-asc" => giftsQuery.SortBy(g => g.Name),
                            "name-desc" => giftsQuery.SortByDescending(g => g.Name),
                            _ => giftsQuery
                        };
                    }
                    
                    var gifts = await giftsQuery.ToListAsync();
                    return JsonSerializer.Serialize(gifts);
                case "gift.getById":
                    var getByIdData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    var giftById = await _dbContext.Gifts.Find(g => g.Id == getByIdData.GiftId).FirstOrDefaultAsync();
                    if (giftById == null) throw new KeyNotFoundException("Gift not found");
                    return JsonSerializer.Serialize(giftById);
                case "gift.getShared":
                    var getSharedData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    var sharedGifts = await _dbContext.Gifts.Find(g => g.WishlistId == getSharedData.UserId).ToListAsync();
                    return JsonSerializer.Serialize(sharedGifts);
                case "gift.uploadImage":
                    var uploadGiftImageData = JsonSerializer.Deserialize<UploadImageRequestDTO>(payload)!;
                    var uploadedUrl2 = await _cloudinaryService.UploadImageAsync(uploadGiftImageData.File);
                    return JsonSerializer.Serialize(new { imageUrl = uploadedUrl2 });
                case "gift.assignToWishlist":
                    var assignData = JsonSerializer.Deserialize<AssignGiftRequestDTO>(payload)!;
                    var giftToAssign = await _dbContext.Gifts.Find(g => g.Id == assignData.GiftId).FirstOrDefaultAsync();
                    if (giftToAssign == null) throw new KeyNotFoundException("Gift not found");
                    
                    giftToAssign.WishlistId = assignData.WishlistId;
                    await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == assignData.GiftId, giftToAssign);
                    return JsonSerializer.Serialize(new { message = "Gift assigned to wishlist successfully" });
                case "gift.removeFromWishlist":
                    var removeData = JsonSerializer.Deserialize<GiftActionRequestDTO>(payload)!;
                    var giftToRemove = await _dbContext.Gifts.Find(g => g.Id == removeData.GiftId).FirstOrDefaultAsync();
                    if (giftToRemove == null) throw new KeyNotFoundException("Gift not found");
                    
                    giftToRemove.WishlistId = null;
                    await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == removeData.GiftId, giftToRemove);
                    return JsonSerializer.Serialize(new { message = "Gift removed from wishlist successfully" });
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
    public class WishlistRequestDTO
    {
        public string WishlistId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class CreateWishlistRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public required CreateWishlistDTO CreateDto { get; set; }
    }

    public class UpdateWishlistRequestDTO
    {
        public string WishlistId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public required UpdateWishlistDTO UpdateDto { get; set; }
    }

    public class UserWishlistsRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class FeedRequestDTO
    {
        public string CurrentUserId { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class WishlistActionRequestDTO
    {
        public string WishlistId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class CommentRequestDTO
    {
        public string WishlistId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public required CreateCommentDTO CommentDto { get; set; }
    }

    public class UpdateCommentRequestDTO
    {
        public string CommentId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public required UpdateCommentDTO CommentDto { get; set; }
    }

    public class CommentActionRequestDTO
    {
        public string CommentId { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class GetCommentsRequestDTO
    {
        public string WishlistId { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class UploadImageRequestDTO
    {
        public IFormFile File { get; set; } = null!;
    }

    public class CreateGiftRequestDTO
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? WishlistId { get; set; }
        public IFormFile? ImageFile { get; set; }
    }

    public class UpdateGiftRequestDTO
    {
        public string GiftId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
    }

    public class GiftActionRequestDTO
    {
        public string GiftId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    public class ReserveGiftRequestDTO
    {
        public string GiftId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class GetUserWishlistRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SortBy { get; set; }
    }

    public class AssignGiftRequestDTO
    {
        public string GiftId { get; set; } = string.Empty;
        public string WishlistId { get; set; } = string.Empty;
    }
}
