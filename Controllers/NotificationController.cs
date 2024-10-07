using Alejandria.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alejandria.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        // Import the services
        private readonly UserService _userService;
        private readonly PostService _postService;
        private readonly NotificationService _notificationService;
        private readonly JwtAuthService _jwtAuthService;

        public NotificationController(UserService userService, PostService postService, NotificationService notificationService, JwtAuthService jwtAuthService)
        {
            _userService = userService;
            _postService = postService;
            _notificationService = notificationService;
            _jwtAuthService = jwtAuthService;
        }

        [HttpGet("{notificationId}")]
        public async Task<IActionResult> GetNotification(string notificationId)
        {
            var notification = await _notificationService.GetNotificationByIdAsync(notificationId);
            if (notification == null)
            {
                return NotFound(new { error = "Notification not found" });
            }
            return Ok(notification);
        }

        [HttpGet("UserNotifications/{username}")]
        public async Task<IActionResult> GetUserNotifications(string username)
        {
            try
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                var notifications = await _notificationService.GetNotificationsForUserAsync(user.Id);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("markAsRead/{notificationId}")]
        public async Task<IActionResult> MarkNotificationAsRead(string notificationId)
        {
            var notification = await _notificationService.GetNotificationByIdAsync(notificationId);
            if (notification == null)
            {
                return NotFound(new { error = "Notification not found" });
            }

            await _notificationService.UpdateNotificationStatusToReadAsync(notificationId);
            return Ok(new { message = "Notification marked as read" });
        }
    }
}
