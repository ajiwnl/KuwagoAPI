using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KuwagoAPI.Controllers.Credentials
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly CloudinaryService _cloudinaryService;


        public AuthController(AuthService authService, CloudinaryService cloudinaryService)
        {
            _cloudinaryService = cloudinaryService;
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

            string firebaseToken = ((dynamic)result.Data)?.FirebaseToken;
            var token = _authService.GenerateJwtToken(uid, user.Role, firebaseToken);


            return Ok(new StatusResponse
            {
                Success = true,
                Message = "Login successful",
                StatusCode = 200,
                Data = new { Token = token }
            });
        }



        [Authorize(Policy = "AdminLendersBorrowers")]
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
                StatusCode = 200,
                Data = result.Data
            });
        }

        [Authorize(Policy = "AdminLendersBorrowers")]
        [HttpGet("GetUserLoggedInInfo")]
        public async Task<IActionResult> GetUserLoggedInInfo()
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
                CreatedAt = user.createdAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Status = Enum.IsDefined(typeof(UserStatus), user.Status) ? ((UserStatus)user.Status).ToString() : "Unknown"

            };

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "User Data fetched successfully.",
                StatusCode = 200,
                Data = userDto
            });
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("GetSpecificUser")]
        public async Task<IActionResult> GetSpecificUser(
    [FromQuery] string? UID,
    [FromQuery] string? LastName,
    [FromQuery] string? Email,
    [FromQuery] int? Role) 
        {
            var users = await _authService.GetAllUsersAsync(UID, LastName, Email, Role);

            var userDtos = users.Select(user => new UserDto
            {
                UID = user.UID,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Username = user.Username,
                ProfilePicture = user.ProfilePicture,
                Role = Enum.IsDefined(typeof(UserRole), user.Role) ? ((UserRole)user.Role).ToString() : "Unknown",
                CreatedAt = user.createdAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Status = Enum.IsDefined(typeof(UserStatus), user.Status) ? ((UserStatus)user.Status).ToString() : "Unknown"

            }).ToList();

            return Ok(new StatusResponse
            {
                Success = true,
                Message = $"Retrieved {userDtos.Count} user(s).",
                StatusCode = 200,
                Data = userDtos
            });
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("GetAllUser")]
        public async Task<IActionResult> GetAllUser()
        {
            var users = await _authService.GetAllUsersAsync(); // No parameters = get all

            var userDtos = users.Select(user => new UserDto
            {
                UID = user.UID,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Username = user.Username,
                ProfilePicture = user.ProfilePicture,
                Role = Enum.IsDefined(typeof(UserRole), user.Role) ? ((UserRole)user.Role).ToString() : "Unknown",
                CreatedAt = user.createdAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Status = Enum.IsDefined(typeof(UserStatus), user.Status) ? ((UserStatus)user.Status).ToString() : "Unknown"

            }).ToList();

            return Ok(new StatusResponse
            {
                Success = true,
                Message = $"Retrieved {userDtos.Count} user(s).",
                StatusCode = 200,
                Data = userDtos
            });
        }


        [Authorize]
        [HttpGet("CheckToken")]
        public IActionResult CheckJwt()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "Missing or invalid Authorization header.",
                    StatusCode = 401
                });
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var principal = JwtHelper.ValidateAndDecodeToken(token, "a-string-secret-at-least-256-bits-long");

            if (principal == null)
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "Invalid or expired token.",
                    StatusCode = 401
                });
            }

            var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "JWT is valid.",
                StatusCode = 200,
                Data = claims
            });
        }

        [Authorize(Policy = "AdminLendersBorrowers")]
        [Authorize]
        [HttpPut("EditUserInfoRequest")]
        public async Task<IActionResult> EditUserInfoRequest([FromBody] EditUserInfoRequest request)
        {
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "UID not found in token.",
                    StatusCode = 401
                });
            }

            var result = await _authService.EditUserInfoRequest(uid, request);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Policy = "AdminLendersBorrowers")]
        [HttpPut("ChangeEmail")]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Invalid request model",
                    StatusCode = 400,
                    Data = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                });
            }

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "UID not found in token.",
                    StatusCode = 401
                });
            }

            // Validate email format
            var emailValidator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
            if (!emailValidator.IsValid(request.NewEmail))
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Invalid email format.",
                    StatusCode = 400
                });
            }

            // Get the firebase token from the request body
            if (string.IsNullOrWhiteSpace(request.FirebaseToken))
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Firebase ID token is required in the request body.",
                    StatusCode = 400
                });
            }

            var result = await _authService.ChangeUserEmailAsync(uid, request.NewEmail, request.FirebaseToken);
            return StatusCode(result.StatusCode, result);
        }


        [Authorize(Policy = "AdminLendersBorrowers")]
        [HttpPut("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid))
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "UID not found in token.",
                    StatusCode = 401
                });

            var result = await _authService.ChangePasswordAsync(uid, request.NewPassword);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Policy = "AdminLendersBorrowers")]
        [HttpPost("UploadProfilePicture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid))
            {
                return Unauthorized(new StatusResponse
                {
                    Success = false,
                    Message = "UID not found in token.",
                    StatusCode = 401
                });
            }

            if (profilePicture == null || profilePicture.Length == 0)
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Profile picture file is required.",
                    StatusCode = 400
                });
            }

            // Validate MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedMimeTypes.Contains(profilePicture.ContentType.ToLower()))
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Only image files (JPEG, PNG, JPG, WEBP) are allowed.",
                    StatusCode = 400
                });
            }

            var user = await _authService.GetUserByUIDAsync(uid);
            if (user == null)
            {
                return NotFound(new StatusResponse
                {
                    Success = false,
                    Message = "User not found.",
                    StatusCode = 404
                });
            }

            var profilePicUrl = await _cloudinaryService.UploadIDAndSelfieAsync(profilePicture);
            await _authService.UpdateUserProfilePictureAsync(uid, profilePicUrl);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "Profile picture uploaded successfully.",
                StatusCode = 200,
                Data = new { profilePicUrl }
            });
        }

    }
}
