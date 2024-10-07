using System.Text.RegularExpressions;
using Alejandria.Server.Models;
using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alejandria.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        // Import the services
        private readonly UserService _userService;
        private readonly PostService _postService;
        private readonly NotificationService _notificationService;
        private readonly DocumentService _documentService;
        private readonly JwtAuthService _jwtAuthService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IPostContentBlobConfiguration _postContentBlobConfiguration;
        private string mentionPattern = @"(^|[^@\w])@(\w{1,15})\b(?!@[\w.]+)";

        public PostController(UserService userService, PostService postService, NotificationService notificationService, DocumentService documentService,
            JwtAuthService jwtAuthService, IPasswordHasher passwordHasher, IPostContentBlobConfiguration postContentBlobConfiguration)
        {
            _userService = userService;
            _postService = postService;
            _notificationService = notificationService;
            _documentService = documentService;
            _jwtAuthService = jwtAuthService;
            _passwordHasher = passwordHasher;
            _postContentBlobConfiguration = postContentBlobConfiguration;
        }

        #region Crear post

        [HttpPost("CreatePost")]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PostedBy) || string.IsNullOrEmpty(request.Text))
                {
                    return BadRequest(new { error = "PostedBy and text fields are required" });
                }

                var user = await _userService.FindUserByIdAsync(request.PostedBy);
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null || currentUser.Id != request.PostedBy)
                {
                    return Unauthorized(new { error = "Unauthorized to create post" });
                }

                const int maxLength = 280;
                if (request.Text.Length > maxLength)
                {
                    return BadRequest(new { error = $"Text must be less than {maxLength} characters" });
                }

                string img = request.Img;
                if (!string.IsNullOrEmpty(img))
                {
                    img = await _postContentBlobConfiguration.UploadBlobAsync(img, "users-post-images");
                }
                else
                {
                    img = "";
                }

                var newPost = new Post
                {
                    PostedBy = request.PostedBy,
                    Text = request.Text,
                    Img = img,
                    UserAutor = user.Username,
                    PostResume = $"{request.Text} {img} - Publicado por {user.Username}, fecha {DateTime.UtcNow}",
                };

                await _postService.SavePostAsync(newPost);

                // Notificar menciones en post
                var mentionRegex = new Regex(this.mentionPattern);
                List<string> mentions = [];
                foreach (Match match in mentionRegex.Matches(request.Text))
                {
                    var mention = match.Value.Replace("@", "").Trim();
                    if(currentUser.Username != mention){
                        var mentionedUser = await _userService.GetUserByUsernameAsync(mention);
                        if(mentionedUser != null){
                            var notification = new Notifications
                            {
                                UserId = mentionedUser.Id,
                                PostId = newPost.Id,
                                ActorId = currentUser.Id,
                                Message = $"te ha mencionado en su aporte",
                                Status = NotificationStatus.Unread,
                                Type = NotificationType.Mention,
                                Action = NotificationAction.Mentioned
                            };
                            await _notificationService.CreateNotificationAsync(notification);
                        }
                    }
                }

                return StatusCode(201, newPost);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Seleccionar un post

        [HttpGet("{PostId}")]
        public async Task<IActionResult> GetPost(string PostId)
        {
            try
            {
                var post = await _postService.FindPostByIdAsync(PostId);
                if (post == null)
                {
                    return NotFound(new { error = "Post no encontrado" });
                }

                return Ok(post);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Like / Unlike post

        [HttpPut("likeUnlike/{PostId}")]
        public async Task<IActionResult> LikeUnlikePost(string PostId)
        {
            try
            {
                var post = await _postService.FindPostByIdAsync(PostId);
                if (post == null)
                {
                    return NotFound(new { error = "Post no encontrado" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null)
                {
                    return Unauthorized(new { error = "No autorizado" });
                }

                var userId = currentUser.Id;
                var userLikedPost = post.Likes.Contains(userId);

                if (userLikedPost)
                {
                    // Quitar like al post
                    await _postService.UnlikePostAsync(PostId, userId);
                    return Ok(new { message = "Post unliked successfully" });
                }
                else
                {
                    // Dar like al post
                    await _postService.LikePostAsync(PostId, userId);

                    // Crear y guardar la notificación solo si el usuario en sesión no es el creador del post
                    if (userId != post.PostedBy)
                    {
                        // Crear y guardar la notificación
                        var notification = new Notifications
                        {
                            UserId = post.PostedBy,
                            PostId = PostId,
                            ActorId = userId,
                            Message = $"le ha gustado tu aporte",
                            Status = NotificationStatus.Unread,
                            Type = NotificationType.Like,
                            Action = NotificationAction.Liked
                        };
                        await _notificationService.CreateNotificationAsync(notification);
                    }
                    return Ok(new { message = "Post liked successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Respuesta a un post

        [HttpPut("Reply/{PostId}")]
        public async Task<IActionResult> ReplyToPost(string PostId, [FromBody] ReplyToPostRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Text))
                {
                    return BadRequest(new { error = "Los campos son requeridos" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                var post = await _postService.FindPostByIdAsync(PostId);
                if (post == null)
                {
                    return NotFound(new { error = "Post no encontrado" });
                }

                var reply = new Reply
                {
                    UserId = currentUser.Id,
                    Text = request.Text,
                    UserProfilePic = currentUser.ProfilePic,
                    Username = currentUser.Username
                };

                await _postService.AddReplyToPostAsync(PostId, reply);

                // Notificar menciones en post
                var mentionRegex = new Regex(this.mentionPattern);
                List<string> mentions = [];
                foreach (Match match in mentionRegex.Matches(request.Text))
                {
                    var mention = match.Value.Replace("@", "").Trim();
                    if(currentUser.Username != mention){
                        var mentionedUser = await _userService.GetUserByUsernameAsync(mention);
                        if(mentionedUser != null){
                            var notification = new Notifications
                            {
                                UserId = mentionedUser.Id,
                                PostId = PostId,
                                ActorId = currentUser.Id,
                                Message = $"te ha mencionado en su comentario",
                                Status = NotificationStatus.Unread,
                                Type = NotificationType.Mention,
                                Action = NotificationAction.Mentioned
                            };
                            await _notificationService.CreateNotificationAsync(notification);
                        }
                    }
                }

                // Comprobar si el usuario actual no es el creador del post y crear la notificación
                if (currentUser.Id != post.PostedBy)
                {
                    var notification = new Notifications
                    {
                        UserId = post.PostedBy,
                        PostId = PostId,
                        ActorId = currentUser.Id,
                        Message = $"ha comentado tu aporte",
                        Status = NotificationStatus.Unread,
                        Type = NotificationType.Comment,
                        Action = NotificationAction.Commented
                    };
                    await _notificationService.CreateNotificationAsync(notification);
                }

                return Ok(reply);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Feed de post

        [HttpGet("Feed")]
        public async Task<IActionResult> GetFeedPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null)
                {
                    return Unauthorized(new { error = "No autorizado" });
                }

                var user = await _userService.FindUserByIdAsync(currentUser.Id);
                if (user == null)
                {
                    return NotFound(new { error = "Usuario no encontrado" });
                }

                var following = user.Following;
                var feedPosts = await _postService.GetFeedPostsAsync(following, user.Id, page, pageSize);

                return Ok(feedPosts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Lista de post por usuario

        [HttpGet("UserPosts/{username}")]
        public async Task<IActionResult> GetUserPosts(string username)
        {
            try
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound(new { error = "Usuario no encontrado" });
                }

                var posts = await _postService.GetUserPostsAsync(user.Id);

                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Eliminar un post

        [HttpDelete("DeletePost/{PostId}")]
        public async Task<IActionResult> DeletePost(string PostId)
        {
            try
            {
                var post = await _postService.FindPostByIdAsync(PostId);
                if (post == null)
                {
                    return NotFound(new { error = "Post no encontrado" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null || currentUser.Id != post.PostedBy)
                {
                    return Unauthorized(new { error = "Unauthorized to delete post" });
                }

                if (!string.IsNullOrEmpty(post.Img))
                {
                    _postContentBlobConfiguration.DeleteBlob(post.Img, "users-post-images");
                }

                await _documentService.RemoveDocumentFromIndex(PostId);
                await _postService.DeletePostAsync(PostId);

                return Ok(new { message = "Post deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion
    }
}