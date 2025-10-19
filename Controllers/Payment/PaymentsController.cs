using KuwagoAPI.DTO;
using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KuwagoAPI.Controllers.Payment
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly PaymentService _paymentService;

        public PaymentsController(PaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Submit a payment for a specific payable loan.
        /// </summary>
        /// <param name="request">Payment request including PayableID, BorrowerUID, AmountPaid, and PaymentType.</param>
        /// <returns>Status message indicating success or failure of the payment submission.</returns>
        [HttpPost]
        [Authorize(Policy = "LenderOnly")]
        public async Task<IActionResult> SubmitPayment([FromBody] PaymentRequestDTO dto)

        {
            var result = await _paymentService.SubmitPaymentAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("Success")]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
                return BadRequest(new { Message = "Payment ID is required" });

            var result = await _paymentService.CompleteECashPayment(paymentId);

            if (result.Success)
            {
                // Redirect to success page or return success view
                return Ok(new
                {
                    Message = "Payment completed successfully!",
                    Data = result.Data
                });
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("Failed")]
        public async Task<IActionResult> PaymentFailed([FromQuery] string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
                return BadRequest(new { Message = "Payment ID is required" });

            var result = await _paymentService.CancelECashPayment(paymentId);

            return Ok(new
            {
                Message = "Payment was cancelled or failed. Please try again.",
                PaymentId = paymentId
            });
        }

        /// <summary>
        /// Retrieve all payments made by a specific borrower.
        /// </summary>
        /// <param name="borrowerUid">The UID of the borrower.</param>
        /// <returns>A list of payments made by the borrower.</returns>
        [HttpGet("by-borrower/{borrowerUid}")]
        [Authorize(Policy = "LenderBorrower")]
        public async Task<IActionResult> GetPaymentsByBorrower(string borrowerUid)

        {
            var payments = await _paymentService.GetPaymentsByBorrower(borrowerUid);
            return Ok(payments);
        }

        /// <summary>
        /// Retrieve all payments related to a specific payable loan.
        /// </summary>
        /// <param name="payableId">The ID of the payable loan agreement.</param>
        /// <returns>List of all payments associated with the specified payable.</returns>
        [HttpGet("by-payable/{payableId}")]
        [Authorize(Policy = "LenderBorrower")]
        public async Task<IActionResult> GetPaymentsByPayableId(string payableId)

        {
            var payments = await _paymentService.GetPaymentsByPayable(payableId);
            return Ok(payments);
        }


        /// <summary>
        /// Get a summary of total paid, remaining amount, and payment status for a specific payable.
        /// </summary>
        /// <param name="payableId">The ID of the payable to summarize.</param>
        /// <returns>A summary object showing total payable amount, total paid, remaining balance, and whether it's fully paid.</returns>
        [HttpGet("summary/{payableId}")]
        [Authorize(Policy = "LenderBorrower")]
        public async Task<IActionResult> GetPaymentSummary(string payableId)

        {
            var result = await _paymentService.GetPaymentSummary(payableId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get payment schedule details including monthly payment amount and scheduled dates
        /// </summary>
        /// <param name="borrowerUid">The UID of the borrower</param>
        /// <param name="payableId">The ID of the payable loan</param>
        /// <returns>Payment schedule details including monthly payment and dates</returns>
        [HttpGet("schedule/{borrowerUid}/{payableId}")]
        [Authorize(Policy = "LenderBorrower")]
        public async Task<IActionResult> GetPaymentScheduleDetails(string borrowerUid, string payableId)
        {
            var result = await _paymentService.GetPaymentScheduleDetails(borrowerUid, payableId);
            return StatusCode(result.StatusCode, result);
        }
    }

}
