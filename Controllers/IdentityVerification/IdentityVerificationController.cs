using KuwagoAPI.Helper;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Mvc;
using static KuwagoAPI.Startup;

namespace KuwagoAPI.Controllers.IdentityVerification
{
    [Route("api/[controller]")]
    [ApiController]
    public class IdentityVerificationController : ControllerBase
    {
        private readonly CloudinaryService _cloudinaryService;
        private readonly IdentityVerificationService _verificationService;
        private readonly AuthService _authService;

        public IdentityVerificationController(
            CloudinaryService cloudinaryService,
            IdentityVerificationService verificationService,
            AuthService authService)
        {
            _cloudinaryService = cloudinaryService;
            _verificationService = verificationService;
            _authService = authService;
        }

        [HttpPost("UploadIDAndSelfie")]
        public async Task<IActionResult> UploadIDAndSelfie(IFormFile idPhoto, IFormFile selfiePhoto)
        {
            if (idPhoto == null || idPhoto.Length == 0 || selfiePhoto == null || selfiePhoto.Length == 0)
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Both ID and Selfie photos are required.",
                    StatusCode = 400
                });
            }

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
            {
                return NotFound(new StatusResponse
                {
                    Success = false,
                    Message = "User not found.",
                    StatusCode = 404
                });
            }

            var alreadyUploaded = await _verificationService.IDAlreadyUploadedAsync(user.UID);
            if (alreadyUploaded)
            {
                return Conflict(new StatusResponse
                {
                    Success = false,
                    Message = "Identity documents were already uploaded.",
                    StatusCode = 409
                });
            }

            // Upload both files
            var idUrl = await _cloudinaryService.UploadIDAndSelfieAsync(idPhoto);
            var selfieUrl = await _cloudinaryService.UploadIDAndSelfieAsync(selfiePhoto);

            // Save both to Firestore
            await _verificationService.UploadIDPhotoAsync(user.UID, idUrl, selfieUrl);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "ID and Selfie uploaded successfully.",
                StatusCode = 200,
                Data = new { idUrl, selfieUrl }
            });
        }
        [HttpGet("GetIdentityVerification")]
public async Task<IActionResult> GetIdentityVerification()
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
    {
        return NotFound(new StatusResponse
        {
            Success = false,
            Message = "User not found.",
            StatusCode = 404
        });
    }

    var verification = await _verificationService.GetIdentityVerificationAsync(user.UID);
    if (verification == null)
    {
        return NotFound(new StatusResponse
        {
            Success = false,
            Message = "Identity verification data not found.",
            StatusCode = 404
        });
    }

    return Ok(new StatusResponse
    {
        Success = true,
        Message = "Identity verification data retrieved successfully.",
        StatusCode = 200,
        Data = new
        {
            UID = user.UID,
            Name = user.FirstName + " " + user.LastName,
            Email = user.Email,
            IDUrl = verification.IDUrl,
            SelfieUrl = verification.SelfieUrl,
            UploadedAt = verification.UploadedAt.ToDateTime()
        }
    });
}   

    }
}
