using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rhino.Compute;
using SpinlioCompute.Utils;

namespace SpinlioCompute.Services
{
    /**
     * 🌿 Service for processing Grasshopper parametric design operations
     * 
     * Core capabilities:
     * - Running Grasshopper definitions with custom parameters
     * - Processing both pre-loaded and dynamically provided definitions
     * - Converting parameter data for Rhino Compute compatibility
     * - Parsing and structuring Grasshopper computation results
     * 
     * This service acts as a bridge between the API and the Rhino Compute server,
     * handling all the complexity of Grasshopper definition execution.
     */
    public class GrasshopperService
    {
        /// <summary>Logger for recording service activities</summary>
        private readonly ILogger<GrasshopperService> _logger;
        
        /// <summary>Application configuration for accessing settings</summary>
        private readonly IConfiguration _configuration;
        
        /// <summary>Path to the directory containing Grasshopper definition files</summary>
        private readonly string _definitionsPath;

        /**
         * 🔧 Initializes a new instance of the GrasshopperService
         * 
         * Sets up required dependencies, configurations, and initializes the 
         * connection to the Rhino Compute server.
         * 
         * @param logger - Logger for recording service activities
         * @param configuration - Application configuration for accessing settings
         */
        public GrasshopperService(ILogger<GrasshopperService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _definitionsPath = _configuration["RhinoCompute:DefaultDefinitionsPath"] ?? "Resources/GrasshopperDefinitions";
            
            // Configure Rhino Compute client
            ComputeServer.WebAddress = _configuration["RhinoCompute:ServerUrl"] ?? "http://localhost:6500/";
            ComputeServer.ApiKey = _configuration["RhinoCompute:ApiKey"];
        }

        /**
         * 🚀 Processes a Grasshopper definition with specified parameters
         * 
         * This method:
         * 1. Prepares parameter data for Grasshopper
         * 2. Loads the definition file (by name or from base64)
         * 3. Sends the definition and parameters to Rhino Compute
         * 4. Processes and structures the results
         * 
         * @param definitionName - Name of a pre-loaded Grasshopper definition file
         * @param parameters - Dictionary of parameter name/value pairs
         * @param definitionBase64 - Optional base64-encoded definition content
         * @returns GrasshopperResponse containing processing results or error information
         */
        public async Task<GrasshopperResponse> ProcessDefinition(string definitionName, Dictionary<string, object> parameters, string? definitionBase64 = null)
        {
            try
            {
                _logger.LogInformation($"Processing Grasshopper definition: {definitionName}");
                
                // Prepare the payload for the Grasshopper component
                var parameterValues = new List<GrasshopperParameter>();
                
                foreach (var param in parameters)
                {
                    // Convert parameter value to appropriate type and format
                    parameterValues.Add(new GrasshopperParameter
                    {
                        ParamName = param.Key,
                        InnerTree = new Dictionary<string, List<GrasshopperDataItem>>
                        {
                            { "0", new List<GrasshopperDataItem> { CreateDataItem(param.Value) } }
                        }
                    });
                }

                // Prepare the algorithm to use - either a base64 string or a path to a definition file
                string algorithm = string.Empty;
                if (!string.IsNullOrEmpty(definitionBase64))
                {
                    // Use the provided base64-encoded definition
                    algorithm = definitionBase64;
                }
                else if (!string.IsNullOrEmpty(definitionName))
                {
                    // Look for the definition file in the definitions directory
                    string definitionPath = Path.Combine(_definitionsPath, definitionName);
                    if (!definitionPath.EndsWith(".gh") && !definitionPath.EndsWith(".ghx"))
                    {
                        definitionPath += ".gh";
                    }
                    
                    if (File.Exists(definitionPath))
                    {
                        // Read the file and convert to base64
                        byte[] fileBytes = File.ReadAllBytes(definitionPath);
                        algorithm = Convert.ToBase64String(fileBytes);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Grasshopper definition not found: {definitionPath}");
                    }
                }
                else
                {
                    throw new ArgumentException("Either definitionName or definitionBase64 must be provided");
                }

                // Create the request payload
                var payload = new GrasshopperRequest
                {
                    Algo = algorithm,
                    Pointer = null,
                    Values = parameterValues.ToArray()
                };
                
                string requestJson = JsonConvert.SerializeObject(payload);
                _logger.LogDebug($"Grasshopper request: {requestJson}");
                
                // Call Rhino Compute
                string endpoint = "grasshopper";
                string result = await ComputeServer.GenericCompute(endpoint, null, requestJson);
                
                // Parse the result
                var computeResponse = JsonConvert.DeserializeObject<GrasshopperComputeResponse>(result);
                
                if (computeResponse == null)
                {
                    throw new Exception("Failed to parse Grasshopper compute response");
                }
                
                _logger.LogInformation($"Grasshopper computation completed successfully");
                
                // Process and convert the results
                var response = new GrasshopperResponse
                {
                    Success = true,
                    Outputs = computeResponse.Values,
                    Meshes = ExtractMeshesFromResponse(computeResponse)
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Grasshopper definition");
                return new GrasshopperResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        /**
         * 🔄 Creates a properly typed data item from a parameter value
         * 
         * Handles type conversion for different parameter types:
         * - Numbers (converted to either Int32 or Double)
         * - Booleans
         * - Strings (default type for other values)
         * 
         * @param value - The parameter value to convert
         * @returns A GrasshopperDataItem with appropriate type information
         */
        private GrasshopperDataItem CreateDataItem(object value)
        {
            var type = value?.GetType();
            string typeName = "System.String";
            
            if (value is int || value is long || value is short)
            {
                typeName = "System.Int32";
                value = Convert.ToInt32(value);
            }
            else if (value is double || value is float || value is decimal)
            {
                typeName = "System.Double";
                value = Convert.ToDouble(value);
            }
            else if (value is bool)
            {
                typeName = "System.Boolean";
            }
            
            return new GrasshopperDataItem
            {
                Type = typeName,
                Data = value
            };
        }
        
        /**
         * 📊 Extracts mesh data from the Grasshopper computation response
         * 
         * This is a placeholder implementation. In a real scenario, this would:
         * 1. Extract mesh data from specific outputs in the response
         * 2. Convert the data into a format suitable for client consumption
         * 3. Handle multiple meshes or other geometric data
         * 
         * @param response - The raw response from Rhino Compute
         * @returns Array of extracted and formatted mesh objects
         */
        private object[] ExtractMeshesFromResponse(GrasshopperComputeResponse response)
        {
            // This is a placeholder. In a real implementation, 
            // you would parse the response and extract meshes or other geometry
            
            // For now, return a simple placeholder
            return new object[] { };
        }
        
        // Internal data structures for Grasshopper communication
        
        /**
         * 📦 Request structure for the Grasshopper API
         * 
         * Follows the format expected by the Rhino Compute Grasshopper endpoint.
         */
        public class GrasshopperRequest
        {
            /// <summary>Base64-encoded Grasshopper definition</summary>
            [JsonProperty("algo")]
            public string Algo { get; set; }
            
            /// <summary>Optional pointer for caching (usually null)</summary>
            [JsonProperty("pointer")]
            public string Pointer { get; set; }
            
            /// <summary>Array of parameters to pass to the definition</summary>
            [JsonProperty("values")]
            public GrasshopperParameter[] Values { get; set; }
        }
        
        /**
         * 📊 Parameter structure for Grasshopper computation
         * 
         * Represents a single named parameter with its data tree of values.
         */
        public class GrasshopperParameter
        {
            /// <summary>Name of the parameter in the Grasshopper definition</summary>
            [JsonProperty("ParamName")]
            public string ParamName { get; set; }
            
            /// <summary>Data tree of values (usually just a single item at path "0")</summary>
            [JsonProperty("InnerTree")]
            public Dictionary<string, List<GrasshopperDataItem>> InnerTree { get; set; }
        }
        
        /**
         * 📄 Individual data item for a Grasshopper parameter
         * 
         * Contains both the value and its .NET type information.
         */
        public class GrasshopperDataItem
        {
            /// <summary>.NET type name (e.g., "System.Double")</summary>
            [JsonProperty("type")]
            public string Type { get; set; }
            
            /// <summary>The actual parameter value</summary>
            [JsonProperty("data")]
            public object Data { get; set; }
        }
        
        /**
         * 📥 Response structure from the Rhino Compute Grasshopper endpoint
         * 
         * Contains the raw computation results returned by Rhino Compute.
         */
        public class GrasshopperComputeResponse
        {
            /// <summary>Reflects the algorithm that was processed</summary>
            public string Algo { get; set; }
            
            /// <summary>Dictionary of output values from the computation</summary>
            public Dictionary<string, object> Values { get; set; }
            
            /// <summary>Cached parameters for potential reuse</summary>
            public object[] CachedParameters { get; set; }
        }
        
        /**
         * 📤 Structured response for Grasshopper computation results
         * 
         * This is our service's output format, containing processed results.
         */
        public class GrasshopperResponse
        {
            /// <summary>Whether the computation was successful</summary>
            public bool Success { get; set; }
            
            /// <summary>Error message if computation failed</summary>
            public string Error { get; set; }
            
            /// <summary>Extracted mesh geometries from the computation</summary>
            public object[] Meshes { get; set; }
            
            /// <summary>Other computed values from the definition</summary>
            public Dictionary<string, object> Outputs { get; set; }
        }
    }
} 