using KuwagoAPI.Helper;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace KuwagoAPI.Controllers.IdentityVerification
{

    /// <summary>
    /// Handles user face verification 
    /// </summary>
    [Route("api/[controller]")]

    [ApiController]
    public class IdentityVerificationController : ControllerBase
    {
        private readonly CloudinaryService _cloudinaryService;
        private readonly IdentityVerificationService _verificationService;
        private readonly AuthService _authService;
        private readonly FaceVerificationService _faceVerificationService;

        public IdentityVerificationController(
            CloudinaryService cloudinaryService,
            IdentityVerificationService verificationService,
            AuthService authService,
            FaceVerificationService faceVerificationService)
        {
            _cloudinaryService = cloudinaryService;
            _verificationService = verificationService;
            _authService = authService;
            _faceVerificationService = faceVerificationService;
        }

        private string GetUserIdFromToken()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Uploads user's Face Selfie and ID Photo
        /// </summary>

        [Authorize(Policy = "BorrowerOnly")]
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

            // Accept only image/jpeg and image/png
            var allowedContentTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedContentTypes.Contains(idPhoto.ContentType) || !allowedContentTypes.Contains(selfiePhoto.ContentType))
            {
                return BadRequest(new StatusResponse
                {
                    Success = false,
                    Message = "Only JPEG and PNG image formats are allowed.",
                    StatusCode = 400
                });
            }

            var uid = GetUserIdFromToken();
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

            var idUrl = await _cloudinaryService.UploadIDAndSelfieAsync(idPhoto);
            var selfieUrl = await _cloudinaryService.UploadIDAndSelfieAsync(selfiePhoto);

            await _verificationService.UploadIDPhotoAsync(user.UID, idUrl, selfieUrl);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "ID and Selfie uploaded successfully.",
                StatusCode = 200,
                Data = new { idUrl, selfieUrl }
            });
        }

        /// <summary>
        /// Get logged-in user Identity verification
        /// </summary>

        [Authorize(Policy = "All")]
        [HttpGet("GetIdentityLoggedInVerification")]
        public async Task<IActionResult> GetIdentityVerification()
        {
            var uid = GetUserIdFromToken();
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
                    Name = $"{user.FirstName} {user.LastName}",
                    Email = user.Email,
                    IDUrl = verification.IDUrl,
                    SelfieUrl = verification.SelfieUrl,
                    UploadedAt = verification.UploadedAt.ToDateTime()
                }
            });
        }


        /// <summary>
        /// Handles automatic face matching for users
        /// </summary>

        [Authorize(Policy = "BorrowerOnly")]
        [HttpPost("VerifyFaceMatch")]
        public async Task<IActionResult> VerifyFaceMatch()
        {
            var uid = GetUserIdFromToken();
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

            var verification = await _verificationService.GetIdentityVerificationAsync(user.UID);
            if (verification == null)
            {
                return NotFound(new StatusResponse
                {
                    Success = false,
                    Message = "Identity photos not found.",
                    StatusCode = 404
                });
            }

            var result = await _faceVerificationService.VerifyFaceMatchAsync(verification.IDUrl, verification.SelfieUrl);
            var confidence = Convert.ToDouble(result?.GetType().GetProperty("confidence")?.GetValue(result));
            var verifyStatus = confidence >= 80 ? VerificationStatus.Passed : VerificationStatus.Failed;

            // Save to Firestore
            await _verificationService.UpdateVerificationResultAsync(user.UID, confidence, verifyStatus);

            return Ok(new StatusResponse
            {
                Success = true,
                Message = "Face verification completed.",
                StatusCode = 200,
                Data = new
                {
                    confidence,
                    verifyStatus = (int)verifyStatus
                }
            });
        }


    }
}
