using System.Threading.Tasks;

namespace auth_service.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
        Task SendPasswordResetEmailAsync(string email, string token, string username);
    }

    public class EmailService : IEmailService
    {
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
