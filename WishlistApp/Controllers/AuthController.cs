using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using MongoDB.Driver;
using WishlistApp.Models;
using WishlistApp.Services;

namespace WishlistApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(MongoDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // Check if username is already taken
            var existingUser = await _context.Users.Find(u => u.Username == user.Username).FirstOrDefaultAsync();
            if (existingUser != null)
                return BadRequest(new { message = "Username already exists" });

            // Assign default role and hash password
            user.Id = Guid.NewGuid().ToString(); // Generate unique ID
            user.Role = "user"; // Default role
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            await _context.Users.InsertOneAsync(user);
            return Ok(new { user.Id, user.Role });
        }

        [HttpPost("login")]
public async Task<IActionResult> Login([FromBody] User loginUser)
{
    var existingUser = await _context.Users.Find(u => u.Username == loginUser.Username).FirstOrDefaultAsync();
    if (existingUser == null || !BCrypt.Net.BCrypt.Verify(loginUser.PasswordHash, existingUser.PasswordHash))
        return Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, existingUser.Id),
            new Claim(ClaimTypes.GivenName, existingUser.Username), // ✅ Store Username
            new Claim(ClaimTypes.Role, existingUser.Role)
        }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Ok(new 
    { 
        Token = tokenString, 
        UserId = existingUser.Id,  // ✅ Include UserId in response
        Username = existingUser.Username 
    });
}

 [HttpGet("user/{wishlistId}")]
        public async Task<IActionResult> GetUserByWishlistId(string wishlistId)
        {
            // Find the user by their wishlistId (which is the same as their userId)
            var user = await _context.Users.Find(u => u.Id == wishlistId).FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new { username = user.Username });
        }

        [HttpPost("set-friend-role")]
        public async Task<IActionResult> SetFriendRole([FromBody] string userId)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update.Set(u => u.Role, "friend");

            var result = await _context.Users.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "User role updated to friend" });
        }
    }
}
