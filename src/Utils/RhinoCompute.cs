/**
 * 🦏 RhinoCompute.cs - Client for Rhino Compute API
 * 
 * This utility file provides methods for communicating with a Rhino Compute server,
 * allowing access to advanced Rhino geometry processing capabilities over HTTP.
 * 
 * Key functionality:
 * - Configuring connection to Rhino Compute server
 * - Sending geometric data for processing
 * - Handling authentication and serialization
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rhino.Geometry;

namespace Rhino.Compute
{
    /**
     * 🌐 Core utility for communicating with a Rhino Compute server
     * 
     * This static class manages the connection to the Rhino Compute server
     * and provides methods for making API calls.
     */
    public static class ComputeServer
    {
        /// <summary>URL of the Rhino Compute server</summary>
        public static string WebAddress { get; set; } = "http://localhost:6500/";
        
        /// <summary>API key for authentication (if required)</summary>
        public static string ApiKey { get; set; } = null;
        
        /// <summary>Auth token for JWT-based authentication (if required)</summary>
        public static string AuthToken { get; set; } = null;

        /// <summary>URL path segment for Rhino.Compute API endpoints</summary>
        private const string _rhino_compute_appserver = "rhino/compute/";

        /// <summary>HttpClient instance for making API requests</summary>
        private static HttpClient _httpClient = null;
        
        /**
         * 🔧 Creates and configures an HttpClient for Rhino Compute requests
         * 
         * Sets up proper headers and timeouts for communication with the Compute server.
         * 
         * @returns Configured HttpClient instance
         */
        private static HttpClient CreateHttpClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10); // default timeout is 100 seconds
            if (!string.IsNullOrEmpty(AuthToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + AuthToken);
            }
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("RhinoComputeKey", ApiKey);
            }
            return _httpClient;
        }

        /**
         * 🚀 Calls a generic endpoint on the Rhino Compute server
         * 
         * This is the core method for making API calls to Rhino Compute.
         * It handles:
         * 1. Setting up the request with appropriate headers
         * 2. Building the URL with path and query parameters
         * 3. Sending the request and handling the response
         * 
         * @param endpoint - The API endpoint to call
         * @param parameters - Optional query string parameters
         * @param postData - Optional JSON data to send in the request body
         * @returns String response from the server (usually JSON)
         * @throws Exception if the request fails
         */
        public static async Task<string> GenericCompute(string endpoint, Dictionary<string, string> parameters = null, string postData = null)
        {
            if (_httpClient == null)
                _httpClient = CreateHttpClient();

            // Build the complete URL
            string computeUrl = WebAddress;
            if (computeUrl.EndsWith("/"))
                computeUrl = computeUrl.Substring(0, computeUrl.Length - 1);
            computeUrl += $"/{endpoint}";

            // Add query parameters if provided
            if (parameters != null && parameters.Count > 0)
            {
                computeUrl += "?";
                foreach (var p in parameters)
                {
                    computeUrl += $"{p.Key}={p.Value}&";
                }
                computeUrl = computeUrl.Substring(0, computeUrl.Length - 1);
            }

            // Create and configure the request
            var request = new HttpRequestMessage(HttpMethod.Post, computeUrl);
            if (!string.IsNullOrEmpty(postData))
            {
                request.Content = new StringContent(postData, Encoding.UTF8, "application/json");
            }

            // Send the request and handle the response
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
            }

            throw new Exception($"Error calling {computeUrl}: {response.StatusCode}");
        }
    }

    /**
     * 🔷 Utility for mesh-related operations via Rhino Compute
     * 
     * Provides methods for creating and manipulating mesh geometry.
     */
    public static class MeshCompute
    {
        /**
         * 🔄 Creates a mesh from a BREP object
         * 
         * @param brep - The BREP object to mesh
         * @returns Array of meshes generated from the BREP
         */
        public static Mesh[] CreateFromBrep(Brep brep)
        {
            return CreateFromBrep1(brep);
        }

        /**
         * 🔄 Implementation of mesh creation from BREP
         * 
         * Sends the BREP to Rhino Compute for meshing and returns the results.
         * 
         * @param brep - The BREP object to mesh
         * @returns Array of meshes generated from the BREP
         */
        public static Mesh[] CreateFromBrep1(Brep brep)
        {
            var args = new
            {
                brep = JsonConvert.SerializeObject(brep, GeometryResolver.Settings),
            };
            var result = ComputeServer.GenericCompute("rhino/geometry/mesh/createfrombrep-brep", null, JsonConvert.SerializeObject(args, GeometryResolver.Settings)).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<Mesh[]>(result, GeometryResolver.Settings);
        }

        // Other mesh compute methods can be added here
    }

    // Add other compute classes for different geometry types as needed

    /**
     * 🔄 JSON converter for Rhino geometry objects
     * 
     * Handles serialization and deserialization of Rhino geometry objects
     * to and from JSON format for API communication.
     */
    internal class GeometryResolver : JsonConverter
    {
        /// <summary>JSON serializer settings with this converter</summary>
        public static readonly JsonSerializerSettings Settings;

        /**
         * 🔧 Static constructor to initialize settings
         * 
         * Sets up the JSON serializer settings with this converter.
         */
        static GeometryResolver()
        {
            Settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            Settings.Converters.Add(new GeometryResolver());
        }

        /**
         * ✅ Checks if this converter can convert the given object type
         * 
         * @param objectType - The type to check
         * @returns True if the type inherits from GeometryBase
         */
        public override bool CanConvert(Type objectType)
        {
            return typeof(GeometryBase).IsAssignableFrom(objectType);
        }

        /**
         * 📥 Deserializes a JSON object to a Rhino geometry object
         * 
         * @param reader - JSON reader
         * @param objectType - Target type
         * @param existingValue - Existing value (if any)
         * @param serializer - JSON serializer
         * @returns Deserialized geometry object
         */
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var dictionary = serializer.Deserialize<Dictionary<string, object>>(reader);
            if (dictionary == null)
                return null;

            var jsData = dictionary["data"];
            var data = jsData.ToString();
            var geometry = GeometryBase.FromJSON(data);
            return Convert.ChangeType(geometry, objectType);
        }

        /**
         * 📤 Serializes a Rhino geometry object to JSON
         * 
         * @param writer - JSON writer
         * @param value - Geometry object to serialize
         * @param serializer - JSON serializer
         */
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var geometryBase = value as GeometryBase;
            if (geometryBase == null)
            {
                writer.WriteNull();
                return;
            }

            // Create serialization options for Rhino 8
            var options = new Rhino.FileIO.SerializationOptions();
            var data = geometryBase.ToJSON(options);
            
            var archive = new
            {
                data = data
            };
            serializer.Serialize(writer, archive);
        }
    }
}