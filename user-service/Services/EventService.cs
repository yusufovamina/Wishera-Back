using MongoDB.Driver;
using user_service.Models;
using WisheraApp.DTO;
using WisheraApp.Models;

namespace user_service.Services
{
    public interface IEventService
    {
        Task<EventDTO> CreateEventAsync(string creatorId, CreateEventDTO createEventDto);
        Task<EventDTO?> GetEventByIdAsync(string eventId, string currentUserId);
        Task<EventListDTO> GetUserEventsAsync(string userId, int page = 1, int pageSize = 10);
        Task<EventListDTO> GetInvitedEventsAsync(string userId, int page = 1, int pageSize = 10);
        Task<EventDTO> UpdateEventAsync(string eventId, string userId, UpdateEventDTO updateEventDto);
        Task<bool> CancelEventAsync(string eventId, string userId);
        Task<bool> DeleteEventAsync(string eventId, string userId);
        Task<EventInvitationDTO> RespondToInvitationAsync(string invitationId, string userId, RespondToInvitationDTO responseDto);
        Task<EventInvitationListDTO> GetUserInvitationsAsync(string userId, int page = 1, int pageSize = 10);
        Task<bool> IsUserInvitedToEventAsync(string eventId, string userId);
        Task<List<EventInvitationDTO>> GetEventInvitationsAsync(string eventId, string userId);
    }

    public class EventService : IEventService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICacheService _cache;
        private readonly INotificationService _notificationService;

        public EventService(MongoDbContext dbContext, ICacheService cache, INotificationService notificationService)
        {
            _dbContext = dbContext;
            _cache = cache;
            _notificationService = notificationService;
        }

        private bool IsValidObjectId(string id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

        public async Task<EventDTO> CreateEventAsync(string creatorId, CreateEventDTO createEventDto)
        {
            if (!IsValidObjectId(creatorId)) throw new ArgumentException("Invalid creator ID format.");

            // Validate invitee IDs
            foreach (var inviteeId in createEventDto.InviteeIds)
            {
                if (!IsValidObjectId(inviteeId)) throw new ArgumentException($"Invalid invitee ID format: {inviteeId}");
            }

            // Verify creator exists
            var creator = await _dbContext.Users.Find(u => u.Id == creatorId).FirstOrDefaultAsync();
            if (creator == null) throw new KeyNotFoundException("Creator not found.");

            // Verify all invitees exist and are friends
            var invitees = await _dbContext.Users.Find(u => createEventDto.InviteeIds.Contains(u.Id)).ToListAsync();
            if (invitees.Count != createEventDto.InviteeIds.Count)
                throw new ArgumentException("One or more invitees not found.");

            // Check if invitees are friends
            foreach (var invitee in invitees)
            {
                if (!creator.FollowingIds.Contains(invitee.Id))
                    throw new ArgumentException($"User {invitee.Username} is not in your friends list.");
            }

            // Create event
            var eventEntity = new Event
            {
                Title = createEventDto.Title,
                Description = createEventDto.Description,
                EventDate = createEventDto.EventDate,
                EventTime = createEventDto.EventTime,
                Location = createEventDto.Location,
                AdditionalNotes = createEventDto.AdditionalNotes,
                CreatorId = creatorId,
                InviteeIds = createEventDto.InviteeIds,
                EventType = createEventDto.EventType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Events.InsertOneAsync(eventEntity);

            // Create invitations
            var invitations = createEventDto.InviteeIds.Select(inviteeId => new EventInvitation
            {
                EventId = eventEntity.Id,
                InviteeId = inviteeId,
                InviterId = creatorId,
                Status = InvitationStatus.Pending,
                InvitedAt = DateTime.UtcNow
            }).ToList();

            if (invitations.Any())
            {
                await _dbContext.EventInvitations.InsertManyAsync(invitations);
            }

            // Send notifications to invitees
            foreach (var invitation in invitations)
            {
                await _notificationService.CreateEventInvitationNotificationAsync(
                    invitation.InviteeId, 
                    creatorId, 
                    eventEntity.Id, 
                    eventEntity.Title
                );
            }

            // Clear cache
            await _cache.RemoveAsync($"user:events:{creatorId}");

            return await GetEventByIdAsync(eventEntity.Id, creatorId);
        }

        public async Task<EventDTO?> GetEventByIdAsync(string eventId, string currentUserId)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"event:{eventId}:{currentUserId}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var eventEntity = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
                if (eventEntity == null) return null;

                // Check if user has access to this event
                if (eventEntity.CreatorId != currentUserId && !eventEntity.InviteeIds.Contains(currentUserId))
                    throw new UnauthorizedAccessException("You don't have access to this event.");

                var creator = await _dbContext.Users.Find(u => u.Id == eventEntity.CreatorId).FirstOrDefaultAsync();
                if (creator == null) throw new KeyNotFoundException("Event creator not found.");

                // Get invitation status for current user
                InvitationStatus? userResponse = null;
                if (eventEntity.InviteeIds.Contains(currentUserId))
                {
                    var invitation = await _dbContext.EventInvitations
                        .Find(i => i.EventId == eventId && i.InviteeId == currentUserId)
                        .FirstOrDefaultAsync();
                    userResponse = invitation?.Status;
                }

                // Get invitation counts
                var invitations = await _dbContext.EventInvitations
                    .Find(i => i.EventId == eventId)
                    .ToListAsync();

                return new EventDTO
                {
                    Id = eventEntity.Id,
                    Title = eventEntity.Title,
                    Description = eventEntity.Description,
                    EventDate = eventEntity.EventDate,
                    EventTime = eventEntity.EventTime,
                    Location = eventEntity.Location,
                    AdditionalNotes = eventEntity.AdditionalNotes,
                    CreatorId = eventEntity.CreatorId,
                    CreatorUsername = creator.Username,
                    CreatorAvatarUrl = creator.AvatarUrl ?? "",
                    InviteeIds = eventEntity.InviteeIds,
                    CreatedAt = eventEntity.CreatedAt,
                    UpdatedAt = eventEntity.UpdatedAt,
                    IsCancelled = eventEntity.IsCancelled,
                    EventType = eventEntity.EventType,
                    AcceptedCount = invitations.Count(i => i.Status == InvitationStatus.Accepted),
                    DeclinedCount = invitations.Count(i => i.Status == InvitationStatus.Declined),
                    PendingCount = invitations.Count(i => i.Status == InvitationStatus.Pending),
                    UserResponse = userResponse
                };
            }, TimeSpan.FromMinutes(15));
        }

        public async Task<EventListDTO> GetUserEventsAsync(string userId, int page = 1, int pageSize = 10)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:events:{userId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var skip = (page - 1) * pageSize;
                var events = await _dbContext.Events
                    .Find(e => e.CreatorId == userId && !e.IsCancelled)
                    .Sort(Builders<Event>.Sort.Descending(e => e.EventDate))
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.Events
                    .CountDocumentsAsync(e => e.CreatorId == userId && !e.IsCancelled);

                var eventDtos = new List<EventDTO>();
                foreach (var eventEntity in events)
                {
                    var creator = await _dbContext.Users.Find(u => u.Id == eventEntity.CreatorId).FirstOrDefaultAsync();
                    if (creator == null) continue;

                    var invitations = await _dbContext.EventInvitations
                        .Find(i => i.EventId == eventEntity.Id)
                        .ToListAsync();

                    eventDtos.Add(new EventDTO
                    {
                        Id = eventEntity.Id,
                        Title = eventEntity.Title,
                        Description = eventEntity.Description,
                        EventDate = eventEntity.EventDate,
                        EventTime = eventEntity.EventTime,
                        Location = eventEntity.Location,
                        AdditionalNotes = eventEntity.AdditionalNotes,
                        CreatorId = eventEntity.CreatorId,
                        CreatorUsername = creator.Username,
                        CreatorAvatarUrl = creator.AvatarUrl ?? "",
                        InviteeIds = eventEntity.InviteeIds,
                        CreatedAt = eventEntity.CreatedAt,
                        UpdatedAt = eventEntity.UpdatedAt,
                        IsCancelled = eventEntity.IsCancelled,
                        EventType = eventEntity.EventType,
                        AcceptedCount = invitations.Count(i => i.Status == InvitationStatus.Accepted),
                        DeclinedCount = invitations.Count(i => i.Status == InvitationStatus.Declined),
                        PendingCount = invitations.Count(i => i.Status == InvitationStatus.Pending)
                    });
                }

                return new EventListDTO
                {
                    Events = eventDtos,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }, TimeSpan.FromMinutes(10));
        }

        public async Task<EventListDTO> GetInvitedEventsAsync(string userId, int page = 1, int pageSize = 10)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:invited-events:{userId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var skip = (page - 1) * pageSize;
                var invitations = await _dbContext.EventInvitations
                    .Find(i => i.InviteeId == userId)
                    .Sort(Builders<EventInvitation>.Sort.Descending(i => i.InvitedAt))
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.EventInvitations
                    .CountDocumentsAsync(i => i.InviteeId == userId);

                var eventDtos = new List<EventDTO>();
                foreach (var invitation in invitations)
                {
                    var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
                    if (eventEntity == null || eventEntity.IsCancelled) continue;

                    var creator = await _dbContext.Users.Find(u => u.Id == eventEntity.CreatorId).FirstOrDefaultAsync();
                    if (creator == null) continue;

                    var allInvitations = await _dbContext.EventInvitations
                        .Find(i => i.EventId == eventEntity.Id)
                        .ToListAsync();

                    eventDtos.Add(new EventDTO
                    {
                        Id = eventEntity.Id,
                        Title = eventEntity.Title,
                        Description = eventEntity.Description,
                        EventDate = eventEntity.EventDate,
                        EventTime = eventEntity.EventTime,
                        Location = eventEntity.Location,
                        AdditionalNotes = eventEntity.AdditionalNotes,
                        CreatorId = eventEntity.CreatorId,
                        CreatorUsername = creator.Username,
                        CreatorAvatarUrl = creator.AvatarUrl ?? "",
                        InviteeIds = eventEntity.InviteeIds,
                        CreatedAt = eventEntity.CreatedAt,
                        UpdatedAt = eventEntity.UpdatedAt,
                        IsCancelled = eventEntity.IsCancelled,
                        EventType = eventEntity.EventType,
                        AcceptedCount = allInvitations.Count(i => i.Status == InvitationStatus.Accepted),
                        DeclinedCount = allInvitations.Count(i => i.Status == InvitationStatus.Declined),
                        PendingCount = allInvitations.Count(i => i.Status == InvitationStatus.Pending),
                        UserResponse = invitation.Status
                    });
                }

                return new EventListDTO
                {
                    Events = eventDtos,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }, TimeSpan.FromMinutes(10));
        }

        public async Task<EventDTO> UpdateEventAsync(string eventId, string userId, UpdateEventDTO updateEventDto)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var eventEntity = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventEntity == null) throw new KeyNotFoundException("Event not found.");
            if (eventEntity.CreatorId != userId) throw new UnauthorizedAccessException("You can only update your own events.");

            var updateDefinition = Builders<Event>.Update.Set(e => e.UpdatedAt, DateTime.UtcNow);

            if (updateEventDto.Title != null) updateDefinition = updateDefinition.Set(e => e.Title, updateEventDto.Title);
            if (updateEventDto.Description != null) updateDefinition = updateDefinition.Set(e => e.Description, updateEventDto.Description);
            if (updateEventDto.EventDate.HasValue) updateDefinition = updateDefinition.Set(e => e.EventDate, updateEventDto.EventDate.Value);
            if (updateEventDto.EventTime.HasValue) updateDefinition = updateDefinition.Set(e => e.EventTime, updateEventDto.EventTime.Value);
            if (updateEventDto.Location != null) updateDefinition = updateDefinition.Set(e => e.Location, updateEventDto.Location);
            if (updateEventDto.AdditionalNotes != null) updateDefinition = updateDefinition.Set(e => e.AdditionalNotes, updateEventDto.AdditionalNotes);
            if (updateEventDto.EventType != null) updateDefinition = updateDefinition.Set(e => e.EventType, updateEventDto.EventType);

            await _dbContext.Events.UpdateOneAsync(e => e.Id == eventId, updateDefinition);

            // Clear cache
            await _cache.RemoveAsync($"event:{eventId}:{userId}");
            await _cache.RemoveAsync($"user:events:{userId}");

            return await GetEventByIdAsync(eventId, userId);
        }

        public async Task<bool> CancelEventAsync(string eventId, string userId)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var eventEntity = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventEntity == null) throw new KeyNotFoundException("Event not found.");
            if (eventEntity.CreatorId != userId) throw new UnauthorizedAccessException("You can only cancel your own events.");

            var updateDefinition = Builders<Event>.Update
                .Set(e => e.IsCancelled, true)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Events.UpdateOneAsync(e => e.Id == eventId, updateDefinition);

            // Send cancellation notifications to invitees
            foreach (var inviteeId in eventEntity.InviteeIds)
            {
                await _notificationService.CreateEventCancellationNotificationAsync(
                    inviteeId, 
                    userId, 
                    eventId, 
                    eventEntity.Title
                );
            }

            // Clear cache
            await _cache.RemoveAsync($"event:{eventId}:{userId}");
            await _cache.RemoveAsync($"user:events:{userId}");

            return true;
        }

        public async Task<bool> DeleteEventAsync(string eventId, string userId)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var eventEntity = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventEntity == null) throw new KeyNotFoundException("Event not found.");
            if (eventEntity.CreatorId != userId) throw new UnauthorizedAccessException("You can only delete your own events.");

            // Delete event and all related invitations
            await _dbContext.Events.DeleteOneAsync(e => e.Id == eventId);
            await _dbContext.EventInvitations.DeleteManyAsync(i => i.EventId == eventId);

            // Clear cache
            await _cache.RemoveAsync($"event:{eventId}:{userId}");
            await _cache.RemoveAsync($"user:events:{userId}");

            return true;
        }

        public async Task<EventInvitationDTO> RespondToInvitationAsync(string invitationId, string userId, RespondToInvitationDTO responseDto)
        {
            if (!IsValidObjectId(invitationId)) throw new ArgumentException("Invalid invitation ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var invitation = await _dbContext.EventInvitations.Find(i => i.Id == invitationId).FirstOrDefaultAsync();
            if (invitation == null) throw new KeyNotFoundException("Invitation not found.");
            if (invitation.InviteeId != userId) throw new UnauthorizedAccessException("You can only respond to your own invitations.");

            var updateDefinition = Builders<EventInvitation>.Update
                .Set(i => i.Status, responseDto.Status)
                .Set(i => i.RespondedAt, DateTime.UtcNow)
                .Set(i => i.ResponseMessage, responseDto.ResponseMessage);

            await _dbContext.EventInvitations.UpdateOneAsync(i => i.Id == invitationId, updateDefinition);

            // Get event and inviter details for response
            var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
            var inviter = await _dbContext.Users.Find(u => u.Id == invitation.InviterId).FirstOrDefaultAsync();

            if (eventEntity != null && inviter != null)
            {
                // Send response notification to event creator
                await _notificationService.CreateEventResponseNotificationAsync(
                    invitation.InviterId,
                    userId,
                    invitation.EventId,
                    eventEntity.Title,
                    responseDto.Status,
                    responseDto.ResponseMessage
                );
            }

            // Clear cache
            await _cache.RemoveAsync($"user:invited-events:{userId}");

            return new EventInvitationDTO
            {
                Id = invitation.Id,
                EventId = invitation.EventId,
                InviteeId = invitation.InviteeId,
                InviterId = invitation.InviterId,
                Status = responseDto.Status,
                InvitedAt = invitation.InvitedAt,
                RespondedAt = DateTime.UtcNow,
                ResponseMessage = responseDto.ResponseMessage,
                InviterUsername = inviter?.Username ?? "",
                InviterAvatarUrl = inviter?.AvatarUrl ?? ""
            };
        }

        public async Task<EventInvitationListDTO> GetUserInvitationsAsync(string userId, int page = 1, int pageSize = 10)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:invitations:{userId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var skip = (page - 1) * pageSize;
                var invitations = await _dbContext.EventInvitations
                    .Find(i => i.InviteeId == userId)
                    .Sort(Builders<EventInvitation>.Sort.Descending(i => i.InvitedAt))
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.EventInvitations
                    .CountDocumentsAsync(i => i.InviteeId == userId);

                var invitationDtos = new List<EventInvitationDTO>();
                foreach (var invitation in invitations)
                {
                    var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
                    var inviter = await _dbContext.Users.Find(u => u.Id == invitation.InviterId).FirstOrDefaultAsync();

                    if (eventEntity != null && inviter != null)
                    {
                        invitationDtos.Add(new EventInvitationDTO
                        {
                            Id = invitation.Id,
                            EventId = invitation.EventId,
                            InviteeId = invitation.InviteeId,
                            InviterId = invitation.InviterId,
                            Status = invitation.Status,
                            InvitedAt = invitation.InvitedAt,
                            RespondedAt = invitation.RespondedAt,
                            ResponseMessage = invitation.ResponseMessage,
                            InviterUsername = inviter.Username,
                            InviterAvatarUrl = inviter.AvatarUrl ?? "",
                            Event = new EventDTO
                            {
                                Id = eventEntity.Id,
                                Title = eventEntity.Title,
                                Description = eventEntity.Description,
                                EventDate = eventEntity.EventDate,
                                EventTime = eventEntity.EventTime,
                                Location = eventEntity.Location,
                                AdditionalNotes = eventEntity.AdditionalNotes,
                                CreatorId = eventEntity.CreatorId,
                                CreatorUsername = inviter.Username,
                                CreatorAvatarUrl = inviter.AvatarUrl ?? "",
                                EventType = eventEntity.EventType,
                                IsCancelled = eventEntity.IsCancelled
                            }
                        });
                    }
                }

                return new EventInvitationListDTO
                {
                    Invitations = invitationDtos,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }, TimeSpan.FromMinutes(10));
        }

        public async Task<bool> IsUserInvitedToEventAsync(string eventId, string userId)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var invitation = await _dbContext.EventInvitations
                .Find(i => i.EventId == eventId && i.InviteeId == userId)
                .FirstOrDefaultAsync();

            return invitation != null;
        }

        public async Task<List<EventInvitationDTO>> GetEventInvitationsAsync(string eventId, string userId)
        {
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var eventEntity = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventEntity == null) throw new KeyNotFoundException("Event not found.");
            if (eventEntity.CreatorId != userId) throw new UnauthorizedAccessException("You can only view invitations for your own events.");

            var invitations = await _dbContext.EventInvitations
                .Find(i => i.EventId == eventId)
                .ToListAsync();

            var invitationDtos = new List<EventInvitationDTO>();
            foreach (var invitation in invitations)
            {
                var invitee = await _dbContext.Users.Find(u => u.Id == invitation.InviteeId).FirstOrDefaultAsync();
                var inviter = await _dbContext.Users.Find(u => u.Id == invitation.InviterId).FirstOrDefaultAsync();

                if (invitee != null && inviter != null)
                {
                    invitationDtos.Add(new EventInvitationDTO
                    {
                        Id = invitation.Id,
                        EventId = invitation.EventId,
                        InviteeId = invitation.InviteeId,
                        InviterId = invitation.InviterId,
                        Status = invitation.Status,
                        InvitedAt = invitation.InvitedAt,
                        RespondedAt = invitation.RespondedAt,
                        ResponseMessage = invitation.ResponseMessage,
                        InviterUsername = inviter.Username,
                        InviterAvatarUrl = inviter.AvatarUrl ?? ""
                    });
                }
            }

            return invitationDtos;
        }
    }
}
