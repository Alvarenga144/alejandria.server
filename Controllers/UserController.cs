using Alejandria.Server.Models;
using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Alejandria.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        // Import other services
        private readonly UserService _userService;
        private readonly PostService _postService;
        private readonly NotificationService _notificationService;
        private readonly ProductService _productService;
        private readonly TokenService _tokenService;
        private readonly JwtAuthService _jwtAuthService;
        private readonly IPasswordHasher _passwordHasher; // Service for encrypting passwords
        private readonly IAvatarsBlobConfiguration _avatarsBlobConfiguration; // Service for Azure Blob Storage
        private readonly IWebHostEnvironment _environment;

        public UserController(UserService userService, PostService postService, NotificationService notificationService, ProductService productService,
            TokenService tokenService, JwtAuthService jwtAuthService, IPasswordHasher passwordHasher, IAvatarsBlobConfiguration avatarsBlobConfiguration,
            IWebHostEnvironment environment)
        {
            _userService = userService;
            _postService = postService;
            _notificationService = notificationService;
            _productService = productService;
            _tokenService = tokenService;
            _jwtAuthService = jwtAuthService;
            _passwordHasher = passwordHasher;
            _avatarsBlobConfiguration = avatarsBlobConfiguration;
            _environment = environment;
        }

        #region Login y Logout

        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginUserRequest loginRequest)
        {
            try
            {
                var user = await _userService.GetUserByEmailOrUsernameAsync(loginRequest);

                if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password))
                {
                    return BadRequest(new { error = "Usuario y contraseña invalidos" });
                }

                if (user.EmailConfirmed == true)
                {
                    if (user.RestorePassword == true)
                    {
                        return BadRequest(new { error = $"Se ha solicitado un cambio de contraseña. Por favor, revise su correo electrónico {user.Email} para completar el proceso." });
                    }

                    _tokenService.GenerateTokenAndSetCookie(user.Id.ToString(), Response);

                    return Ok(new
                    {
                        _id = user.Id,
                        name = user.Name,
                        email = user.Email,
                        username = user.Username,
                        bio = user.Bio,
                        profilePic = user.ProfilePic,
                        userType = user.UserType,
                        isVerified = user.IsVerified,
                    });
                }
                else
                {
                    if (string.IsNullOrEmpty(user.VerificationToken))
                    {
                        var verificationToken = Guid.NewGuid().ToString();
                        var updatedUser = new User
                        {
                            EmailConfirmed = false,
                            ConfirmPassword = null,
                            RestorePassword = false,
                            VerificationToken = verificationToken
                        };

                        var updatedResult = await _userService.UpdateUserFieldsAsync(user.Id, updatedUser);

                        // Enviar correo al usuario actualizado
                        string path = Path.Combine(_environment.ContentRootPath, "Templates", "Confirm.html");
                        string content = System.IO.File.ReadAllText(path);
                        string url = $"{this.Request.Scheme}://{this.Request.Host}/verify/{verificationToken}";

                        string htmlBody = string.Format(content, updatedResult.Name, url);

                        CorreoDTO correoDTO = new()
                        {
                            Para = updatedResult.Email,
                            Asunto = "Correo de confirmacion de Alejandría",
                            Contenido = htmlBody
                        };

                        bool enviado = EmailSenderService.Enviar(correoDTO);

                        if (enviado)
                        {
                            return BadRequest(new
                            {
                                error = $"Registro modificado con éxito. Por favor, confirme su cuenta, se le envio un correo a {updatedResult.Email}",
                            });
                        }
                        else
                        {
                            return BadRequest(new { error = $"Falta confirmar su cuenta. Se le envio un correo a {user.Email}" });
                        }
                    }
                    else
                    {
                        return BadRequest(new { error = $"Falta confirmar su cuenta. Se le envio un correo a {user.Email}" });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("Logout")]
        public IActionResult LogoutUser()
        {
            try
            {
                // Set the JWT cookie to an empty value with an immediate expiration
                Response.Cookies.Append("token", "", new CookieOptions { MaxAge = TimeSpan.FromSeconds(1) });

                return Ok(new { message = "User logged out successfully" });
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine("Error in LogoutUser: ", ex.Message);

                // Return an Internal Server Error response
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Get User profile by id

        // This endpoint don't need authentication becase some users can see other users profiles
        // and the user can see his own profile or other profiles too without authentication

        [HttpGet("Profile/{query}")]
        public async Task<IActionResult> GetUser(string query)
        {
            var user = await FindUser(query);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new
            {
                _id = user.Id,
                name = user.Name,
                //email = user.Email,
                username = user.Username,
                userType = user.UserType,
                bio = user.Bio,
                isVerified = user.IsVerified,
                profilePic = user.ProfilePic,
                followers = user.Followers,
                following = user.Following,
                //createdAt = user.CreatedAt,
                //updatedAt = user.UpdatedAt,
            });
        }

        private async Task<User?> FindUser(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            // Intenta buscar por ID
            if (ObjectId.TryParse(query, out ObjectId objectId) && objectId != ObjectId.Empty)
            {
                return await _userService.FindUserByIdAsync(query);
            }

            // Si no se encontró por ID, busca por username
            return await _userService.GetUserByUsernameAsync(query);
        }

        #endregion Get User profile by id

        #region Signup

        [HttpPost("SignUp")]
        public async Task<IActionResult> SignUp(SignupRequest signupRequest)
        {
            try
            {
                var existingUserByUsername = await _userService.GetUserByUsernameAsync(signupRequest.Username);
                var existingUserByEmail = await _userService.GetUserByEmailAsync(signupRequest.Email);

                if (existingUserByUsername != null)
                {
                    return BadRequest(new { error = "Usuario no disponible, use otro" });
                }

                if (existingUserByEmail != null)
                {
                    return BadRequest(new { error = "Correo ocupado en otra cuenta, use otro" });
                }

                var salt = BCrypt.Net.BCrypt.GenerateSalt();
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(signupRequest.Password, salt);

                var verificationToken = Guid.NewGuid().ToString();

                var newUser = new User
                {
                    Name = signupRequest.Name,
                    Username = signupRequest.Username,
                    Email = signupRequest.Email,
                    Password = hashedPassword,
                    RestorePassword = false,
                    EmailConfirmed = false,
                    UserType = signupRequest.UserType,
                    ProfilePic = "",
                    Bio = "",
                    IsVerified = false,
                    IsFrozen = false,
                    VerificationToken = verificationToken
                };

                string path = Path.Combine(_environment.ContentRootPath, "Templates", "Confirm.html");
                string content = System.IO.File.ReadAllText(path);
                string url = $"{this.Request.Scheme}://{this.Request.Host}/verify/{verificationToken}";

                string htmlBody = string.Format(content, newUser.Name, url);

                CorreoDTO correoDTO = new()
                {
                    Para = newUser.Email,
                    Asunto = "Correo de confirmacion de Alejandría",
                    Contenido = htmlBody
                };

                bool enviado = EmailSenderService.Enviar(correoDTO);

                if (enviado)
                {
                    await _userService.CreateUserAsync(newUser);
                    return Ok(new
                    {
                        _id = newUser.Id,
                        name = newUser.Name,
                        email = newUser.Email,
                        username = newUser.Username,
                        bio = newUser.Bio,
                        profilePic = newUser.ProfilePic,
                        userType = newUser.UserType,
                        isVerified = newUser.IsVerified,
                    });
                }
                else
                {
                    return BadRequest(new { error = "No se pudo crear el usuario, intentalo de nuevo" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Follow/Unfollow User

        [HttpPost("Follow/{id}")]
        public async Task<IActionResult> FollowUnFollowUser(string id)
        {
            var token = HttpContext.Request.Cookies["token"];
            var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);

            if (errorMessage != null)
            {
                return StatusCode(500, new { error = errorMessage });
            }

            if (currentUser == null || id == currentUser.Id)
            {
                return BadRequest(new { error = "You cannot follow/unfollow yourself or User not found" });
            }

            try
            {
                var isFollowing = currentUser.Following.Contains(id);
                if (isFollowing)
                {
                    await _userService.UnfollowUserAsync(currentUser.Id, id);
                    return Ok(new { message = "User unfollowed successfully" });
                }
                else
                {
                    await _userService.FollowUserAsync(currentUser.Id, id);
                    // Crear y guardar la notificación para el usuario seguido
                    var notification = new Notifications
                    {
                        UserId = id, // ID del usuario que es seguido
                        ActorId = currentUser.Id, // ID del usuario que sigue
                        Message = $"ha comenzado a seguirte",
                        Status = NotificationStatus.Unread,
                        Type = NotificationType.Follow,
                        Action = NotificationAction.Followed
                    };
                    await _notificationService.CreateNotificationAsync(notification);
                    return Ok(new { message = "User followed successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Update User profile

        [HttpPut("UpdateUser/{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);

                if (errorMessage != null)
                {
                    return StatusCode(500, new { error = errorMessage });
                }

                if (currentUser == null || currentUser.Id != userId)
                {
                    return BadRequest(new { error = "Error de autorizacion o usuario no encontrado" });
                }

                var existingUserByEmail = await _userService.GetUserByEmailAsync(request.Email);
                var existingUserByUsername = await _userService.GetUserByUsernameAsync(request.Username);

                if (existingUserByEmail != null && existingUserByEmail.Id != currentUser.Id)
                {
                    return BadRequest(new { error = "Correo ocupado en otra cuenta, use otro" });
                }

                if (existingUserByUsername != null && existingUserByUsername.Id != currentUser.Id)
                {
                    return BadRequest(new { error = "Usuario no disponible, use otro" });
                }

                bool isUrl = Uri.TryCreate(request.ProfilePic, UriKind.Absolute, out var uriResult)
                     && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                if (!string.IsNullOrEmpty(request.ProfilePic) && !isUrl)
                {
                    if (!string.IsNullOrEmpty(currentUser.ProfilePic))
                    {
                        _avatarsBlobConfiguration.DeleteBlob(currentUser.ProfilePic, "avatars-images");
                    }

                    string imageUrl = await _avatarsBlobConfiguration.UploadBlobAsync(request.ProfilePic, "avatars-images");
                    currentUser.ProfilePic = imageUrl;
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    currentUser.Name = request.Name;
                }

                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    currentUser.Email = request.Email;
                }

                if (!string.IsNullOrWhiteSpace(request.Username))
                {
                    currentUser.Username = request.Username;
                }

                if (!string.IsNullOrWhiteSpace(request.Bio))
                {
                    currentUser.Bio = request.Bio;
                }

                if (!string.IsNullOrWhiteSpace(request.UserType))
                {
                    currentUser.UserType = request.UserType;
                }

                await _userService.UpdateUserAsync(currentUser);

                if (!string.IsNullOrWhiteSpace(request.Username) || !string.IsNullOrEmpty(currentUser.ProfilePic))
                {
                    // Encuentra todos los posts que contienen respuestas del usuario
                    var postsToUpdate = await _postService.FindPostsWithRepliesByUser(userId);
                    foreach (var post in postsToUpdate)
                    {
                        foreach (var reply in post.Replies.Where(r => r.UserId == userId))
                        {
                            // Actualiza la información del usuario en las respuestas
                            reply.Username = currentUser.Username;
                            reply.UserProfilePic = currentUser.ProfilePic;
                        }

                        // Guarda los cambios en cada post
                        await _postService.UpdatePostAsync(post);
                    }

                    // Encuentra todos los products que contienen respuestas del usuario
                    var productsToUpdate = await _productService.FindProducsWithRepliesByUser(userId);
                    foreach (var product in productsToUpdate)
                    {
                        foreach (var reply in product.Replies.Where(r => r.UserId == userId))
                        {
                            // Actualiza la información del usuario en las respuestas
                            reply.Username = currentUser.Username;
                            reply.UserProfilePic = currentUser.ProfilePic;
                        }

                        // Guarda los cambios en cada productos
                        await _productService.UpdateProductAsync(product);
                    }
                }

                return Ok(new
                {
                    _id = currentUser.Id,
                    name = currentUser.Name,
                    email = currentUser.Email,
                    username = currentUser.Username,
                    bio = currentUser.Bio,
                    profilePic = currentUser.ProfilePic,
                    userType = currentUser.UserType,
                    isVerified = currentUser.IsVerified,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion Update User profile

        #region Suggested Users

        [HttpGet("Suggested")]
        public async Task<IActionResult> GetSuggestedUsers()
        {
            try
            {
                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);

                if (errorMessage != null)
                {
                    return StatusCode(500, new { error = errorMessage });
                }

                if (currentUser == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado o error de autorización" });
                }

                var suggestedUsers = await _userService.GetSuggestedUsers(currentUser.Id);

                var formattedUsers = suggestedUsers.Select(user => new
                {
                    _id = user.Id,
                    name = user.Name,
                    //email = user.Email,
                    username = user.Username,
                    userType = user.UserType,
                    bio = user.Bio,
                    isVerified = user.IsVerified,
                    profilePic = user.ProfilePic,
                    followers = user.Followers,
                    following = user.Following,
                    //createdAt = user.CreatedAt,
                    //updatedAt = user.UpdatedAt,
                }).ToList();

                return Ok(formattedUsers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region User search

        [HttpGet("Search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return BadRequest(new { error = "El término de búsqueda es requerido." });
                }

                var searchResult = await _userService.SearchUsersByTermAsync(term);
                var formattedResult = searchResult.Select(user => new
                {
                    _id = user.Id,
                    name = user.Name,
                    username = user.Username,
                    profilePic = user.ProfilePic,
                    isVerified = user.IsVerified,
                }).ToList();

                return Ok(formattedResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion 

        #region Confirm account

        [HttpGet("ConfirmEmail/{token}")]
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            var result = await _userService.ConfirmEmailAsync(token);

            if (result)
            {
                return Ok(new { message = "Email confirmed successfully." });
            }
            else
            {
                return BadRequest(new { error = "Invalid or expired token." });
            }
        }

        #endregion 

        #region Restore password

        [HttpPost("RestorePassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel mailOrUsername)
        {
            try
            {
                var user = await _userService.GetUserByEmailAsync(mailOrUsername.Email);

                if (user != null)
                {
                    string verificationToken = string.IsNullOrEmpty(user.VerificationToken)? Guid.NewGuid().ToString(): user.VerificationToken;
                    await _userService.UpdateUserRestorePasswordAsync(user.Id, true, user.EmailConfirmed, verificationToken);

                    // Generar la URL de restablecimiento de contraseña
                    string path = Path.Combine(_environment.ContentRootPath, "Templates", "Restore.html");
                    string content = System.IO.File.ReadAllText(path);
                    string url = $"{this.Request.Scheme}://{this.Request.Host}/updateNewKeys/{verificationToken}";

                    string htmlBody = string.Format(content, user.Name, url);

                    CorreoDTO correoDTO = new CorreoDTO()
                    {
                        Para = user.Email,
                        Asunto = "Restablecer contraseña",
                        Contenido = htmlBody
                    };

                    bool enviado = EmailSenderService.Enviar(correoDTO);

                    if (enviado)
                    {
                        return Ok(new { message = "Se ha enviado un enlace para restablecer la contraseña a su correo electrónico." });
                    }
                    else
                    {
                        return StatusCode(500, new { error = "No se pudo enviar el correo electrónico." });
                    }
                }
                else
                {
                    return BadRequest(new { error = "No se encontró un usuario con ese correo electrónico." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion 

        #region updatePassword

        [HttpPost("UpdatePassword")]
        public async Task<IActionResult> ActualizarContraseña([FromBody] UpdatePasswordTkn updatePass)
        {
            try
            {
                if (updatePass.Password != updatePass.ConfirmPassword)
                {
                    return BadRequest(new { error = "Las contraseñas no coinciden" });
                }

                var user = await _userService.FindUserByVTokenAsync(updatePass.VerificationToken);

                if (user == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado" });
                }

                var salt = BCrypt.Net.BCrypt.GenerateSalt();
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(updatePass.Password, salt);
                string verificationToken = Guid.NewGuid().ToString();

                var respuesta = await _userService.UpdateUserPasswordAsync(user.Id, hashedPassword);
                await _userService.UpdateUserRestorePasswordAsync(user.Id, false, true, verificationToken);

                if (!respuesta)
                {
                    return BadRequest(new { error = "No se pudo actualizar la contraseña" });
                }

                return Ok(new { mensaje = "Contraseña actualizada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion 
    }
}