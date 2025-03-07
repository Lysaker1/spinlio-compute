using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using SpinlioCompute.Models;
using SpinlioCompute.Utils;
using Rhino.FileIO;
using Rhino.Geometry;

namespace SpinlioCompute.Services
{
    /**
     * Service responsible for handling 3D model conversions using Rhino Compute
     * Core capabilities:
     * - Converting between different 3D file formats (OBJ, STL, GLTF, etc.)
     * - Managing conversion jobs asynchronously
     * - Tracking conversion status
     * - Integrating with AWS S3 for storage
     */
    public class ModelConversionService
    {
        private readonly S3Utility _s3Utility;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, ConversionJob> _jobs = new Dictionary<string, ConversionJob>();

        /**
         * Initializes a new instance of the ModelConversionService
         * Sets up required dependencies and configurations
         * 
         * @param s3Client - AWS S3 client for file operations
         * @param configuration - Application configuration for settings
         */
        public ModelConversionService(IAmazonS3 s3Client, IConfiguration configuration)
        {
            _configuration = configuration;
            _s3Utility = new S3Utility(s3Client, configuration);
        }

        /**
         * Initiates a new model conversion job
         * This method:
         * 1. Creates a new job with unique ID
         * 2. Stores job metadata
         * 3. Starts asynchronous processing
         * 4. Returns job information to caller
         * 
         * @param modelId - Unique identifier for the model
         * @param s3Key - S3 location of the source model
         * @param sourceFormat - Format of the source file (obj, stl, etc.)
         * @param targetFormats - Array of formats to convert to
         * @returns ConversionJob with tracking information
         */
        public async Task<ConversionJob> StartConversionJob(string modelId, string s3Key, string sourceFormat, string[] targetFormats)
        {
            // Create a new job with unique ID
            var job = new ConversionJob
            {
                Id = Guid.NewGuid().ToString(),
                ModelId = modelId,
                S3Key = s3Key,
                SourceFormat = sourceFormat,
                TargetFormats = targetFormats,
                Status = "processing",
                StartTime = DateTime.UtcNow
            };

            // Store the job for status tracking
            _jobs[job.Id] = job;

            // Start the conversion process in background
            _ = Task.Run(async () => await ProcessConversion(job));

            return job;
        }

        /**
         * 📊 Gets the current status of a conversion job
         * 
         * Retrieves the status of a previously initiated conversion job
         * and returns a simplified status object suitable for API responses.
         * 
         * @param jobId - Unique identifier of the conversion job
         * @returns ConversionJobStatus with current status, outputs, and any error
         * @throws Exception if the job ID is not found
         */
        public ConversionJobStatus GetJobStatus(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                throw new Exception($"Job with ID {jobId} not found");
            }

            return new ConversionJobStatus
            {
                Status = job.Status,
                Outputs = job.Outputs,
                Error = job.Error
            };
        }

        /**
         * 🔄 Processes a model conversion job asynchronously
         * 
         * This is the core conversion method that:
         * 1. Downloads the source model from S3
         * 2. Creates a temporary local file
         * 3. Converts the model to each requested target format
         * 4. Uploads the converted files back to S3
         * 5. Updates the job status with results or errors
         * 
         * This method runs in the background and updates the job status
         * as it progresses through the conversion steps.
         * 
         * @param job - The conversion job to process
         */
        private async Task ProcessConversion(ConversionJob job)
        {
            try
            {
                // Download the model from S3
                using var modelStream = await _s3Utility.DownloadObjectAsync(job.S3Key);
                
                // Create a temp file path to save the downloaded file
                var tempFilePath = Path.GetTempFileName();
                
                using (var fileStream = File.Create(tempFilePath))
                {
                    modelStream.Position = 0;
                    await modelStream.CopyToAsync(fileStream);
                }

                // Process the model with Rhino
                var outputs = new List<ConversionOutput>();
                
                // Convert to each requested format
                foreach (var format in job.TargetFormats)
                {
                    try
                    {
                        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{format}");
                        bool success = await ConvertFile(tempFilePath, outputPath, job.SourceFormat, format);
                        
                        if (success)
                        {
                            // Upload the converted file to S3
                            string outputKey = _s3Utility.GetOutputS3Key(job.S3Key, format);
                            string contentType = GetContentType(format);
                            
                            using var outputStream = File.OpenRead(outputPath);
                            string url = await _s3Utility.UploadObjectAsync(outputStream, outputKey, contentType);
                            
                            outputs.Add(new ConversionOutput
                            {
                                Format = format,
                                Url = url,
                                S3Key = outputKey
                            });
                            
                            // Clean up the output file
                            if (File.Exists(outputPath))
                            {
                                File.Delete(outputPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other formats
                        Console.WriteLine($"Error converting to {format}: {ex.Message}");
                    }
                }

                // Clean up temp file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                // Update job status
                job.Status = outputs.Count > 0 ? "completed" : "failed";
                job.Outputs = outputs.ToArray();
                job.EndTime = DateTime.UtcNow;
                job.Error = outputs.Count == 0 ? "Failed to convert to any of the requested formats" : null;
            }
            catch (Exception ex)
            {
                // Update job status on error
                job.Status = "failed";
                job.Error = ex.Message;
                job.EndTime = DateTime.UtcNow;
                Console.WriteLine($"Conversion job {job.Id} failed: {ex.Message}");
            }
        }

        /**
         * 🔄 Converts a 3D model file from one format to another
         * 
         * This method handles the actual file format conversion using Rhino libraries.
         * It supports various source and target formats including:
         * - OBJ
         * - STL
         * - 3DM (Rhino's native format)
         * 
         * The conversion process:
         * 1. Loads the source file into a Rhino 3DM model
         * 2. Writes the model to the target format
         * 
         * @param inputPath - Path to the source file
         * @param outputPath - Path where the converted file should be written
         * @param sourceFormat - Format of the source file (e.g., "obj")
         * @param targetFormat - Desired output format (e.g., "stl")
         * @returns True if conversion was successful, false otherwise
         */
        private async Task<bool> ConvertFile(string inputPath, string outputPath, string sourceFormat, string targetFormat)
        {
            // This method handles the actual conversion logic using Rhino libraries
            
            try
            {
                sourceFormat = sourceFormat.ToLowerInvariant();
                targetFormat = targetFormat.ToLowerInvariant();
                
                // Initialize a 3DM file to work with
                var model = new File3dm();
                bool sourceLoaded = false;
                
                // Load the source file based on its format
                switch (sourceFormat)
                {
                    case "obj":
                        // In Rhino 8, the Read method has different parameters
                        var fileReadOptions = new FileReadOptions();
                        var objReadOptions = new FileObjReadOptions(fileReadOptions);
                        
                        // Correct API for Rhino 8: FileObj.Read(string path, FileObjReadOptions options, out File3dm model);
                        sourceLoaded = FileObj.Read(inputPath, objReadOptions, out model);
                        break;
                        
                    case "stl":
                        // For STL files in Rhino 8
                        // Create a default read options - in Rhino 8 we can use the parameterless Read method
                        sourceLoaded = FileStl.Read(inputPath, out model);
                        break;
                        
                    case "3dm":
                        // Direct reading of 3DM file
                        model = File3dm.Read(inputPath);
                        sourceLoaded = model != null;
                        break;
                        
                    // Add more source formats as needed
                    
                    default:
                        throw new NotSupportedException($"Source format {sourceFormat} is not supported");
                }
                
                if (!sourceLoaded || model == null)
                {
                    throw new Exception($"Failed to load source file in {sourceFormat} format");
                }
                
                // Convert to the target format
                bool conversionSuccess = false;
                
                switch (targetFormat)
                {
                    case "obj":
                        // In Rhino 8, the Write method has different parameters
                        var fileWriteOptions = new FileWriteOptions();
                        var objWriteOptions = new FileObjWriteOptions(fileWriteOptions);
                        
                        // Correct API for Rhino 8
                        conversionSuccess = FileObj.Write(outputPath, model, objWriteOptions);
                        break;
                        
                    case "stl":
                        // For STL files in Rhino 8
                        conversionSuccess = FileStl.Write(outputPath, model, new FileStlWriteOptions());
                        break;
                        
                    case "3dm":
                        conversionSuccess = model.Write(outputPath, 7);
                        break;
                        
                    case "gltf":
                    case "glb":
                        // Note: This is a placeholder - actual implementation depends on available Rhino libraries
                        // For GLB/GLTF, you might need to use a different approach or library
                        throw new NotImplementedException($"Conversion to {targetFormat} is not implemented yet");
                        
                    // Add more target formats as needed
                    
                    default:
                        throw new NotSupportedException($"Target format {targetFormat} is not supported");
                }
                
                return conversionSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during conversion: {ex.Message}");
                return false;
            }
        }

        /**
         * 📋 Determines the MIME content type for a given file format
         * 
         * Maps 3D model file format extensions to their corresponding MIME types
         * for proper content type headers when uploading to S3.
         * 
         * @param format - The file format extension (e.g., "obj", "stl")
         * @returns The corresponding MIME type string
         */
        private string GetContentType(string format)
        {
            return format.ToLowerInvariant() switch
            {
                "obj" => "model/obj",
                "stl" => "model/stl",
                "gltf" => "model/gltf+json",
                "glb" => "model/gltf-binary",
                "3dm" => "application/x-3dm",
                "fbx" => "application/octet-stream",
                _ => "application/octet-stream"
            };
        }
    }
} 