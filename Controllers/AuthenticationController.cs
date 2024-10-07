using Alejandria.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Alejandria.Server.Controllers
{
    /*
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly string _appSecretKey;

        public AuthenticationController(IConfiguration config)
        {
            _appSecretKey = config.GetSection("Settings").GetSection("SecretKey").ToString();
        }

        [HttpPost]
        [Route("ValidateAuth")]
        public IActionResult ValidateAuthentication([FromBody] User userRequest)
        {
            if (userRequest.Username == "juan" && userRequest.Password == "")
            {
                var KeyBytes = Encoding.UTF8.GetBytes(_appSecretKey);
                var claims = new ClaimsIdentity();

                claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, userRequest.Username));

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = claims,
                    Expires = DateTime.UtcNow.AddDays(15),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(KeyBytes), SecurityAlgorithms.HmacSha256Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenConfig = tokenHandler.CreateToken(tokenDescriptor);

                string tokenCreated = tokenHandler.WriteToken(tokenConfig);

                return StatusCode(StatusCodes.Status200OK, new { token = tokenCreated });
            }
            else
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { token = "" });
            }
        }
    }
    */
}
