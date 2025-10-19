using KuwagoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuwagoAPI.Controllers.Analytics
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Retrieves all analytics data: total users, active loans, total revenue.
        /// </summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet("Overview")]
        public async Task<IActionResult> GetAnalyticsOverview()
        {
            var data = await _analyticsService.GetAnalyticsAsync();
            return Ok(new
            {
                success = true,
                message = "Analytics fetched successfully",
                data
            });
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("UserGrowth")]
        public async Task<IActionResult> GetUserGrowth([FromQuery] string period = "monthly")
        {
            var data = await _analyticsService.GetUserGrowthAsync(period);

            return Ok(new
            {
                success = true,
                message = "User growth data fetched successfully",
                data
            });
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("RevenueTrend")]
        public async Task<IActionResult> GetRevenueTrend([FromQuery] string period = "monthly")
        {
            var data = await _analyticsService.GetRevenueTrendAsync(period);

            return Ok(new
            {
                success = true,
                message = "Revenue trend data fetched successfully",
                data
            });
        }


    }
}