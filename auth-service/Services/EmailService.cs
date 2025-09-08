<<<<<<< HEAD
using System.Threading.Tasks;

namespace WishlistApp.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
        Task SendPasswordResetEmailAsync(string email, string token, string username);
=======
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace auth_service.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string username);
        Task SendWelcomeEmailAsync(string toEmail, string username);
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
    }

    public class EmailService : IEmailService
    {
<<<<<<< HEAD
        public Task SendWelcomeEmailAsync(string email, string username)
        {
            // No-op stub for development
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string email, string token, string username)
        {
            // No-op stub for development
            return Task.CompletedTask;
        }
    }
}


=======
        private readonly IConfiguration _configuration;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["Email:Username"] ?? "";
            _smtpPassword = _configuration["Email:Password"] ?? "";
            _fromEmail = _configuration["Email:FromEmail"] ?? "";
            _fromName = _configuration["Email:FromName"] ?? "Wishlist App";
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string username)
        {
            var subject = "Reset Your Password - Wishlist App";
            var resetLink = $"http://localhost:3000/reset-password?token={resetToken}";
            
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px;'>Wishlist App</h1>
                            <p style='color: white; margin: 10px 0 0 0; font-size: 16px;'>Password Reset Request</p>
                        </div>
                        
                        <div style='background: white; padding: 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                            <h2 style='color: #333; margin-bottom: 20px;'>Hello {username},</h2>
                            
                            <p style='margin-bottom: 20px;'>We received a request to reset your password for your Wishlist App account.</p>
                            
                            <p style='margin-bottom: 30px;'>Click the button below to reset your password:</p>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{resetLink}' 
                                   style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                          color: white; 
                                          padding: 15px 30px; 
                                          text-decoration: none; 
                                          border-radius: 25px; 
                                          display: inline-block; 
                                          font-weight: bold;
                                          font-size: 16px;'>
                                    Reset Password
                                </a>
                            </div>
                            
                            <p style='margin-bottom: 20px; font-size: 14px; color: #666;'>
                                If the button doesn't work, copy and paste this link into your browser:
                            </p>
                            
                            <p style='margin-bottom: 30px; font-size: 14px; color: #667eea; word-break: break-all;'>
                                {resetLink}
                            </p>
                            
                            <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 30px 0;'>
                                <h3 style='color: #333; margin-top: 0; font-size: 16px;'>Important:</h3>
                                <ul style='margin: 0; padding-left: 20px; color: #666;'>
                                    <li>This link will expire in 24 hours</li>
                                    <li>If you didn't request this password reset, please ignore this email</li>
                                    <li>For security, this link can only be used once</li>
                                </ul>
                            </div>
                            
                            <p style='margin-bottom: 10px;'>Best regards,</p>
                            <p style='margin: 0; color: #667eea; font-weight: bold;'>The Wishlist App Team</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                            <p>This email was sent to {toEmail}</p>
                            <p>If you have any questions, please contact our support team</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string username)
        {
            var subject = "Welcome to Wishlist App!";
            
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px;'>Welcome to Wishlist App!</h1>
                            <p style='color: white; margin: 10px 0 0 0; font-size: 16px;'>Your account has been created successfully</p>
                        </div>
                        
                        <div style='background: white; padding: 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                            <h2 style='color: #333; margin-bottom: 20px;'>Hello {username},</h2>
                            
                            <p style='margin-bottom: 20px;'>Welcome to Wishlist App! We're excited to have you on board.</p>
                            
                            <p style='margin-bottom: 20px;'>With Wishlist App, you can:</p>
                            
                            <ul style='margin-bottom: 30px; color: #666;'>
                                <li>Create and manage your wishlists</li>
                                <li>Share your wishlists with friends and family</li>
                                <li>Discover gifts from other users</li>
                                <li>Connect with friends and see their wishlists</li>
                            </ul>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='http://localhost:3000' 
                                   style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                          color: white; 
                                          padding: 15px 30px; 
                                          text-decoration: none; 
                                          border-radius: 25px; 
                                          display: inline-block; 
                                          font-weight: bold;
                                          font-size: 16px;'>
                                    Get Started
                                </a>
                            </div>
                            
                            <p style='margin-bottom: 10px;'>Best regards,</p>
                            <p style='margin: 0; color: #667eea; font-weight: bold;'>The Wishlist App Team</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                            <p>This email was sent to {toEmail}</p>
                            <p>If you have any questions, please contact our support team</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Validate configuration
                if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
                {
                    throw new InvalidOperationException("Email configuration is incomplete. Please check your appsettings.json");
                }

                using var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    Timeout = 10000 // 10 second timeout
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                Console.WriteLine($"Email sent successfully to {toEmail}");
            }
            catch (SmtpException ex)
            {
                Console.WriteLine($"SMTP Error: {ex.StatusCode} - {ex.Message}");
                throw new InvalidOperationException($"Email authentication failed. Please check your Gmail app password configuration. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                throw new InvalidOperationException($"Failed to send email: {ex.Message}");
            }
        }
    }
}
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
