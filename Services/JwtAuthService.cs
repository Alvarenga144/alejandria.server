using Alejandria.Server.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Alejandria.Server.Services
{
    public class JwtAuthService
    {
        private readonly MongoDBSettings _mongoDbSettings;
        private readonly string _appSecretKey;

        public JwtAuthService(IOptions<MongoDBSettings> settings, IConfiguration antiforgery)
        {
            _mongoDbSettings = settings.Value;
            _appSecretKey = antiforgery.GetSection("Settings").GetSection("SecretKey").ToString();
        }

        // verify the token and return the user
        public async Task<(User? user, string? errorMessage)> ValidateTokenAndGetUser(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return (null, "Unauthorized: No token provided");
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_appSecretKey);
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = jwtToken.Claims.First(x => x.Type == "userId").Value;

                var client = new MongoClient(_mongoDbSettings.ConnectionString);
                var database = client.GetDatabase(_mongoDbSettings.DatabaseName);
                var usersCollection = database.GetCollection<User>(_mongoDbSettings.UserCollectionName);

                var user = await usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
                return (user, null);
            }
            catch (Exception ex)
            {
                return (null, $"Internal Server Error: {ex.Message}");
            }
        }
    }
}
