# RabbitMQ Configuration Guide

## Overview

The auth service uses RabbitMQ for RPC (Remote Procedure Calls) between microservices. **RabbitMQ is optional** - the service will start and work without it, but RPC functionality will be disabled.

## Quick Start (Development)

### Without RabbitMQ (Simplest)
The service will start successfully even if RabbitMQ is not running. You'll see a warning message, but all REST API endpoints will work normally.

### With RabbitMQ (For Full Functionality)

#### macOS (using Homebrew)
```bash
brew install rabbitmq
brew services start rabbitmq
```

#### Verify RabbitMQ is Running
```bash
rabbitmqctl status
```

#### Access Management UI
- URL: http://localhost:15672
- Username: `guest`
- Password: `guest`

## Configuration Options

### Option 1: CloudAMQP Connection String (Recommended for Production)

CloudAMQP provides a free RabbitMQ service that works well with Render.com.

#### Steps:

1. **Create CloudAMQP account** at [cloudamqp.com](https://www.cloudamqp.com)
2. **Create free instance** (Little Lemur plan)
3. **Get connection string** from dashboard (format: `amqps://user:pass@host:port/vhost`)
4. **Set environment variable:**
   ```bash
   CLOUDAMQP_URL=amqps://username:password@coyote.rmq.cloudamqp.com/username
   ```

### Option 2: Individual Environment Variables

Set these environment variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `RABBITMQ_HOSTNAME` | Server hostname | `coyote.rmq.cloudamqp.com` |
| `RABBITMQ_USERNAME` | Username | `your_username` |
| `RABBITMQ_PASSWORD` | Password | `your_password` |
| `RABBITMQ_VIRTUALHOST` | Virtual host | Usually same as username for CloudAMQP |
| `RABBITMQ_PORT` | Port number | `5672` (default) or `5671` for SSL |

### Option 3: Configuration File (Development)

Add to `appsettings.Development.json`:

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "Port": "5672",
    "Exchange": "auth.exchange",
    "Queue": "auth.requests"
  }
}
```

## Render.com Deployment

### Configure Environment Variables in Render:

1. Go to Render.com dashboard
2. Select your auth-service
3. Go to **Environment** tab
4. Add one of:

**Option A - CloudAMQP URL:**
```
CLOUDAMQP_URL=amqps://user:pass@host:port/vhost
```

**Option B - Individual variables:**
```
RABBITMQ_HOSTNAME=coyote.rmq.cloudamqp.com
RABBITMQ_USERNAME=your_username
RABBITMQ_PASSWORD=your_password
RABBITMQ_VIRTUALHOST=your_username
```

5. Click **Save Changes**
6. Service will automatically redeploy

## Verification

### Check Connection Status

Look for this in service logs:

✅ **Success:**
```
[AuthRpcServer] Successfully connected to RabbitMQ at coyote.rmq.cloudamqp.com
```

⚠️ **Not Connected (but service still works):**
```
[AuthRpcServer] Warning: Failed to connect to RabbitMQ: ...
[AuthRpcServer] Service will continue without RabbitMQ RPC support. Direct API calls will still work.
```

### Test in CloudAMQP Dashboard

1. Go to CloudAMQP dashboard
2. Check **Connections** - should see connection from Render.com
3. Check **Queues** - should see `auth.requests` queue
4. Check **Exchanges** - should see `auth.exchange`

## What Works Without RabbitMQ

✅ **Always Available:**
- All REST API endpoints (login, register, etc.)
- JWT authentication
- Email services
- External authentication (Google OAuth)
- Direct HTTP service-to-service calls

❌ **Requires RabbitMQ:**
- RPC-based inter-service communication
- Message queuing between services

## Troubleshooting

### Issue: "Failed to connect to RabbitMQ"

**Possible Causes:**
- Incorrect credentials
- Server not accessible
- Network/firewall issues
- Wrong virtual host

**Solutions:**
1. Verify CloudAMQP connection string is correct
2. Check CloudAMQP instance is active
3. Verify virtual host (usually same as username for CloudAMQP)
4. Test connection from local machine first

### Issue: Service not starting

**Check:**
- Service logs for specific error messages
- Environment variables are set correctly
- Connection string format (should start with `amqps://` or `amqp://`)
- CloudAMQP plan is active

### Issue: Virtual host error

For CloudAMQP, the virtual host is usually the same as your username. The service handles this automatically, but if issues persist:

1. Set `RABBITMQ_VIRTUALHOST` explicitly to your CloudAMQP username
2. Or include it in connection string: `amqps://user:pass@host:port/username`

## Local Development with Docker

If you don't want to install RabbitMQ locally:

```bash
# Start RabbitMQ in Docker
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management

# Check it's running
docker ps | grep rabbitmq

# Stop when done
docker stop rabbitmq
docker rm rabbitmq
```

## Environment Variables Summary

### For CloudAMQP (Recommended):
```bash
CLOUDAMQP_URL=amqps://user:pass@host:port/vhost
```

### For Individual Variables:
```bash
RABBITMQ_HOSTNAME=your-hostname
RABBITMQ_USERNAME=your-username
RABBITMQ_PASSWORD=your-password
RABBITMQ_VIRTUALHOST=your-vhost
RABBITMQ_PORT=5672
```

### Optional Configuration:
```bash
RabbitMq__Exchange=auth.exchange
RabbitMq__Queue=auth.requests
```

## Best Practices

1. **Use CloudAMQP for production** - Free tier is sufficient for development
2. **Use `amqps://` (SSL)** for production connections
3. **Test locally first** before deploying to production
4. **Monitor connections** in CloudAMQP dashboard
5. **Keep credentials secure** - use environment variables, not config files

## Support

If you encounter issues:
1. Check service logs for specific error messages
2. Verify CloudAMQP instance is active
3. Test connection string format
4. Verify environment variables in Render.com
5. Check CloudAMQP dashboard for connection status

## Notes

- **RabbitMQ is optional**: Service works without it
- **Free tier available**: CloudAMQP Little Lemur plan is free
- **SSL required for production**: Use `amqps://` protocol
- **REST API always works**: Even without RabbitMQ
- **Automatic reconnection**: Service attempts to reconnect if connection drops
