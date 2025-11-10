# RabbitMQ Setup (Optional)

The auth-service uses RabbitMQ for RPC (Remote Procedure Call) communication between services. However, **RabbitMQ is now optional** - the service will start even if RabbitMQ is not available.

## Quick Start (Without RabbitMQ)

The auth-service will now start successfully even if RabbitMQ is not running. You'll see a warning message, but the REST API endpoints will work normally.

## Installing RabbitMQ (Optional)

If you want to enable full RPC functionality, install RabbitMQ:

### macOS (using Homebrew)
```bash
brew install rabbitmq
brew services start rabbitmq
```

### Verify RabbitMQ is Running
```bash
rabbitmqctl status
```

### Access RabbitMQ Management UI
Once installed, you can access the management UI at:
- http://localhost:15672
- Username: `guest`
- Password: `guest`

## Disabling RabbitMQ in Development

If you want to explicitly disable RabbitMQ to avoid connection attempts, add this to `appsettings.Development.json`:

```json
{
  "RabbitMq": {
    "Enabled": false,
    ...
  }
}
```

## What Works Without RabbitMQ

✅ All REST API endpoints (login, register, etc.)
✅ JWT authentication
✅ Email services
✅ External authentication (Google OAuth)

❌ RPC-based inter-service communication (if other services use it)

## Production

In production environments (like Render.com), RabbitMQ is typically provided as a managed service (e.g., CloudAMQP). The service will automatically use environment variables if configured.

