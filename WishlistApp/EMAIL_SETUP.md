# Email Setup Guide for Wishlist App

This guide will help you set up Gmail SMTP to send emails from your Wishlist App.

## Prerequisites

- A Gmail account
- 2-Factor Authentication enabled on your Gmail account

## Step 1: Enable 2-Factor Authentication

1. Go to your Google Account settings: https://myaccount.google.com/
2. Navigate to "Security"
3. Enable "2-Step Verification" if not already enabled

## Step 2: Generate an App Password

1. Go to your Google Account settings: https://myaccount.google.com/
2. Navigate to "Security"
3. Under "2-Step Verification", click on "App passwords"
4. Select "Mail" as the app and "Other" as the device
5. Enter a name like "Wishlist App"
6. Click "Generate"
7. **Copy the 16-character password** (you'll need this for the configuration)

## Step 3: Update Configuration

1. Open `appsettings.json` in the `WishlistApp` folder
2. Update the Email section with your Gmail credentials:

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your-gmail@gmail.com",
    "Password": "your-16-character-app-password",
    "FromEmail": "your-gmail@gmail.com",
    "FromName": "Wishlist App"
  }
}
```

**Important Notes:**
- Replace `your-gmail@gmail.com` with your actual Gmail address
- Replace `your-16-character-app-password` with the app password generated in Step 2
- The `Password` field should be the app password, NOT your regular Gmail password

## Step 4: Test the Configuration

1. Start your backend application
2. Try the forgot password functionality
3. Check your email for the password reset link

## Security Best Practices

1. **Never commit your app password to version control**
2. Use environment variables or user secrets for production
3. Consider using a dedicated email service like SendGrid for production

## Troubleshooting

### Common Issues:

1. **"Authentication failed" error**
   - Make sure you're using the app password, not your regular Gmail password
   - Ensure 2-Factor Authentication is enabled

2. **"SMTP server requires authentication" error**
   - Verify your Gmail credentials are correct
   - Check that the SMTP settings are correct (smtp.gmail.com:587)

3. **"Connection timeout" error**
   - Check your internet connection
   - Verify firewall settings aren't blocking SMTP traffic

### For Production:

For production environments, consider using:
- **SendGrid**: Professional email service with better deliverability
- **Amazon SES**: Cost-effective email service
- **Mailgun**: Developer-friendly email API

## Environment Variables (Recommended for Production)

Instead of storing credentials in appsettings.json, use environment variables:

```bash
# Windows
set Email__Username=your-gmail@gmail.com
set Email__Password=your-app-password

# Linux/Mac
export Email__Username=your-gmail@gmail.com
export Email__Password=your-app-password
```

## Testing Email Functionality

1. **Register a new account** - Should send a welcome email
2. **Use forgot password** - Should send a password reset email
3. **Check spam folder** - Sometimes emails might end up there initially

## Email Templates

The app includes beautiful HTML email templates for:
- Welcome emails (when users register)
- Password reset emails (when users request password reset)

Both templates are responsive and include:
- Professional branding
- Clear call-to-action buttons
- Security information
- Mobile-friendly design 