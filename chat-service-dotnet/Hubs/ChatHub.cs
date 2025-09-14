using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ChatService.Api.Hubs
{
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; } = string.Empty;
        public string SenderUserId { get; set; } = string.Empty;
        public string RecipientUserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeliveredAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
        public string? ClientMessageId { get; set; }
    }

    public interface IChatStore
    {
        Task AppendAsync(ChatMessage message);
        Task<IEnumerable<ChatMessage>> GetHistoryAsync(string conversationId, int page, int pageSize);
        Task<bool> EditMessageAsync(string conversationId, string messageId, string newText);
        Task<bool> DeleteMessageAsync(string conversationId, string messageId);
        Task<int> MarkReadAsync(string conversationId, string userId, IEnumerable<string> messageIds);
        Task<IEnumerable<ChatMessage>> SearchAsync(string conversationId, string? queryText, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize);
    }

    public class InMemoryChatStore : IChatStore
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _messagesByConversation = new();

        public Task AppendAsync(ChatMessage message)
        {
            var list = _messagesByConversation.GetOrAdd(message.ConversationId, _ => new List<ChatMessage>());
            lock (list)
            {
                list.Add(message);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ChatMessage>> GetHistoryAsync(string conversationId, int page, int pageSize)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult(Enumerable.Empty<ChatMessage>());
            }
            IEnumerable<ChatMessage> pageItems;
            lock (list)
            {
                var ordered = list.OrderBy(m => m.SentAt).ToList();
                var skip = Math.Max(0, page) * Math.Max(1, pageSize);
                pageItems = ordered.Skip(skip).Take(pageSize).ToList();
            }
            return Task.FromResult(pageItems);
        }

        public Task<bool> EditMessageAsync(string conversationId, string messageId, string newText)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult(false);
            }
            lock (list)
            {
                var msg = list.FirstOrDefault(m => m.Id == messageId);
                if (msg == null) return Task.FromResult(false);
                msg.Text = newText;
                return Task.FromResult(true);
            }
        }

        public Task<bool> DeleteMessageAsync(string conversationId, string messageId)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult(false);
            }
            lock (list)
            {
                var removed = list.RemoveAll(m => m.Id == messageId) > 0;
                return Task.FromResult(removed);
            }
        }

        public Task<int> MarkReadAsync(string conversationId, string userId, IEnumerable<string> messageIds)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult(0);
            }
            int count = 0;
            lock (list)
            {
                foreach (var id in messageIds)
                {
                    var msg = list.FirstOrDefault(m => m.Id == id);
                    if (msg != null && msg.RecipientUserId == userId)
                    {
                        if (!msg.ReadAt.HasValue)
                        {
                            msg.ReadAt = DateTimeOffset.UtcNow;
                            count++;
                        }
                    }
                }
            }
            return Task.FromResult(count);
        }

        public Task<IEnumerable<ChatMessage>> SearchAsync(string conversationId, string? queryText, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult(Enumerable.Empty<ChatMessage>());
            }
            IEnumerable<ChatMessage> result;
            lock (list)
            {
                result = list.AsEnumerable();
                if (from.HasValue) result = result.Where(m => m.SentAt >= from.Value);
                if (to.HasValue) result = result.Where(m => m.SentAt <= to.Value);
                if (!string.IsNullOrWhiteSpace(queryText))
                {
                    result = result.Where(m => m.Text.Contains(queryText, StringComparison.OrdinalIgnoreCase));
                }
                result = result.OrderBy(m => m.SentAt)
                               .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
                               .Take(Math.Max(1, pageSize))
                               .ToList();
            }
            return Task.FromResult(result);
        }
    }

    public class ChatHub : Hub
    {
        private readonly IChatStore _store;

        public ChatHub(IChatStore store)
        {
            _store = store;
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public static string GetConversationId(string userA, string userB)
        {
            var pair = new[] { userA, userB }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            return $"dm:{pair[0]}:{pair[1]}";
        }

        public async Task JoinDirect(string currentUserId, string otherUserId)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        }

        public async Task SendDirectMessage(string fromUserId, string toUserId, string text, string? clientMessageId)
        {
            var conversationId = GetConversationId(fromUserId, toUserId);
            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderUserId = fromUserId,
                RecipientUserId = toUserId,
                Text = text,
                ClientMessageId = clientMessageId,
                DeliveredAt = DateTimeOffset.UtcNow
            };

            await _store.AppendAsync(message);

            await Clients.Group(conversationId).SendAsync("messageReceived", new
            {
                id = message.Id,
                clientMessageId = message.ClientMessageId,
                conversationId = message.ConversationId,
                senderUserId = message.SenderUserId,
                recipientUserId = message.RecipientUserId,
                text = message.Text,
                sentAt = message.SentAt,
                deliveredAt = message.DeliveredAt,
                readAt = message.ReadAt
            });
        }

        public async Task SetTyping(string currentUserId, string otherUserId, bool isTyping)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            await Clients.Group(conversationId).SendAsync("typing", new
            {
                userId = currentUserId,
                conversationId,
                isTyping
            });
        }

        public async Task<int> MarkRead(string currentUserId, string otherUserId, IEnumerable<string> messageIds)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            var count = await _store.MarkReadAsync(conversationId, currentUserId, messageIds);
            await Clients.Group(conversationId).SendAsync("readReceipts", new
            {
                userId = currentUserId,
                conversationId,
                messageIds = messageIds.ToArray(),
                readAt = DateTimeOffset.UtcNow
            });
            return count;
        }

        public async Task<IEnumerable<ChatMessage>> LoadHistory(string currentUserId, string otherUserId, int page, int pageSize)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            return await _store.GetHistoryAsync(conversationId, page, pageSize);
        }

        public async Task<bool> EditMessage(string currentUserId, string otherUserId, string messageId, string newText)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            var success = await _store.EditMessageAsync(conversationId, messageId, newText);
            if (success)
            {
                await Clients.Group(conversationId).SendAsync("messageEdited", new
                {
                    messageId,
                    conversationId,
                    newText
                });
            }
            return success;
        }

        public async Task<bool> DeleteMessage(string currentUserId, string otherUserId, string messageId)
        {
            var conversationId = GetConversationId(currentUserId, otherUserId);
            var success = await _store.DeleteMessageAsync(conversationId, messageId);
            if (success)
            {
                await Clients.Group(conversationId).SendAsync("messageDeleted", new
                {
                    messageId,
                    conversationId
                });
            }
            return success;
        }
    }
}


