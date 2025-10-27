# Wishera – Complete Gift & Social Platform

A comprehensive, microservices-based gift wishlist and social platform built with **Next.js** (frontend) and **.NET Core** (backend). Features real-time chat, notifications, event management, gift reservations, and a modern responsive UI.

---

## Features

### Authentication & Security
- **User Registration & Login** – Secure JWT-based authentication
- **Email Verification** – Verify accounts via email confirmation
- **External OAuth** – Google OAuth provider
- **Password Recovery** – Reset password via email
- **Account Management** – Update profile, change password, delete account

### Wishlist & Gift Management
- **Create & Manage Wishlists** – Multiple wishlists per user
- **Gift CRUD Operations** – Add, edit, delete, and organize gifts
- **Gift Reservations** – Friends can secretly reserve gifts
- **Image Uploads** – Cloud-based image storage with Cloudinary
- **Gift Sharing** – Share wishlists via unique links
- **Gift Search & Filter** – Advanced filtering and search capabilities

### Real-Time Chat
- **Direct Messaging** – One-on-one conversations
- **Group Chats** – Create and manage group conversations
- **Message History** – Persistent chat history
- **Real-Time Updates** – Powered by SignalR
- **Custom Wallpapers** – Personalize chat backgrounds
- **GIF & Emoji Support** – Rich media messaging

### Notification System
- **Real-Time Notifications** – Instant push notifications
- **Notification Types** – Gift reservations, friend requests, events, messages
- **Notification Management** – Mark as read, delete, auto-cleanup
- **Notification Center** – Centralized notification hub

### Events & Calendar
- **Event Creation** – Create and manage events
- **Event Invitations** – Invite friends to events
- **Calendar Integration** – View all events in calendar format
- **Event Reminders** – Automated notification reminders

### Social Features
- **Friend System** – Add, remove, and manage friends
- **User Profiles** – Customizable user profiles
- **Activity Feed** – See friends' activities

---

## Architecture

This is a **microservices-based application** with the following services:

```
┌─────────────────────────────────────────────────────────────┐
│                        Next.js Frontend                      │
│              (React 19, TypeScript, Tailwind CSS)            │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
┌───────▼─────────┐      ┌───────▼──────────┐
│  Auth Service   │      │  User Service    │
│   (Port 5219)   │      │   (Port 5400)    │
└─────────────────┘      └──────────────────┘
        │                         │
┌───────▼─────────┐      ┌───────▼──────────┐
│ Gift/Wishlist   │      │  Chat Service    │
│    Service      │      │   (Port 5000)    │
│   (Port 5300)   │      │                  │
└─────────────────┘      └──────────────────┘
        │                         │
        └────────────┬────────────┘
                     │
        ┌────────────▼────────────┐
        │    MongoDB + Redis      │
        │  RabbitMQ (Message Bus) │
        └─────────────────────────┘
```

### Services Overview:

1. **Auth Service**
   - User authentication and authorization
   - JWT token management
   - Email verification
   - External OAuth integration

2. **User Service**
   - User profile management
   - Friend system
   - Notifications
   - Account settings

3. **Gift/Wishlist Service**
   - Wishlist CRUD operations
   - Gift management
   - Reservation system
   - Image uploads

4. **Chat Service**
   - Real-time messaging with SignalR
   - Message persistence
   - Group chat management
   - Custom wallpapers

5. **Wishera App**
   - Main application service
   - Event management
   - Calendar integration

---

## Tech Stack

### **Frontend**
- **Next.js 15** – React framework with App Router
- **React 19** – UI library
- **TypeScript** – Type-safe development
- **Tailwind CSS 4** – Utility-first styling
- **Material-UI (MUI)** – Component library
- **Framer Motion** – Animation library
- **SignalR Client** – Real-time communication
- **Axios** – HTTP client
- **React Hook Form + Zod** – Form validation

### **Backend**
- **ASP.NET Core 8** – Web API framework
- **C# 12** – Programming language
- **Entity Framework Core** – ORM for SQL databases
- **MongoDB Driver** – NoSQL database access
- **SignalR** – Real-time communication hub
- **RabbitMQ** – Message broker for inter-service communication
- **Redis** – Caching and session management
- **JWT Authentication** – Secure token-based auth
- **BCrypt** – Password hashing

### **Infrastructure**
- **MongoDB** – Primary database
- **Redis** – Caching layer
- **RabbitMQ** – Message queue
- **Docker & Docker Compose** – Containerization
- **Cloudinary** – Cloud image storage

---

## API Documentation

### Auth Service

| Method | Endpoint | Description |
|--------|---------|-------------|
| `POST` | `/api/Auth/register` | Register new user |
| `POST` | `/api/Auth/login` | Login and receive JWT |
| `POST` | `/api/Auth/verify-email` | Verify email address |
| `POST` | `/api/Auth/forgot-password` | Request password reset |
| `POST` | `/api/Auth/reset-password` | Reset password with token |
| `POST` | `/api/ExternalAuth/google` | Google OAuth login |

### User Service

| Method | Endpoint | Description |
|--------|---------|-------------|
| `GET` | `/api/Users/profile` | Get current user profile |
| `PUT` | `/api/Users/profile` | Update profile |
| `DELETE` | `/api/Users/account` | Delete account |
| `GET` | `/api/Friends` | Get friends list |
| `POST` | `/api/Friends/{userId}` | Add friend |
| `DELETE` | `/api/Friends/{userId}` | Remove friend |
| `GET` | `/api/Notifications` | Get all notifications |
| `PUT` | `/api/Notifications/{id}/read` | Mark as read |
| `DELETE` | `/api/Notifications/{id}` | Delete notification |

### Gift/Wishlist Service

| Method | Endpoint | Description |
|--------|---------|-------------|
| `GET` | `/api/Wishlists` | Get user's wishlists |
| `POST` | `/api/Wishlists` | Create new wishlist |
| `PUT` | `/api/Wishlists/{id}` | Update wishlist |
| `DELETE` | `/api/Wishlists/{id}` | Delete wishlist |
| `GET` | `/api/Gift` | Get gifts in wishlist |
| `POST` | `/api/Gift` | Add new gift |
| `PUT` | `/api/Gift/{id}` | Update gift |
| `DELETE` | `/api/Gift/{id}` | Delete gift |
| `POST` | `/api/Gift/{id}/reserve` | Reserve gift |
| `DELETE` | `/api/Gift/{id}/unreserve` | Unreserve gift |
| `POST` | `/api/Gift/{id}/upload-image` | Upload gift image |

### Chat Service

| Method | Endpoint | Description |
|--------|---------|-------------|
| `GET` | `/api/Chat/conversations` | Get user conversations |
| `POST` | `/api/Chat/conversation` | Create conversation |
| `GET` | `/api/Chat/messages/{conversationId}` | Get messages |
| `POST` | `/api/Chat/message` | Send message |
| `POST` | `/api/Chat/wallpaper` | Upload custom wallpaper |

#### SignalR Hub: `/chatHub`
- `SendMessage` – Send real-time message
- `JoinConversation` – Join chat room
- `LeaveConversation` – Leave chat room
- `TypingIndicator` – Broadcast typing status

### Events Service

| Method | Endpoint | Description |
|--------|---------|-------------|
| `GET` | `/api/Events` | Get all events |
| `POST` | `/api/Events` | Create event |
| `PUT` | `/api/Events/{id}` | Update event |
| `DELETE` | `/api/Events/{id}` | Delete event |
| `POST` | `/api/Events/{id}/invite` | Send invitation |
| `GET` | `/api/Calendar` | Get calendar view |

---

## License

This project is open-source and available under the **MIT License**.

---

## Credits

💡 **Developed with ❤️ by:**
- [@yusufovamina](https://github.com/yusufovamina)
- [@Newalgabe](https://github.com/Newalgabe)

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

---

## Support

For questions or support, please open an issue on GitHub.

---

**⭐ If you like this project, please give it a star!**
