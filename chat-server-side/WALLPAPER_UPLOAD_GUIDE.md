# Custom Wallpaper Upload Guide

This guide explains how to use the new custom wallpaper upload functionality in the chat service.

## Overview

The chat service now supports uploading custom wallpapers that are stored in the cloud (Cloudinary) and can be used as background wallpapers in chat conversations. Users can upload their own images and manage them alongside the default wallpapers.

## API Endpoints

### 1. Upload Custom Wallpaper
**POST** `/api/chat/upload-wallpaper`

Upload a custom wallpaper image to Cloudinary.

**Form Data:**
- `file` (required): Image file (JPG, PNG, etc.)
- `userId` (required): User ID who owns the wallpaper
- `name` (optional): Custom name for the wallpaper
- `description` (optional): Description of the wallpaper
- `category` (optional): Category (default: "custom")
- `supportsDark` (optional): Whether it works well in dark mode (default: true)
- `supportsLight` (optional): Whether it works well in light mode (default: true)

**Response:**
```json
{
  "wallpaperId": "custom_12345-67890-abcdef",
  "url": "https://res.cloudinary.com/...",
  "name": "My Custom Wallpaper",
  "description": "A beautiful custom wallpaper",
  "category": "custom",
  "supportsDark": true,
  "supportsLight": true
}
```

### 2. Get All Wallpapers
**GET** `/api/chat/wallpapers?userId={userId}`

Get both default and custom wallpapers for a user.

**Response:**
```json
[
  {
    "id": "abstract-aurora",
    "name": "Abstract Aurora",
    "description": "Flowing teal/purple gradients",
    "category": "abstract",
    "supportsDark": true,
    "supportsLight": true,
    "previewUrl": "/wallpapers/abstract-aurora.svg"
  },
  {
    "id": "custom_12345-67890-abcdef",
    "name": "My Custom Wallpaper",
    "description": "A beautiful custom wallpaper",
    "category": "custom",
    "supportsDark": true,
    "supportsLight": true,
    "previewUrl": "https://res.cloudinary.com/..."
  }
]
```

### 3. Get Custom Wallpapers
**GET** `/api/chat/custom-wallpapers?userId={userId}`

Get only custom wallpapers for a specific user.

### 4. Delete Custom Wallpaper
**DELETE** `/api/chat/custom-wallpapers/{wallpaperId}?userId={userId}`

Delete a custom wallpaper (both from Cloudinary and database).

### 5. Set Wallpaper Preference
**POST** `/api/chat/preferences/wallpaper`

Set wallpaper preference for a chat conversation.

**Request Body:**
```json
{
  "me": "user1",
  "peer": "user2",
  "wallpaperId": "custom_12345-67890-abcdef",
  "opacity": 0.3,
  "wallpaperUrl": "https://res.cloudinary.com/..." // optional, auto-resolved
}
```

### 6. Get Wallpaper Preference
**GET** `/api/chat/preferences/wallpaper?me={user1}&peer={user2}`

Get current wallpaper preference for a chat conversation.

**Response:**
```json
{
  "wallpaperId": "custom_12345-67890-abcdef",
  "opacity": 0.3,
  "wallpaperUrl": "https://res.cloudinary.com/..."
}
```

## Features

- **Cloud Storage**: Custom wallpapers are stored in Cloudinary for reliable access
- **Image Optimization**: Images are automatically resized and optimized (1920x1080, 85% quality)
- **User Ownership**: Each user can only manage their own custom wallpapers
- **Metadata Storage**: Wallpaper metadata is stored in MongoDB
- **Backward Compatibility**: Existing default wallpapers continue to work
- **Mixed Support**: Custom wallpapers can specify dark/light mode compatibility

## File Requirements

- **Format**: Images only (JPG, PNG, etc.)
- **Size**: Maximum 10MB
- **Resolution**: Automatically resized to 1920x1080 with fill crop
- **Quality**: Optimized to 85% quality for web delivery

## Database Collections

### `custom_wallpapers`
Stores metadata for custom wallpapers:
```json
{
  "id": "custom_12345-67890-abcdef",
  "name": "My Custom Wallpaper",
  "description": "A beautiful custom wallpaper",
  "category": "custom",
  "supportsDark": true,
  "supportsLight": true,
  "previewUrl": "https://res.cloudinary.com/...",
  "cloudinaryPublicId": "wallpapers/custom/user123_abc123",
  "userId": "user123",
  "uploadedAt": "2024-01-01T00:00:00Z",
  "isCustom": true
}
```

### `chat_wallpaper_prefs` (Updated)
Now includes wallpaperUrl field:
```json
{
  "key": "user1:user2",
  "wallpaperId": "custom_12345-67890-abcdef",
  "opacity": 0.3,
  "wallpaperUrl": "https://res.cloudinary.com/..."
}
```

## Configuration

Ensure Cloudinary is properly configured in `appsettings.json`:

```json
{
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

Or use the connection URL format:
```json
{
  "Cloudinary": {
    "Url": "cloudinary://apiKey:apiSecret@cloudName"
  }
}
```

## Testing

Use the provided `wallpaper-api-tests.http` file to test the API endpoints. Make sure to:

1. Start the chat service
2. Configure Cloudinary credentials
3. Replace test user IDs with actual user IDs
4. Use a real image file for upload tests

## Error Handling

The API returns appropriate HTTP status codes:
- `400 Bad Request`: Missing required parameters or invalid file
- `404 Not Found`: Wallpaper not found or access denied
- `500 Internal Server Error`: Cloudinary configuration issues or server errors

All errors include descriptive messages in the response body.
