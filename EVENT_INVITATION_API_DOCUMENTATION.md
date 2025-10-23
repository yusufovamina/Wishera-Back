# 🎉 Event Invitation API Documentation

## Overview
This document describes the enhanced event invitation system that allows invitees to view and change their responses to event invitations. The system provides comprehensive status tracking, validation, and UI-friendly data for frontend integration.

## 🔗 Base URL
```
http://localhost:5220/api/EventInvitations
```

## 📋 Available Endpoints

### 1. Get All My Invitations
**GET** `/api/EventInvitations/all`

Retrieves all invitations for the authenticated user with pagination support.

**Query Parameters:**
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 10)

**Response:**
```json
{
  "invitations": [
    {
      "id": "invitation_id",
      "eventId": "event_id",
      "inviteeId": "user_id",
      "inviterId": "creator_id",
      "status": 1,
      "invitedAt": "2024-01-15T10:00:00Z",
      "respondedAt": "2024-01-15T12:00:00Z",
      "responseMessage": "Looking forward to it!",
      "inviterUsername": "john_doe",
      "inviterAvatarUrl": "https://example.com/avatar.jpg",
      "canChangeResponse": true,
      "statusDisplayText": "Accepted",
      "statusColor": "#28A745",
      "event": {
        "id": "event_id",
        "title": "Birthday Party",
        "description": "Come celebrate!",
        "eventDate": "2024-02-15T00:00:00Z",
        "eventTime": "18:00:00",
        "location": "123 Main St",
        "additionalNotes": "Bring gifts!",
        "creatorId": "creator_id",
        "creatorUsername": "john_doe",
        "creatorAvatarUrl": "https://example.com/avatar.jpg",
        "eventType": "Birthday",
        "isCancelled": false
      }
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3
}
```

### 2. Get Pending Invitations Only
**GET** `/api/EventInvitations/pending`

Retrieves only pending invitations for the authenticated user.

**Query Parameters:**
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 10)

**Response:** Same structure as above, but filtered to only pending invitations.

### 3. Respond to Invitation (Initial Response)
**PUT** `/api/EventInvitations/{invitationId}/respond`

Allows a user to respond to an invitation for the first time.

**Request Body:**
```json
{
  "status": 1,
  "responseMessage": "Looking forward to it!"
}
```

**Status Values:**
- `0`: Pending
- `1`: Accepted
- `2`: Declined
- `3`: Maybe

**Response:** Returns the updated `EventInvitationDTO`.

### 4. Change Invitation Response ⭐ NEW
**PUT** `/api/EventInvitations/{invitationId}/change-response`

Allows a user to change their existing response to an invitation.

**Request Body:**
```json
{
  "status": 2,
  "responseMessage": "Sorry, I can't make it after all"
}
```

**Validation Rules:**
- User can only change their own invitations
- Event must not be cancelled
- Event date must not have passed
- Response can be changed multiple times until event date

**Response:** Returns the updated `EventInvitationDTO` with new status information.

### 5. Get Invitation Statistics ⭐ NEW
**GET** `/api/EventInvitations/statistics`

Provides statistics about the user's invitation responses for dashboard/UI purposes.

**Response:**
```json
{
  "totalInvitations": 25,
  "pendingCount": 3,
  "acceptedCount": 15,
  "declinedCount": 5,
  "maybeCount": 2,
  "upcomingEventsCount": 12,
  "responseRate": 88.0
}
```

## 🎨 UI Design Guidelines

### Status Colors
The API provides color codes for consistent UI theming:

- **Pending**: `#FFA500` (Orange)
- **Accepted**: `#28A745` (Green)
- **Declined**: `#DC3545` (Red)
- **Maybe**: `#6C757D` (Gray)

### Status Display Text
- **Pending**: "Pending Response"
- **Accepted**: "Accepted"
- **Declined**: "Declined"
- **Maybe**: "Maybe"

### Response Change Logic
- `canChangeResponse`: Boolean indicating if the user can change their response
- Users cannot change responses for:
  - Cancelled events
  - Past events
  - Events they don't have access to

## 🔄 Frontend Integration Examples

### React/JavaScript Example

```javascript
// Get all invitations
const getAllInvitations = async (page = 1, pageSize = 10) => {
  const response = await fetch(`/api/EventInvitations/all?page=${page}&pageSize=${pageSize}`, {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    }
  });
  return await response.json();
};

// Change invitation response
const changeInvitationResponse = async (invitationId, status, message = '') => {
  const response = await fetch(`/api/EventInvitations/${invitationId}/change-response`, {
    method: 'PUT',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      status: status,
      responseMessage: message
    })
  });
  return await response.json();
};

// Get statistics for dashboard
const getInvitationStatistics = async () => {
  const response = await fetch('/api/EventInvitations/statistics', {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    }
  });
  return await response.json();
};
```

### UI Component Example (React)

```jsx
const InvitationCard = ({ invitation }) => {
  const [isChanging, setIsChanging] = useState(false);
  
  const handleStatusChange = async (newStatus) => {
    setIsChanging(true);
    try {
      await changeInvitationResponse(invitation.id, newStatus);
      // Refresh the invitation list
      window.location.reload(); // or use state management
    } catch (error) {
      console.error('Failed to change response:', error);
    } finally {
      setIsChanging(false);
    }
  };

  return (
    <div className="invitation-card">
      <h3>{invitation.event.title}</h3>
      <p>From: {invitation.inviterUsername}</p>
      <p>Date: {new Date(invitation.event.eventDate).toLocaleDateString()}</p>
      
      <div className="status-section">
        <span 
          className="status-badge" 
          style={{ backgroundColor: invitation.statusColor }}
        >
          {invitation.statusDisplayText}
        </span>
        
        {invitation.canChangeResponse && (
          <div className="response-buttons">
            <button 
              onClick={() => handleStatusChange(1)}
              disabled={isChanging || invitation.status === 1}
            >
              Accept
            </button>
            <button 
              onClick={() => handleStatusChange(2)}
              disabled={isChanging || invitation.status === 2}
            >
              Decline
            </button>
            <button 
              onClick={() => handleStatusChange(3)}
              disabled={isChanging || invitation.status === 3}
            >
              Maybe
            </button>
          </div>
        )}
      </div>
      
      {invitation.responseMessage && (
        <p className="response-message">{invitation.responseMessage}</p>
      )}
    </div>
  );
};
```

## 🚨 Error Handling

### Common Error Responses

**400 Bad Request:**
```json
{
  "message": "Invalid invitation ID format."
}
```

**401 Unauthorized:**
```json
{
  "message": "You can only change responses for your own invitations."
}
```

**404 Not Found:**
```json
{
  "message": "Invitation not found."
}
```

**400 Bad Request (Business Logic):**
```json
{
  "message": "Cannot change response for a cancelled event."
}
```

```json
{
  "message": "Cannot change response for past events."
}
```

## 🔐 Authentication

All endpoints require JWT authentication. Include the token in the Authorization header:

```
Authorization: Bearer <your_jwt_token>
```

## 📊 Database Changes

The system uses the existing `EventInvitation` model with these fields:
- `Status`: Enum (Pending, Accepted, Declined, Maybe)
- `RespondedAt`: DateTime? (updated when response changes)
- `ResponseMessage`: string? (optional message with response)

## 🎯 Key Features

1. **Response Change Capability**: Users can change their responses multiple times
2. **Smart Validation**: Prevents changes for cancelled or past events
3. **Rich Status Information**: Color codes and display text for UI consistency
4. **Statistics Dashboard**: Comprehensive invitation analytics
5. **Cache Optimization**: Efficient caching with proper invalidation
6. **Notification System**: Event creators are notified of response changes

## 🔄 Migration Notes

- Existing invitations will work seamlessly with the new system
- The `RespondedAt` field will be populated when users change responses
- Cache keys are automatically invalidated when responses change
- No database migration required - uses existing schema

---

*This API enhancement allows for a much more flexible and user-friendly event invitation system, enabling invitees to manage their responses dynamically while maintaining data integrity and providing rich UI information.*
