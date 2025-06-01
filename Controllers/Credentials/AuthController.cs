using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KuwagoAPI.Controllers.Credentials
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterUserAsync(request);

            if (!result.Success && result.Message.Contains("already"))
                return Conflict(result);

            return StatusCode(result.StatusCode, result);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginUserAsync(request.Email, request.Password);

            if (!result.Success)
                return StatusCode(result.StatusCode, new StatusResponse
                {
                    Success = false,
                    Message = result.Message,
                    StatusCode = result.StatusCode
                });

            // Set a cookie with 10-minute expiration
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, //Set to false
                Expires = DateTime.UtcNow.AddMinutes(10),
                SameSite = SameSiteMode.Strict //SameSiteMode.None
            };

            Response.Cookies.Append("session_token", result.Message, cookieOptions);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "Login successful!",
                StatusCode = 200
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            if (Request.Cookies["session_token"] != null)
            {
                Response.Cookies.Delete("session_token");
                return Ok(new StatusResponse
                {
                    Success = true,
                    Message = "User logged out successfully.",
                    StatusCode = 200
                });
            }

            return Ok(new StatusResponse
            {
                Success = false,
                Message = "No active session found.",
                StatusCode = 400
            });

        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPassRequest request)
        {
            var result = await _authService.ForgotPasswordAsync(request.Email);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, new StatusResponse
                {
                    Success = false,
                    Message = result.Message,
                    StatusCode = result.StatusCode
                });
            }

            return Ok(new StatusResponse
            {
                Success = true,
                Message = result.Message,
                StatusCode = 200
            });
        }

        [HttpGet("GetUser")]
        public async Task<IActionResult> GetUserFromCookie()
        {
            if (!Request.Cookies.TryGetValue("session_token", out var sessionToken) || string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "Session token missing or expired.",
                    StatusCode = 401
                });
            }

            var user = await _authService.GetUserByUIDAsync(sessionToken);

            if (user == null)
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "User not found.",
                    StatusCode = 400
                });

            var userDto = new UserDto
            {
                UID = user.UID,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Username = user.Username,
                ProfilePicture = user.ProfilePicture,
                Role = Enum.IsDefined(typeof(UserRole), user.Role) ? ((UserRole)user.Role).ToString() : "Unknown",
                CreatedAt = user.createdAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
            };

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "User Data fetched successfully.",
                StatusCode = 201,
                Data = userDto

            });
        }


    }
}
