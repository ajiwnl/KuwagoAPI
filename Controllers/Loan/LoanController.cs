using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;



namespace KuwagoAPI.Controllers.Loan
{
    /// <summary>
    /// Handles loan request creation, retrieval, filtering, and agreements.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class LoanController : ControllerBase
    {
        private readonly LoanService _loanService;

        public LoanController(LoanService loanService)
        {
            _loanService = loanService;
        }

        /// <summary>
        /// Submits a new loan request by a borrower.
        /// </summary>
        /// <param name="dto">Loan details to be submitted.</param>
        /// <returns>Status response indicating the outcome of the loan request creation.</returns>
        [Authorize(Policy = "BorrowerOnly")]
        [HttpPost("LoanRequest")]
        public async Task<IActionResult> RequestLoan([FromBody] LoanDTO dto)
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid))
                return Unauthorized(new StatusResponse { Success = false, Message = "UID not found.", StatusCode = 401 });

            var response = await _loanService.CreateLoanRequestAsync(uid, dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Retrieves all loan requests submitted by a specific borrower.
        /// </summary>
        /// <param name="uid">User ID of the borrower.</param>
        /// <returns>List of loan requests.</returns>
        [Authorize(Policy = "BorrowerOnly")]
        [HttpGet("LoanRequests/{uid}")]
        public async Task<IActionResult> GetLoanRequests(string uid)
        {
            var result = await _loanService.GetLoanRequestsByUIDAsync(uid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Filters loan requests submitted by a borrower based on specific criteria.
        /// </summary>
        /// <param name="uid">User ID of the borrower.</param>
        /// <param name="filter">Filter parameters such as loan type, amount, etc.</param>
        /// <returns>Filtered list of loan requests.</returns>
        [Authorize(Policy = "BorrowerOnly")]
        [HttpPost("FilterLoans/{uid}")]
        public async Task<IActionResult> FilterLoans(string uid, [FromBody] LoanFilterDTO filter)
        {
            var result = await _loanService.FilterLoanRequestsAsync(uid, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Retrieves all loan requests across all users. Accessible by Admins and Lenders.
        /// </summary>
        /// <returns>All loan requests in the system.</returns>
        [Authorize(Policy = "AdminLender")]
        [HttpGet("AllLoans")]
        public async Task<IActionResult> GetAllLoans()
        {
            var result = await _loanService.GetAllLoanRequestsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Filters loan requests (Admin/Lender version) based on criteria such as amount, status, etc.
        /// </summary>
        /// <param name="filter">Filter parameters for admin or lender view.</param>
        /// <returns>Filtered list of loan requests.</returns>
        [HttpPost("FilterLoans")]
        [Authorize(Policy = "AdminLender")]
        public async Task<IActionResult> FilterLoans([FromBody] LoanFilterDTOv2 filter)
        {
            var result = await _loanService.FilterLoanRequestsAsync(filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Approves or denies a loan request. Only accessible by lenders.
        /// </summary>
        /// <param name="dto">Loan agreement decision and modified loan details.</param>
        /// <returns>Result of the loan agreement processing.</returns>
        [Authorize(Policy = "LenderOnly")]
        [HttpPut("LoanAgreement")]
        public async Task<IActionResult> ApproveOrDenyLoan([FromBody] LoanAgreementDTO dto)
        {
            var adminUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminUid))
                return Unauthorized(new StatusResponse { Success = false, Message = "Admin UID not found.", StatusCode = 401 });

            var result = await _loanService.ProcessLoanAgreementAsync(dto, adminUid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Filters agreed (approved/denied) loan records based on specified criteria.
        /// </summary>
        /// <returns>Filtered agreed loan records.</returns>
        [Authorize(Policy = "All")]
        [HttpGet("FilterAgreedLoans")]
        public async Task<IActionResult> FilterAgreedLoans(
            [FromQuery] string? agreedLoanId = null,
            [FromQuery] string? borrowerUid = null,
            [FromQuery] string? lenderUid = null,
            [FromQuery] string? borrowerFirstName = null,
            [FromQuery] string? borrowerLastName = null,
            [FromQuery] string? lenderFirstName = null,
            [FromQuery] string? lenderLastName = null,
            [FromQuery] double? minInterestRate = null,
            [FromQuery] double? maxInterestRate = null,
            [FromQuery] DateTime? agreementDateAfter = null,
            [FromQuery] DateTime? agreementDateBefore = null,
            [FromQuery] string? loanStatus = null)
        {
            var filter = new AgreedLoanFilterDTO
            {
                AgreedLoanID = agreedLoanId,
                BorrowerUID = borrowerUid,
                LenderUID = lenderUid,
                BorrowerFirstName = borrowerFirstName,
                BorrowerLastName = borrowerLastName,
                LenderFirstName = lenderFirstName,
                LenderLastName = lenderLastName,
                MinInterestRate = minInterestRate,
                MaxInterestRate = maxInterestRate,
                AgreementDateAfter = agreementDateAfter,
                AgreementDateBefore = agreementDateBefore,
                LoanStatus = loanStatus
            };

            var response = await _loanService.FilterAgreedLoansAsync(filter);
            return StatusCode(response.StatusCode, response);
        }
    }
}
