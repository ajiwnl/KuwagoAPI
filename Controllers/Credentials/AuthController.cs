using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
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
                return Unauthorized(result);

            string uid = "";
            if (result.Data is IDictionary<string, object> dataDict && dataDict.ContainsKey("UID"))
                uid = dataDict["UID"].ToString();
            else
                uid = ((dynamic)result.Data)?.UID;

            var user = await _authService.GetUserByUIDAsync(uid);
            if (user == null)
                return Unauthorized("User not found");

            var token = _authService.GenerateJwtToken(uid, user.Role);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "Login successful",
                StatusCode = 200,
                Data = new { Token = token }
            });
        }



        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
        
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

        [Authorize(Policy = "AdminLendersBorrowers")]
        [HttpGet("GetUser")]
        public async Task<IActionResult> GetUser()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "User ID missing from token.",
                    StatusCode = 401
                });
            }

            var user = await _authService.GetUserByUIDAsync(userId);

            if (user == null)
            {
                return NotFound(new StatusResponse
                {
                    Success = false,
                    Message = "User not found.",
                    StatusCode = 404
                });
            }

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
                StatusCode = 200,
                Data = userDto
            });
        }

        [Authorize]
        [HttpGet("CheckTokenStatus")]
        public IActionResult CheckJwt()
        {
            return Ok(new StatusResponse
            {
                Success = true,
                Message = "JWT is valid.",
                StatusCode = 200
            });
        }







    }
}
