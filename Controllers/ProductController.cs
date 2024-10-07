using Alejandria.Server.Models;
using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alejandria.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        // Import the services
        private readonly UserService _userService;
        private readonly ProductService _productService;
        private readonly NotificationService _notificationService;
        private readonly DocumentService _documentService;
        private readonly JwtAuthService _jwtAuthService;
        private readonly IPostContentBlobConfiguration _postContentBlobConfiguration;

        public ProductController(UserService userService, ProductService productService, NotificationService notificationService, DocumentService documentService,
            JwtAuthService jwtAuthService, IPostContentBlobConfiguration postContentBlobConfiguration)
        {
            _userService = userService;
            _productService = productService;
            _notificationService = notificationService;
            _documentService = documentService;
            _jwtAuthService = jwtAuthService;
            _postContentBlobConfiguration = postContentBlobConfiguration;
        }

        #region Crear producto

        [HttpPost("CreateProduct")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PostedBy) || string.IsNullOrEmpty(request.Category) || string.IsNullOrEmpty(request.Title)
                    || string.IsNullOrEmpty(request.DetailedDescription) || string.IsNullOrEmpty(request.Title) || request.Price <= 0)
                {
                    return BadRequest(new { error = "Todos los campos son requeridos" });
                }

                var user = await _userService.FindUserByIdAsync(request.PostedBy);
                if (user == null)
                {
                    return NotFound(new { error = "Usuario no encontrado" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null || currentUser.Id != request.PostedBy)
                {
                    return Unauthorized(new { error = "No authorizado para publicar" });
                }

                const int maxLength = 360;
                if (request.DetailedDescription.Length > maxLength)
                {
                    return BadRequest(new { error = $"El texto debería tener menos de {maxLength} caracteres" });
                }

                List<string> imgs = new List<string>();
                foreach (var img in request.Imgs)
                {
                    if (!string.IsNullOrEmpty(img))
                    {
                        var uploadedImg = await _postContentBlobConfiguration.UploadBlobAsync(img, "products-images");
                        imgs.Add(uploadedImg);
                    }
                }

                var newProductPost = new Product
                {
                    PostedBy = request.PostedBy,
                    Category = request.Category,
                    Title = request.Title,
                    DetailedDescription = request.DetailedDescription,
                    Imgs = imgs,
                    Price = request.Price,
                    UserAutor = user.Username,
                    ProductResume = $"Categoria: {request.Category}. Titulo: {request.Title}. Descripcion: {request.DetailedDescription}. Precio: ${request.Price}. - Publicado por {user.Username}, fecha {DateTime.UtcNow}",
                    Rating = 0,
                    SupportsInAppPayment = false
                };

                await _productService.SaveProductPostAsync(newProductPost);

                return StatusCode(201, newProductPost);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Seleccionar un post

        [HttpGet("{ProductId}")]
        public async Task<IActionResult> GetProduct(string ProductId)
        {
            try
            {
                var products = await _productService.FindProductByIdAsync(ProductId);
                if (products == null)
                {
                    return NotFound(new { error = "Producto o servicio no encontrado" });
                }

                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Like / Unlike producto

        [HttpPut("likeUnlike/{ProductId}")]
        public async Task<IActionResult> LikeUnlikeProduct(string ProductId)
        {
            try
            {
                var product = await _productService.FindProductByIdAsync(ProductId);
                if (product == null)
                {
                    return NotFound(new { error = "Producto o servicio no encontrado" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null)
                {
                    return Unauthorized(new { error = "No autorizado" });
                }

                var userId = currentUser.Id;
                var userLikedPost = product.Likes.Contains(userId);

                if (userLikedPost)
                {
                    // Quitar like al producto
                    await _productService.UnlikeProductAsync(ProductId, userId);
                    return Ok(new { message = "Product unliked successfully" });
                }
                else
                {
                    // Dar like al producto
                    await _productService.LikeProductAsync(ProductId, userId);

                    // Crear y guardar la notificación solo si el usuario en sesión no es el creador del post
                    if (userId != product.PostedBy)
                    {
                        // Crear y guardar la notificación
                        var notification = new Notifications
                        {
                            UserId = product.PostedBy,
                            PostId = ProductId,
                            ActorId = userId,
                            Message = $"le ha gustado tu producto",
                            Status = NotificationStatus.Unread,
                            Type = NotificationType.Like,
                            Action = NotificationAction.Liked
                        };
                        await _notificationService.CreateNotificationAsync(notification);
                    }
                    return Ok(new { message = "Product liked successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Respuesta a un producto

        [HttpPut("Reply/{ProductId}")]
        public async Task<IActionResult> ReplyToProduct(string ProductId, [FromBody] ReplyToProductRequest request)
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

                var post = await _productService.FindProductByIdAsync(ProductId);
                if (post == null)
                {
                    return NotFound(new { error = "Producto no encontrado" });
                }

                var reply = new Reply
                {
                    UserId = currentUser.Id,
                    Text = request.Text,
                    UserProfilePic = currentUser.ProfilePic,
                    Username = currentUser.Username
                };

                await _productService.AddReplyToProductAsync(ProductId, reply);

                // Comprobar si el usuario actual no es el creador del post y crear la notificación
                if (currentUser.Id != post.PostedBy)
                {
                    var notification = new Notifications
                    {
                        UserId = post.PostedBy,
                        PostId = ProductId,
                        ActorId = currentUser.Id,
                        Message = $"ha comentado sobre tu producto", // mensaje igual para mostrar redirection en notificaciones
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

        #region Feed de products

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
                var feedPosts = await _productService.GetFeedProductsAsync(page, pageSize);

                return Ok(feedPosts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Lista de productos por usuario

        [HttpGet("UserProducts/{username}")]
        public async Task<IActionResult> GetUserProducts(string username)
        {
            try
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound(new { error = "Usuario no encontrado" });
                }

                var products = await _productService.GetUserProductsAsync(user.Id);

                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Eliminar un post

        [HttpDelete("DeleteProduct/{ProductId}")]
        public async Task<IActionResult> DeleteProduct(string ProductId)
        {
            try
            {
                var product = await _productService.FindProductByIdAsync(ProductId);
                if (product == null)
                {
                    return NotFound(new { error = "Producto no encontrado" });
                }

                var token = HttpContext.Request.Cookies["token"];
                var (currentUser, errorMessage) = await _jwtAuthService.ValidateTokenAndGetUser(token);
                if (currentUser == null || currentUser.Id != product.PostedBy)
                {
                    return Unauthorized(new { error = "Unauthorized to delete post" });
                }

                if (product.Imgs != null && product.Imgs.Count > 0)
                {
                    foreach (var img in product.Imgs)
                    {
                        if (!string.IsNullOrEmpty(img))
                        {
                            _postContentBlobConfiguration.DeleteBlob(img, "products-images");
                        }
                    }
                }

                await _documentService.RemoveDocumentFromIndex(ProductId);
                await _productService.DeleteProductAsync(ProductId);

                return Ok(new { message = "Producto deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion
    }
}
