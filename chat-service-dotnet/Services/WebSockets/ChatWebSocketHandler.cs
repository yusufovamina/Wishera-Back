using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChatService.Api.Hubs;

namespace ChatService.Api.WebSockets
{
    public static class ChatWebSocketHandler
    {
        private static readonly ConcurrentDictionary<string, HashSet<WebSocket>> _userSockets = new();

        private record Envelope(string type, JsonElement? payload);

        public static async Task HandleAsync(HttpContext context, WebSocket socket, IChatStore store)
        {
            string? currentUserId = null;
            try
            {
                var buffer = new byte[64 * 1024];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer), cancellationToken: CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var env = JsonSerializer.Deserialize<Envelope>(message);
                    if (env == null) continue;
                    switch (env.type)
                    {
                        case "register":
                        {
                            var userId = env.payload?.GetProperty("userId").GetString();
                            if (string.IsNullOrWhiteSpace(userId)) break;
                            currentUserId = userId!;
                            var set = _userSockets.GetOrAdd(userId!, _ => new HashSet<WebSocket>());
                            lock (set) set.Add(socket);
                            await BroadcastAll(new { type = "presenceChanged", payload = new { userId, isOnline = true } });
                            break;
                        }
                        case "joinDirect":
                        {
                            // no-op: membership is implicit; client tracks conversationId locally
                            break;
                        }
                        case "sendDirect":
                        {
                            var fromUserId = env.payload?.GetProperty("fromUserId").GetString() ?? "";
                            var toUserId = env.payload?.GetProperty("toUserId").GetString() ?? "";
                            var text = env.payload?.GetProperty("text").GetString() ?? "";
                            var clientMessageId = env.payload?.TryGetProperty("clientMessageId", out var cmid) == true ? cmid.GetString() : null;
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(fromUserId, toUserId);
                            var msg = new ChatMessage
                            {
                                ConversationId = conversationId,
                                SenderUserId = fromUserId,
                                RecipientUserId = toUserId,
                                Text = text,
                                ClientMessageId = clientMessageId,
                                DeliveredAt = DateTimeOffset.UtcNow
                            };
                            await store.AppendAsync(msg);
                            await BroadcastToConversation(conversationId, new
                            {
                                type = "messageReceived",
                                payload = new
                                {
                                    id = msg.Id,
                                    clientMessageId = msg.ClientMessageId,
                                    conversationId = msg.ConversationId,
                                    senderUserId = msg.SenderUserId,
                                    recipientUserId = msg.RecipientUserId,
                                    text = msg.Text,
                                    sentAt = msg.SentAt,
                                    deliveredAt = msg.DeliveredAt,
                                    readAt = msg.ReadAt
                                }
                            });
                            break;
                        }
                        case "typing":
                        {
                            var userId = env.payload?.GetProperty("userId").GetString() ?? "";
                            var otherUserId = env.payload?.GetProperty("otherUserId").GetString() ?? "";
                            var isTyping = env.payload?.GetProperty("isTyping").GetBoolean() ?? false;
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userId, otherUserId);
                            await BroadcastToConversation(conversationId, new { type = "typing", payload = new { userId, conversationId, isTyping } });
                            break;
                        }
                        case "delivered":
                        {
                            var userId = env.payload?.GetProperty("userId").GetString() ?? "";
                            var otherUserId = env.payload?.GetProperty("otherUserId").GetString() ?? "";
                            var ids = env.payload?.GetProperty("messageIds").EnumerateArray().Select(x => x.GetString()!).ToArray() ?? Array.Empty<string>();
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userId, otherUserId);
                            await store.MarkDeliveredAsync(conversationId, userId, ids);
                            await BroadcastToConversation(conversationId, new { type = "deliveredReceipts", payload = new { userId, conversationId, messageIds = ids, deliveredAt = DateTimeOffset.UtcNow } });
                            break;
                        }
                        case "read":
                        {
                            var userId = env.payload?.GetProperty("userId").GetString() ?? "";
                            var otherUserId = env.payload?.GetProperty("otherUserId").GetString() ?? "";
                            var ids = env.payload?.GetProperty("messageIds").EnumerateArray().Select(x => x.GetString()!).ToArray() ?? Array.Empty<string>();
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userId, otherUserId);
                            await store.MarkReadAsync(conversationId, userId, ids);
                            await BroadcastToConversation(conversationId, new { type = "readReceipts", payload = new { userId, conversationId, messageIds = ids, readAt = DateTimeOffset.UtcNow } });
                            break;
                        }
                        case "history":
                        {
                            var userA = env.payload?.GetProperty("userA").GetString() ?? "";
                            var userB = env.payload?.GetProperty("userB").GetString() ?? "";
                            var page = env.payload?.TryGetProperty("page", out var p) == true ? p.GetInt32() : 0;
                            var pageSize = env.payload?.TryGetProperty("pageSize", out var ps) == true ? ps.GetInt32() : 20;
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userA, userB);
                            var items = await store.GetHistoryAsync(conversationId, page, pageSize);
                            var payload = new { items };
                            await Send(socket, new { type = "historyResult", payload });
                            break;
                        }
                        case "edit":
                        {
                            var userA = env.payload?.GetProperty("userA").GetString() ?? "";
                            var userB = env.payload?.GetProperty("userB").GetString() ?? "";
                            var messageId = env.payload?.GetProperty("messageId").GetString() ?? "";
                            var newText = env.payload?.GetProperty("newText").GetString() ?? "";
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userA, userB);
                            var ok = await store.EditMessageAsync(conversationId, messageId, newText);
                            if (ok)
                            {
                                await BroadcastToConversation(conversationId, new { type = "messageEdited", payload = new { messageId, conversationId, newText } });
                            }
                            break;
                        }
                        case "delete":
                        {
                            var userA = env.payload?.GetProperty("userA").GetString() ?? "";
                            var userB = env.payload?.GetProperty("userB").GetString() ?? "";
                            var messageId = env.payload?.GetProperty("messageId").GetString() ?? "";
                            var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userA, userB);
                            var ok = await store.DeleteMessageAsync(conversationId, messageId);
                            if (ok)
                            {
                                await BroadcastToConversation(conversationId, new { type = "messageDeleted", payload = new { messageId, conversationId } });
                            }
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    if (_userSockets.TryGetValue(currentUserId, out var set))
                    {
                        lock (set) set.Remove(socket);
                        if (set.Count == 0)
                        {
                            _userSockets.TryRemove(currentUserId, out _);
                            await BroadcastAll(new { type = "presenceChanged", payload = new { userId = currentUserId, isOnline = false } });
                        }
                    }
                }
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            }
        }

        private static async Task Send(WebSocket socket, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task BroadcastAll(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            foreach (var set in _userSockets.Values)
            {
                WebSocket[] sockets;
                lock (set) sockets = set.ToArray();
                foreach (var s in sockets)
                {
                    if (s.State == WebSocketState.Open)
                    {
                        await s.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }

        private static async Task BroadcastToConversation(string conversationId, object payload)
        {
            // conversation participants derived from id: dm:a:b
            var parts = conversationId.Split(':');
            if (parts.Length != 3) return;
            var a = parts[1];
            var b = parts[2];
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            foreach (var userId in new[] { a, b })
            {
                if (_userSockets.TryGetValue(userId, out var set))
                {
                    WebSocket[] sockets;
                    lock (set) sockets = set.ToArray();
                    foreach (var s in sockets)
                    {
                        if (s.State == WebSocketState.Open)
                        {
                            await s.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
        }
    }
}



