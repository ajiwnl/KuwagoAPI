using Microsoft.AspNetCore.Mvc;
using KuwagoAPI.Services;
using KuwagoAPI.Models;

namespace KuwagoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleController : ControllerBase
    {
        private readonly FirestoreService _firestoreService;

        public SampleController(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSample(string id)
        {
            var sample = await _firestoreService.GetSampleByIdAsync(id);

            if (sample == null)
                return NotFound($"No document found with ID: {id}");

            return Ok(sample);
        }
    }
}
