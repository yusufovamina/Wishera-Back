# RabbitMQ Configuration Guide for Render.com

## Overview

The auth service uses RabbitMQ for RPC (Remote Procedure Calls) between services. RabbitMQ is **optional** - the service will start and work without it, but RPC functionality will be disabled.

## Configuration Options

### Option 1: CloudAMQP Connection String (Recommended)

CloudAMQP provides a free RabbitMQ service that works well with Render.com. You can get a free instance at [cloudamqp.com](https://www.cloudamqp.com).

#### Steps:

1. **Create a CloudAMQP account** and create a free instance
2. **Get the connection string** from your CloudAMQP dashboard (format: `amqps://user:pass@host:port/vhost`)
3. **Set environment variable in Render.com:**
   - Go to your Render.com dashboard
   - Navigate to `wishera-auth-service` → Environment
   - Add environment variable:
     - **Key**: `CLOUDAMQP_URL` or `RABBITMQ_URL`
     - **Value**: Your CloudAMQP connection string (e.g., `amqps://username:password@coyote.rmq.cloudamqp.com/username`)

The service will automatically parse this connection string and connect to RabbitMQ.

### Option 2: Individual Environment Variables

If you prefer to set individual environment variables:

#### In Render.com Dashboard:

1. Go to `wishera-auth-service` → Environment
2. Add the following environment variables:

| Key | Value | Example |
|-----|-------|---------|
| `RABBITMQ_HOSTNAME` | RabbitMQ server hostname | `coyote.rmq.cloudamqp.com` |
| `RABBITMQ_USERNAME` | RabbitMQ username | `your_username` |
| `RABBITMQ_PASSWORD` | RabbitMQ password | `your_password` |
| `RABBITMQ_VIRTUALHOST` | Virtual host (optional) | Usually same as username for CloudAMQP |
| `RABBITMQ_PORT` | Port number (optional) | `5672` (default) or `5671` for SSL |

#### Alternative: Use Configuration Format

You can also use the configuration format in Render.com:

| Key | Value |
|-----|-------|
| `RabbitMq__HostName` | RabbitMQ hostname |
| `RabbitMq__UserName` | RabbitMQ username |
| `RabbitMq__Password` | RabbitMQ password |
| `RabbitMq__VirtualHost` | Virtual host |
| `RabbitMq__Port` | Port number |
| `RabbitMq__Exchange` | Exchange name (default: `auth.exchange`) |
| `RabbitMq__Queue` | Queue name (default: `auth.requests`) |

### Option 3: Local Development

For local development, RabbitMQ defaults to `localhost:5672` with guest/guest credentials.

Make sure RabbitMQ is running locally:
```bash
# Using Docker
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Or install locally and start the service
```

## Verification

### Check if RabbitMQ is Connected

1. **Check service logs** in Render.com:
   - Look for: `[AuthRpcServer] Successfully connected to RabbitMQ at ...`
   - If not connected: `[AuthRpcServer] Warning: Failed to connect to RabbitMQ: ...`

2. **Test the service:**
   - The service should start successfully even if RabbitMQ is not available
   - Direct API calls will work regardless of RabbitMQ status
   - Only RPC calls will be affected if RabbitMQ is unavailable

### Common Issues

#### Issue: "Failed to connect to RabbitMQ"

**Causes:**
- RabbitMQ credentials are incorrect
- RabbitMQ server is not accessible
- Network/firewall issues
- Virtual host is incorrect

**Solutions:**
1. Verify your CloudAMQP connection string is correct
2. Check that your CloudAMQP instance is active
3. Verify virtual host (usually same as username for CloudAMQP)
4. Check network connectivity from Render.com to CloudAMQP

#### Issue: Service starts but RabbitMQ is not connecting

**Check:**
1. Environment variables are set correctly in Render.com
2. Connection string format is correct (should start with `amqps://` or `amqp://`)
3. Credentials are valid
4. Check Render.com logs for specific error messages

#### Issue: Virtual host error

For CloudAMQP, the virtual host is usually the same as your username. The service automatically handles this, but if you're still having issues:

1. Set `RABBITMQ_VIRTUALHOST` to your CloudAMQP username
2. Or include it in the connection string: `amqps://user:pass@host:port/username`

## CloudAMQP Setup (Step by Step)

1. **Sign up for CloudAMQP:**
   - Go to [cloudamqp.com](https://www.cloudamqp.com)
   - Sign up for a free account
   - Create a new instance (Little Lemur plan is free)

2. **Get connection details:**
   - Go to your CloudAMQP dashboard
   - Click on your instance
   - Copy the connection string or individual credentials

3. **Configure in Render.com:**
   - Go to Render.com dashboard
   - Navigate to `wishera-auth-service`
   - Go to Environment tab
   - Add `CLOUDAMQP_URL` environment variable
   - Paste your connection string

4. **Redeploy:**
   - The service will automatically connect to RabbitMQ on next deployment
   - Check logs to verify connection

## Environment Variables Summary

### Required for RabbitMQ (if using):
- `CLOUDAMQP_URL` or `RABBITMQ_URL` (connection string format) **OR**
- `RABBITMQ_HOSTNAME`, `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD` (individual variables)

### Optional:
- `RABBITMQ_VIRTUALHOST` - Virtual host (defaults to username for CloudAMQP)
- `RABBITMQ_PORT` - Port number (defaults to 5672)
- `RabbitMq__Exchange` - Exchange name (defaults to `auth.exchange`)
- `RabbitMq__Queue` - Queue name (defaults to `auth.requests`)

## Testing

### Test RabbitMQ Connection:

1. **Check logs:**
   ```bash
   # In Render.com, check service logs for:
   [AuthRpcServer] Successfully connected to RabbitMQ at ...
   ```

2. **Test RPC functionality:**
   - If RabbitMQ is connected, RPC calls between services will work
   - If not connected, direct API calls will still work

3. **Verify in CloudAMQP dashboard:**
   - Go to CloudAMQP dashboard
   - Check "Connections" - you should see a connection from Render.com
   - Check "Queues" - you should see `auth.requests` queue

## Notes

- **RabbitMQ is optional**: The service will start and work without RabbitMQ
- **Direct API calls always work**: Even without RabbitMQ, HTTP API endpoints work
- **RPC is optional**: RPC functionality is only used for inter-service communication
- **Free tier**: CloudAMQP free tier is sufficient for development and small deployments
- **Security**: Use `amqps://` (SSL) connection strings for production

## Support

If you encounter issues:
1. Check Render.com service logs for specific error messages
2. Verify CloudAMQP instance is active and accessible
3. Test connection string format
4. Verify environment variables are set correctly in Render.com

