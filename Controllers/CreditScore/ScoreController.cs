using KuwagoAPI.Models;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuwagoAPI.Controllers.CreditScore
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScoreController : ControllerBase
    {
        private readonly CreditScoreService _creditScoreService;

        public ScoreController(CreditScoreService creditScoreService)
        {
            _creditScoreService = creditScoreService;
        }

        [HttpGet("GetCreditScore/{uid}")]
        [Authorize("BorrowerOnly")]
        public async Task<IActionResult> GetCreditScore(string uid)
        {
            var creditScore = await _creditScoreService.GetCreditScoreAsync(uid);

            if (creditScore == null)
                return NotFound(new
                {
                    success = false,
                    message = "Credit score not found for this user.",
                    statusCode = 404
                });

            return Ok(new
            {
                success = true,
                message = "Credit score retrieved successfully.",
                statusCode = 200,
                data = new
                {
                    creditScore.UID,
                    creditScore.Score,
                    creditScore.TotalLoans,
                    creditScore.SuccessfulRepayments,
                    creditScore.MissedRepayments,
                    LastUpdated = creditScore.LastUpdated.ToDateTime().ToString("MMMM dd, yyyy hh:mm:ss tt 'UTC'zzz")
                }
            });
        }

        [HttpGet("GetCreditScoreCategory/{uid}")]
        [Authorize("BorrowerOnly")]
        public async Task<IActionResult> GetCreditScoreCategory(string uid)
        {
            var creditScore = await _creditScoreService.GetCreditScoreAsync(uid);

            if (creditScore == null)
                return NotFound(new
                {
                    success = false,
                    message = "Credit score not found for this user.",
                    statusCode = 404
                });

            string category = GetScoreCategory(creditScore.Score);

            return Ok(new
            {
                success = true,
                message = "Credit score category calculated successfully.",
                statusCode = 200,
                data = new
                {
                    creditScore.UID,
                    creditScore.Score,
                    Category = category
                }
            });
        }

        // Helper method to determine credit score category
        private string GetScoreCategory(int score)
        {
            if (score < 580) return "Poor";
            if (score >= 580 && score < 670) return "Fair";
            if (score >= 670 && score < 740) return "Good";
            if (score >= 740 && score < 800) return "Very Good";
            return "Excellent";
        }
    }
}
