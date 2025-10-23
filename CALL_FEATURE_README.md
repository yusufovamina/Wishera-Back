# Call Feature Implementation

This document describes the call feature implementation in the Wishera chat application.

## Overview

The call feature provides real-time audio and video communication between users using WebRTC technology with SignalR for signaling.

## Features

- **Audio Calls**: Voice-only communication
- **Video Calls**: Video and audio communication
- **Real-time Signaling**: Uses SignalR for call signaling
- **WebRTC**: Peer-to-peer media streaming
- **Call Controls**: Mute, video toggle, end call
- **Incoming Call Notifications**: Toast notifications for incoming calls
- **Call Modal**: Full-screen call interface

## Architecture

### Backend Components

1. **SignalR Hubs** (`ChatHub.cs`):
   - `InitiateCall`: Start a new call
   - `AcceptCall`: Accept an incoming call
   - `RejectCall`: Reject an incoming call
   - `EndCall`: End an active call
   - `SendCallSignal`: Send WebRTC signaling data

2. **Call Signaling Events**:
   - `CallInitiated`: Call invitation sent
   - `CallAccepted`: Call accepted by recipient
   - `CallRejected`: Call rejected by recipient
   - `CallEnded`: Call terminated
   - `CallSignal`: WebRTC signaling data

### Frontend Components

1. **useCall Hook** (`useCall.ts`):
   - Manages call state and WebRTC connections
   - Handles media streams (audio/video)
   - Provides call control functions

2. **CallModal Component** (`CallModal.tsx`):
   - Full-screen call interface
   - Video display for both local and remote streams
   - Call controls (mute, video toggle, end call)

3. **CallNotification Component** (`CallNotification.tsx`):
   - Toast notification for incoming calls
   - Quick accept/reject buttons

## Usage

### Starting a Call

```typescript
// Audio call
startCall(contactId, "audio");

// Video call
startCall(contactId, "video");
```

### Call Controls

```typescript
// Toggle mute
toggleMute();

// Toggle video (video calls only)
toggleVideo();

// End call
endCall();
```

### Call State Management

```typescript
const {
  currentCall,      // Current call info
  localStream,      // Local media stream
  remoteStream,     // Remote media stream
  isMuted,          // Mute state
  isVideoEnabled,   // Video state
  // ... other properties
} = useCall(currentUserId, {
  onCallInitiated: (callInfo) => { /* handle call initiated */ },
  onCallAccepted: (callInfo) => { /* handle call accepted */ },
  onCallRejected: (callInfo) => { /* handle call rejected */ },
  onCallEnded: (callInfo) => { /* handle call ended */ },
  onCallFailed: (callInfo) => { /* handle call failed */ },
});
```

## WebRTC Configuration

The implementation uses Google's STUN servers for NAT traversal:

```typescript
const rtcConfig: RTCConfiguration = {
  iceServers: [
    { urls: "stun:stun.l.google.com:19302" },
    { urls: "stun:stun1.l.google.com:19302" },
  ],
};
```

## Browser Compatibility

- **Chrome/Edge**: Full support
- **Firefox**: Full support
- **Safari**: Full support (iOS 11+)
- **Mobile Browsers**: Supported on modern mobile browsers

## Security Considerations

- Media access requires user permission
- WebRTC connections are encrypted
- Signaling goes through authenticated SignalR connections
- No media data is stored on the server

## Troubleshooting

### Common Issues

1. **No Audio/Video**: Check browser permissions
2. **Connection Failed**: Verify STUN server accessibility
3. **Call Not Received**: Check SignalR connection status

### Debug Information

Enable console logging to see call events and WebRTC connection states.

## Future Enhancements

- Call recording
- Screen sharing
- Group calls
- Call history
- Push notifications for missed calls
- Custom STUN/TURN servers for better connectivity

