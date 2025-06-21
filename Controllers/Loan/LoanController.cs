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
    }

}
