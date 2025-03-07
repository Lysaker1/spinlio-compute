using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpinlioCompute.Models;
using SpinlioCompute.Services;

namespace SpinlioCompute.Controllers
{
    /**
     * 🔄 Controller for 3D model format conversion operations
     * 
     * This API controller provides endpoints for:
     * - Starting new model conversion jobs
     * - Checking the status of ongoing or completed conversions
     * 
     * Conversions are processed asynchronously, with the client receiving
     * a job ID that can be used to check the status later.
     */
    [ApiController]
    [Route("[controller]")]
    public class ConvertController : ControllerBase
    {
        /// <summary>Service for handling model conversion operations</summary>
        private readonly ModelConversionService _conversionService;
        
        /// <summary>Logger for recording controller activities</summary>
        private readonly ILogger<ConvertController> _logger;

        /**
         * 🔧 Initializes a new instance of the ConvertController
         * 
         * @param conversionService - Service for processing model conversions
         * @param logger - Logger for recording controller activities
         */
        public ConvertController(ModelConversionService conversionService, ILogger<ConvertController> logger)
        {
            _conversionService = conversionService;
            _logger = logger;
        }

        /**
         * 🚀 Starts a new model conversion job
         * 
         * POST /api/convert
         * 
         * This endpoint:
         * 1. Validates the incoming request
         * 2. Initiates an asynchronous conversion job
         * 3. Returns a job ID for status tracking
         * 
         * @param request - Contains model ID, S3 key, source format, and target formats
         * @returns 200 OK with job ID on success, 400 Bad Request if validation fails,
         *          or 500 Internal Server Error if an exception occurs
         */
        [HttpPost]
        public async Task<IActionResult> InitiateConversion([FromBody] ConversionRequest request)
        {
            try
            {
                _logger.LogInformation($"Received conversion request for model {request.ModelId}, S3 key: {request.S3Key}");
                
                // Validate required parameters
                if (string.IsNullOrEmpty(request.ModelId) || 
                    string.IsNullOrEmpty(request.S3Key) ||
                    string.IsNullOrEmpty(request.SourceFormat))
                {
                    return BadRequest("Required parameters missing. ModelId, S3Key, and SourceFormat are required.");
                }
                
                // Default to GLB and GLTF if no target formats specified
                var targetFormats = request.TargetFormats ?? new[] { "glb", "gltf" };
                
                // Start the conversion job
                var job = await _conversionService.StartConversionJob(
                    request.ModelId,
                    request.S3Key,
                    request.SourceFormat,
                    targetFormats
                );
                
                _logger.LogInformation($"Conversion job {job.Id} started for model {request.ModelId}");
                
                // Return the job ID to the client
                return Ok(new { jobId = job.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error initiating conversion for model {request.ModelId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /**
         * 📊 Gets the current status of a conversion job
         * 
         * GET /api/convert/status/{jobId}
         * 
         * This endpoint:
         * 1. Retrieves the current status of a job by ID
         * 2. Returns status details including outputs if completed
         * 
         * @param jobId - Unique identifier of the conversion job
         * @returns 200 OK with status information or 500 Internal Server Error if job not found
         */
        [HttpGet("status/{jobId}")]
        public IActionResult GetJobStatus(string jobId)
        {
            try
            {
                _logger.LogInformation($"Checking status for job {jobId}");
                
                // Retrieve the job status
                var status = _conversionService.GetJobStatus(jobId);
                
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving status for job {jobId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
} 