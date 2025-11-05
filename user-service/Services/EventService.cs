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
        Task<EventInvitationDTO> ChangeInvitationResponseAsync(string invitationId, string userId, RespondToInvitationDTO responseDto);
        Task<EventInvitationListDTO> GetUserInvitationsAsync(string userId, int page = 1, int pageSize = 10);
        Task<object> GetUserInvitationStatisticsAsync(string userId);
        Task<bool> IsUserInvitedToEventAsync(string eventId, string userId);
        Task<List<EventInvitationDTO>> GetEventInvitationsAsync(string eventId, string userId);
        Task<List<Event>> GetAllEventsAsync();
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

            Console.WriteLine($"GetUserEventsAsync called with userId: {userId}");
            
            // Temporarily disable caching to fix the cache invalidation issue
            // Get all events for the user (no pagination in cache)
            
            // First, let's get ALL events to debug
            var allEventsInDb = await _dbContext.Events.Find(_ => true).ToListAsync();
            Console.WriteLine($"Total events in database: {allEventsInDb.Count}");
            foreach (var evt in allEventsInDb)
            {
                Console.WriteLine($"All Events - Event: {evt.Title} (ID: {evt.Id}, CreatorId: {evt.CreatorId}, IsCancelled: {evt.IsCancelled})");
            }
            
            // Now filter for the user's events
            var filter = Builders<Event>.Filter.And(
                Builders<Event>.Filter.Eq(e => e.CreatorId, userId),
                Builders<Event>.Filter.Eq(e => e.IsCancelled, false)
            );
            
            var allEvents = await _dbContext.Events
                .Find(filter)
                .Sort(Builders<Event>.Sort.Descending(e => e.EventDate))
                .ToListAsync();

            Console.WriteLine($"Found {allEvents.Count} events for user {userId}");
            foreach (var evt in allEvents)
            {
                Console.WriteLine($"Event: {evt.Title} (ID: {evt.Id}, CreatorId: {evt.CreatorId}, IsCancelled: {evt.IsCancelled})");
            }

            var eventDtos = new List<EventDTO>();
            foreach (var eventEntity in allEvents)
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
                TotalCount = eventDtos.Count,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)eventDtos.Count / pageSize)
            };
        }

        public async Task<EventListDTO> GetInvitedEventsAsync(string userId, int page = 1, int pageSize = 10)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            // Use a simpler cache key without pagination to make cache clearing easier
            var cacheKey = $"user:invited-events:{userId}";
            var cachedData = await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                // Get ALL invitations for this user (no pagination in cache)
                var allInvitations = await _dbContext.EventInvitations
                    .Find(i => i.InviteeId == userId)
                    .Sort(Builders<EventInvitation>.Sort.Descending(i => i.InvitedAt))
                    .ToListAsync();

                var eventDtos = new List<EventDTO>();
                foreach (var invitation in allInvitations)
                {
                    var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
                    if (eventEntity == null || eventEntity.IsCancelled) continue;

                    var creator = await _dbContext.Users.Find(u => u.Id == eventEntity.CreatorId).FirstOrDefaultAsync();
                    if (creator == null) continue;

                    var allEventInvitations = await _dbContext.EventInvitations
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
                        AcceptedCount = allEventInvitations.Count(i => i.Status == InvitationStatus.Accepted),
                        DeclinedCount = allEventInvitations.Count(i => i.Status == InvitationStatus.Declined),
                        PendingCount = allEventInvitations.Count(i => i.Status == InvitationStatus.Pending),
                        UserResponse = invitation.Status,
                        InvitationId = invitation.Id
                    });
                }

                return eventDtos;
            }, TimeSpan.FromMinutes(10));

            // Handle null cache result
            if (cachedData == null)
            {
                return new EventListDTO
                {
                    Events = new List<EventDTO>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 0
                };
            }

            // Apply pagination to the cached data
            var totalCount = cachedData.Count;
            var skip = (page - 1) * pageSize;
            var paginatedEvents = cachedData.Skip(skip).Take(pageSize).ToList();

            return new EventListDTO
            {
                Events = paginatedEvents,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
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

            // Handle invitee updates
            if (updateEventDto.InviteeIds != null)
            {
                // Get current invitees
                var currentInvitees = eventEntity.InviteeIds ?? new List<string>();
                var newInvitees = updateEventDto.InviteeIds;

                // Find new invitees (not in current list)
                var inviteesToAdd = newInvitees.Except(currentInvitees).ToList();
                
                // Find removed invitees (in current list but not in new list)
                var inviteesToRemove = currentInvitees.Except(newInvitees).ToList();

                // Update event with new invitee list
                await _dbContext.Events.UpdateOneAsync(
                    e => e.Id == eventId, 
                    Builders<Event>.Update.Set(e => e.InviteeIds, newInvitees)
                );

                // Create invitations for new invitees
                if (inviteesToAdd.Any())
                {
                    var newInvitations = inviteesToAdd.Select(inviteeId => new EventInvitation
                    {
                        EventId = eventId,
                        InviterId = userId,
                        InviteeId = inviteeId,
                        Status = InvitationStatus.Pending,
                        InvitedAt = DateTime.UtcNow
                    }).ToList();

                    await _dbContext.EventInvitations.InsertManyAsync(newInvitations);

                    // Send invitation notifications to new invitees
                    foreach (var inviteeId in inviteesToAdd)
                    {
                        await _notificationService.CreateEventInvitationNotificationAsync(
                            inviteeId,
                            userId,
                            eventId,
                            eventEntity.Title
                        );
                    }
                }

                // Remove invitations for removed invitees
                if (inviteesToRemove.Any())
                {
                    await _dbContext.EventInvitations.DeleteManyAsync(
                        i => i.EventId == eventId && inviteesToRemove.Contains(i.InviteeId)
                    );
                }
            }

            // Clear cache - remove all possible cache keys for this user's events
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

            // Clear cache - remove all possible cache keys for this user's events
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

            // Clear cache - remove all possible cache keys for this user's events
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

            var invitationDto = new EventInvitationDTO
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

            if (eventEntity != null)
            {
                SetInvitationStatusDisplayInfo(invitationDto, eventEntity);
            }

            return invitationDto;
        }

        public async Task<EventInvitationDTO> ChangeInvitationResponseAsync(string invitationId, string userId, RespondToInvitationDTO responseDto)
        {
            if (!IsValidObjectId(invitationId)) throw new ArgumentException("Invalid invitation ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var invitation = await _dbContext.EventInvitations.Find(i => i.Id == invitationId).FirstOrDefaultAsync();
            if (invitation == null) throw new KeyNotFoundException("Invitation not found.");
            if (invitation.InviteeId != userId) throw new UnauthorizedAccessException("You can only change responses for your own invitations.");

            // Check if the event is still active (not cancelled)
            var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
            if (eventEntity == null) throw new KeyNotFoundException("Event not found.");
            if (eventEntity.IsCancelled) throw new InvalidOperationException("Cannot change response for a cancelled event.");

            // Check if the event date has passed
            if (eventEntity.EventDate < DateTime.UtcNow.Date)
            {
                throw new InvalidOperationException("Cannot change response for past events.");
            }

            var previousStatus = invitation.Status;
            var updateDefinition = Builders<EventInvitation>.Update
                .Set(i => i.Status, responseDto.Status)
                .Set(i => i.RespondedAt, DateTime.UtcNow)
                .Set(i => i.ResponseMessage, responseDto.ResponseMessage);

            await _dbContext.EventInvitations.UpdateOneAsync(i => i.Id == invitationId, updateDefinition);

            var inviter = await _dbContext.Users.Find(u => u.Id == invitation.InviterId).FirstOrDefaultAsync();

            if (eventEntity != null && inviter != null)
            {
                // Send response change notification to event creator
                await _notificationService.CreateEventResponseNotificationAsync(
                    invitation.InviterId,
                    userId,
                    invitation.EventId,
                    eventEntity.Title,
                    responseDto.Status,
                    responseDto.ResponseMessage
                );
            }

            // Clear relevant caches
            await _cache.RemoveAsync($"user:invited-events:{userId}");
            await _cache.RemoveAsync($"user:invitations:{userId}:*");
            await _cache.RemoveAsync($"event:{invitation.EventId}:*");

            var invitationDto = new EventInvitationDTO
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
                InviterAvatarUrl = inviter?.AvatarUrl ?? "",
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
                    CreatorUsername = inviter?.Username ?? "",
                    CreatorAvatarUrl = inviter?.AvatarUrl ?? "",
                    EventType = eventEntity.EventType,
                    IsCancelled = eventEntity.IsCancelled
                }
            };

            SetInvitationStatusDisplayInfo(invitationDto, eventEntity);
            return invitationDto;
        }

        private void SetInvitationStatusDisplayInfo(EventInvitationDTO invitationDto, Event eventEntity)
        {
            // Determine if response can be changed
            invitationDto.CanChangeResponse = !eventEntity.IsCancelled && eventEntity.EventDate >= DateTime.UtcNow.Date;

            // Set status display text and color
            switch (invitationDto.Status)
            {
                case InvitationStatus.Pending:
                    invitationDto.StatusDisplayText = "Pending Response";
                    invitationDto.StatusColor = "#FFA500"; // Orange
                    break;
                case InvitationStatus.Accepted:
                    invitationDto.StatusDisplayText = "Accepted";
                    invitationDto.StatusColor = "#28A745"; // Green
                    break;
                case InvitationStatus.Declined:
                    invitationDto.StatusDisplayText = "Declined";
                    invitationDto.StatusColor = "#DC3545"; // Red
                    break;
                case InvitationStatus.Maybe:
                    invitationDto.StatusDisplayText = "Maybe";
                    invitationDto.StatusColor = "#6C757D"; // Gray
                    break;
                default:
                    invitationDto.StatusDisplayText = "Unknown";
                    invitationDto.StatusColor = "#6C757D";
                    break;
            }
        }

        public async Task<object> GetUserInvitationStatisticsAsync(string userId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:invitation-stats:{userId}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var invitations = await _dbContext.EventInvitations
                    .Find(i => i.InviteeId == userId)
                    .ToListAsync();

                var totalInvitations = invitations.Count;
                var pendingCount = invitations.Count(i => i.Status == InvitationStatus.Pending);
                var acceptedCount = invitations.Count(i => i.Status == InvitationStatus.Accepted);
                var declinedCount = invitations.Count(i => i.Status == InvitationStatus.Declined);
                var maybeCount = invitations.Count(i => i.Status == InvitationStatus.Maybe);

                // Get upcoming events count (accepted invitations for future events)
                var upcomingEventsCount = 0;
                foreach (var invitation in invitations.Where(i => i.Status == InvitationStatus.Accepted))
                {
                    var eventEntity = await _dbContext.Events.Find(e => e.Id == invitation.EventId).FirstOrDefaultAsync();
                    if (eventEntity != null && eventEntity.EventDate >= DateTime.UtcNow.Date && !eventEntity.IsCancelled)
                    {
                        upcomingEventsCount++;
                    }
                }

                return new
                {
                    TotalInvitations = totalInvitations,
                    PendingCount = pendingCount,
                    AcceptedCount = acceptedCount,
                    DeclinedCount = declinedCount,
                    MaybeCount = maybeCount,
                    UpcomingEventsCount = upcomingEventsCount,
                    ResponseRate = totalInvitations > 0 ? Math.Round((double)(acceptedCount + declinedCount + maybeCount) / totalInvitations * 100, 1) : 0
                };
            }, TimeSpan.FromMinutes(15));
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
                        var invitationDto = new EventInvitationDTO
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
                        };

                        SetInvitationStatusDisplayInfo(invitationDto, eventEntity);
                        invitationDtos.Add(invitationDto);
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

        public async Task<List<Event>> GetAllEventsAsync()
        {
            return await _dbContext.Events.Find(_ => true).ToListAsync();
        }
    }
}
