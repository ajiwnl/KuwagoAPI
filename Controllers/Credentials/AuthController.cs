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
                return StatusCode(result.StatusCode, result.Message);

            // Set a cookie with 10-minute expiration
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddMinutes(10),
                SameSite = SameSiteMode.Strict
            };

            Response.Cookies.Append("session_token", result.Message, cookieOptions);

            return Ok("Login successful!");
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            if (Request.Cookies["session_token"] != null)
            {
                Response.Cookies.Delete("session_token");
                return Ok("Logged out successfully.");
            }

            return BadRequest("No active session found.");
        }



    }
}
