//using KuwagoAPI.DTO;
//using KuwagoAPI.Helper;
//using KuwagoAPI.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//namespace KuwagoAPI.Controllers.Assessment
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class AIAssessmentController : ControllerBase
//    {
//        private readonly AIAssessmentService _aiAssessmentService;

//        public AIAssessmentController(AIAssessmentService aiAssessmentService)
//        {
//            _aiAssessmentService = aiAssessmentService;
//        }

//        /// <summary>
//        /// Assesses a user's eligibility for a new loan using AI analysis of their credit history.
//        /// </summary>
//        /// <param name="uid">The user ID to assess.</param>
//        /// <returns>Detailed assessment of loan eligibility including AI-generated insights.</returns>
//        [HttpGet("{uid}")]
//        [Authorize(Policy = "AdminLender")]
//        public async Task<IActionResult> AssessLoanEligibility(string uid)
//        {
//            try
//            {
//                var assessment = await _aiAssessmentService.AssessLoanEligibility(uid);
//                return Ok(new StatusResponse
//                {
//                    Success = true,
//                    Message = "Loan eligibility assessment completed successfully.",
//                    StatusCode = 200,
//                    Data = assessment
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new StatusResponse
//                {
//                    Success = false,
//                    Message = $"Error performing loan eligibility assessment: {ex.Message}",
//                    StatusCode = 500
//                });
//            }
//        }
//    }
//}