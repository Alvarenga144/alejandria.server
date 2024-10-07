using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Alejandria.Server.Services
{
    public class TokenService
    {
        private readonly string _appSecretKey;

        public TokenService(IConfiguration antiforgery) => _appSecretKey = antiforgery.GetSection("Settings").GetSection("SecretKey").ToString();

        // Create a JWT token for the user and return it

        public string GenerateTokenAndSetCookie(string userId, HttpResponse response)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_appSecretKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("userId", userId) }),
                Expires = DateTime.UtcNow.AddDays(15),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // cookie configuration
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                MaxAge = TimeSpan.FromDays(15),
                SameSite = SameSiteMode.Strict
            };

            response.Cookies.Append("token", tokenString, cookieOptions);

            return tokenString;
        }
    }
}
