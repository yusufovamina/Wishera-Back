using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace auth_service.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
        Task SendPasswordResetEmailAsync(string email, string code, string username, bool isMobile = false);
        Task SendEmailVerificationAsync(string email, string token, string username);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
        {
            var emailEnabled = _configuration.GetValue<bool>("Email:Enabled");
            
            // If email is disabled, just log to console (development mode)
            if (!emailEnabled)
            {
                _logger.LogInformation("================================================");
                _logger.LogInformation("EMAIL (Development Mode - Not Sent)");
                _logger.LogInformation("================================================");
                _logger.LogInformation("To: {Email}", toEmail);
                _logger.LogInformation("Subject: {Subject}", subject);
                _logger.LogInformation("Body: {Body}", textBody);
                _logger.LogInformation("================================================");
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["Email:FromName"] ?? "Wishera",
                    _configuration["Email:FromAddress"] ?? throw new InvalidOperationException("Email:FromAddress not configured")
                ));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = textBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                
                var host = _configuration["Email:SmtpHost"] ?? throw new InvalidOperationException("Email:SmtpHost not configured");
                var port = _configuration.GetValue<int>("Email:SmtpPort");
                var username = _configuration["Email:Username"];
                var password = _configuration["Email:Password"];

                _logger.LogInformation("Connecting to SMTP server {Host}:{Port}", host, port);

                // For Gmail (port 587), use STARTTLS
                if (port == 587)
                {
                    await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                }
                // For port 465, use direct SSL
                else if (port == 465)
                {
                    await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
                }
                else
                {
                    await client.ConnectAsync(host, port, SecureSocketOptions.Auto);
                }

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    _logger.LogInformation("Authenticating as {Username}", username);
                    await client.AuthenticateAsync(username, password);
                }

                _logger.LogInformation("Sending email to {Email}", toEmail);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendWelcomeEmailAsync(string email, string username)
        {
            var subject = "Welcome to Wishera!";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h1 style='color: #f59e0b;'>Welcome to Wishera, {username}!</h1>
                        <p>Thank you for joining Wishera, your ultimate wishlist experience.</p>
                        <p>We're excited to have you on board!</p>
                        <p>Best regards,<br>The Wishera Team</p>
                    </div>
                </body>
                </html>
            ";
            var textBody = $"Welcome to Wishera, {username}!\n\nThank you for joining Wishera, your ultimate wishlist experience.\n\nWe're excited to have you on board!\n\nBest regards,\nThe Wishera Team";

            await SendEmailAsync(email, subject, htmlBody, textBody);
        }

        public async Task SendPasswordResetEmailAsync(string email, string code, string username, bool isMobile = false)
        {
            var subject = "Reset Your Wishera Password";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h1 style='color: #6366f1;'>Password Reset Code</h1>
                        <p>Hi {username},</p>
                        <p>We received a request to reset your password. Use the code below to verify your identity:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='background-color: #6366f1; color: white; padding: 20px; border-radius: 10px; display: inline-block;'>
                                <div style='font-size: 32px; font-weight: bold; letter-spacing: 8px;'>{code}</div>
                            </div>
                        </div>
                        <p style='font-size: 14px; color: #666;'>Enter this code in the app to reset your password.</p>
                        <p style='font-size: 12px; color: #999;'>This code will expire in 15 minutes.</p>
                        <p>If you didn't request this, please ignore this email.</p>
                        <p>Best regards,<br>The Wishera Team</p>
                    </div>
                </body>
                </html>
            ";
            
            var textBody = $"Hi {username},\n\nWe received a request to reset your password.\n\nYour reset code is: {code}\n\nEnter this code in the app to reset your password.\n\nThis code will expire in 15 minutes.\n\nIf you didn't request this, please ignore this email.\n\nBest regards,\nThe Wishera Team";

            await SendEmailAsync(email, subject, htmlBody, textBody);
        }

        public async Task SendEmailVerificationAsync(string email, string token, string username)
        {
            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var verificationLink = $"{frontendUrl}/verify-email?token={token}";

            var subject = "Verify Your Wishera Email Address";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h1 style='color: #10b981;'>Verify Your Email Address</h1>
                        <p>Hi {username},</p>
                        <p>Thank you for registering with Wishera! Please verify your email address by clicking the button below:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{verificationLink}' style='background-color: #10b981; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>Verify Email</a>
                        </div>
                        <p>Or copy and paste this link into your browser:</p>
                        <p style='background-color: #f3f4f6; padding: 10px; border-radius: 5px; word-break: break-all;'>{verificationLink}</p>
                        <p>This verification link will expire in 7 days.</p>
                        <p>If you didn't create an account with Wishera, please ignore this email.</p>
                        <p>Best regards,<br>The Wishera Team</p>
                    </div>
                </body>
                </html>
            ";
            var textBody = $"Hi {username},\n\nThank you for registering with Wishera!\n\nPlease verify your email address by clicking this link: {verificationLink}\n\nThis verification link will expire in 7 days.\n\nIf you didn't create an account with Wishera, please ignore this email.\n\nBest regards,\nThe Wishera Team";

            await SendEmailAsync(email, subject, htmlBody, textBody);
        }
    }
}
