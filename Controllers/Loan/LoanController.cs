using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KuwagoAPI.Controllers.Loan
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoanController : ControllerBase
    {
        private readonly LoanService _loanService;

        public LoanController(LoanService loanService)
        {
            _loanService = loanService;
        }

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

        [Authorize(Policy = "BorrowerOnly")]
        [HttpGet("LoanRequests/{uid}")]
        public async Task<IActionResult> GetLoanRequests(string uid)
        {
            var result = await _loanService.GetLoanRequestsByUIDAsync(uid);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Policy = "BorrowerOnly")]
        [HttpPost("FilterLoans/{uid}")]
        public async Task<IActionResult> FilterLoans(string uid, [FromBody] LoanFilterDTO filter)
        {
            var result = await _loanService.FilterLoanRequestsAsync(uid, filter);

            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Policy = "AdminLender")]
        [HttpGet("AllLoans")]
        public async Task<IActionResult> GetAllLoans()
        {
            var result = await _loanService.GetAllLoanRequestsAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("FilterLoans")]
        [Authorize(Policy = "AdminLender")]
        public async Task<IActionResult> FilterLoans([FromBody] LoanFilterDTOv2 filter)
        {
            var result = await _loanService.FilterLoanRequestsAsync(filter);
            return StatusCode(result.StatusCode, result);
        }


    }

}
