using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuwagoAPI.Controllers.Assessment
{
    [ApiController]
    [Route("api/[controller]")]
    public class AIAssessmentController : ControllerBase
    {
        private readonly AIAssessmentService _aiAssessmentService;

        public AIAssessmentController(AIAssessmentService aiAssessmentService)
        {
            _aiAssessmentService = aiAssessmentService;
        }


        [Authorize("BorrowerOnly")]
        [HttpGet("ImprovedScore/{uid}")]
        public async Task<IActionResult> GetImprovedScore(string uid)
        {
            var result = await _aiAssessmentService.GetImprovedScoreAsync(uid);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("LoanAssessment/{uid}")]
        [Authorize("LenderBorrower")]
        public async Task<IActionResult> GetLoanApprovalAnalysis(string uid)
        {
            var result = await _aiAssessmentService.GetLoanApprovalAnalysisAsync(uid);
            return StatusCode(result.StatusCode, result);
        }
    }
}