using BusinessLayer.Services.UserService.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BusinessLayer.Services.UserService.Implementations
{
    public class AuthenticatedUserService : IAuthenticatedUserService
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public AuthenticatedUserService(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public int GetAuthenticatedUserId()
        {
            var httpContext = httpContextAccessor.HttpContext;
            var nameIdentifier = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(nameIdentifier))
            {
                throw new UnauthorizedAccessException("Missing NameIdentifier claim");
            }
            return int.Parse(nameIdentifier);
        }

        public string GetAuthenticatedUsername()
        {
            var httpContext = httpContextAccessor.HttpContext;
            var username = httpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username))
            {
                throw new UnauthorizedAccessException("Missing Name claim");
            }
            return username;
        }
    }
}
