using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpinlioCompute.Services;

namespace SpinlioCompute.Controllers
{
    /**
     * 🌿 Controller for Grasshopper parametric design operations
     * 
     * This API controller provides endpoints for:
     * - Processing parametric models using Grasshopper definitions
     * - Running defined algorithms with custom parameters
     * - Generating geometric outputs based on parametric inputs
     * 
     * Grasshopper is a visual programming language for Rhino that allows
     * creation of parametric 3D models that can be manipulated via inputs.
     */
    [ApiController]
    [Route("api/[controller]")]
    public class GrasshopperController : ControllerBase
    {
        /// <summary>Logger for recording controller activities</summary>
        private readonly ILogger<GrasshopperController> _logger;
        
        /// <summary>Service for processing Grasshopper definitions</summary>
        private readonly GrasshopperService _grasshopperService;

        /**
         * 🔧 Initializes a new instance of the GrasshopperController
         * 
         * @param logger - Logger for recording controller activities
         * @param grasshopperService - Service for processing Grasshopper definitions
         */
        public GrasshopperController(ILogger<GrasshopperController> logger, GrasshopperService grasshopperService)
        {
            _logger = logger;
            _grasshopperService = grasshopperService;
        }

        /**
         * 🚀 Processes a Grasshopper definition with specified parameters
         * 
         * POST /api/grasshopper
         * 
         * This endpoint:
         * 1. Validates the incoming request
         * 2. Processes a Grasshopper definition with provided parameters
         * 3. Returns generated meshes and computed outputs
         * 
         * The definition can be specified either by name (to use a pre-loaded definition)
         * or by providing a base64-encoded definition file directly in the request.
         * 
         * @param request - Contains definition name/data and input parameters
         * @returns 200 OK with processing results, 400 Bad Request if validation fails,
         *          or 500 Internal Server Error if processing fails
         */
        [HttpPost]
        public async Task<IActionResult> ProcessGrasshopperDefinition([FromBody] GrasshopperRequest request)
        {
            try
            {
                _logger.LogInformation($"Received Grasshopper processing request, definition: {request.DefinitionName}");
                
                // Validate that either definition name or base64 content is provided
                if (string.IsNullOrEmpty(request.DefinitionName) && string.IsNullOrEmpty(request.DefinitionBase64))
                {
                    return BadRequest("Either DefinitionName or DefinitionBase64 must be provided");
                }
                
                // Validate that parameters are provided
                if (request.Parameters == null || request.Parameters.Count == 0)
                {
                    return BadRequest("Parameters are required");
                }
                
                // Process the Grasshopper definition using our service
                var result = await _grasshopperService.ProcessDefinition(
                    request.DefinitionName,
                    request.Parameters,
                    request.DefinitionBase64
                );
                
                // Handle processing failure
                if (!result.Success)
                {
                    return StatusCode(500, new { error = result.Error });
                }
                
                // Return successful result
                return Ok(new GrasshopperResponse
                {
                    Success = true,
                    Meshes = result.Meshes,
                    Outputs = result.Outputs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Grasshopper definition");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /**
         * 📥 Request model for Grasshopper processing
         * 
         * Contains all information needed to process a Grasshopper definition,
         * including how to locate the definition and what parameters to use.
         */
        public class GrasshopperRequest
        {
            /// <summary>Name of a pre-loaded Grasshopper definition file</summary>
            public string DefinitionName { get; set; }
            
            /// <summary>Base64-encoded Grasshopper definition (.gh file)</summary>
            public string DefinitionBase64 { get; set; }
            
            /// <summary>Key-value pairs of parameters to use in the definition</summary>
            public Dictionary<string, object> Parameters { get; set; }
        }

        /**
         * 📤 Response model for Grasshopper processing results
         * 
         * Contains the results of processing a Grasshopper definition,
         * including success status, generated meshes, and computed values.
         */
        public class GrasshopperResponse
        {
            /// <summary>Whether the processing completed successfully</summary>
            public bool Success { get; set; }
            
            /// <summary>Error message if processing failed</summary>
            public string Error { get; set; }
            
            /// <summary>Array of 3D meshes generated by the definition</summary>
            public object[] Meshes { get; set; }
            
            /// <summary>Key-value pairs of computed outputs from the definition</summary>
            public Dictionary<string, object> Outputs { get; set; }
        }
    }
} 