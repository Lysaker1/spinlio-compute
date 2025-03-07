using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace SpinlioCompute.Utils
{
    /**
     * 📦 S3Utility - Handles all AWS S3 storage operations for 3D models
     * 
     * This utility class encapsulates all interactions with AWS S3, providing methods
     * for uploading, downloading, and generating paths for 3D model files. It handles
     * the complexities of S3 interactions, including error handling and presigned URL generation.
     */
    public class S3Utility
    {
        /// <summary>AWS S3 client for interacting with S3 buckets</summary>
        private readonly IAmazonS3 _s3Client;
        
        /// <summary>Application configuration for accessing settings</summary>
        private readonly IConfiguration _configuration;
        
        /// <summary>Name of the S3 bucket where models are stored</summary>
        private readonly string _bucketName;

        /**
         * 🔧 Initializes a new instance of the S3Utility
         * 
         * @param s3Client - AWS S3 client instance for S3 operations
         * @param configuration - Application configuration for accessing bucket name
         */
        public S3Utility(IAmazonS3 s3Client, IConfiguration configuration)
        {
            _s3Client = s3Client;
            _configuration = configuration;
            _bucketName = _configuration["AWS:BucketName"];
        }

        /**
         * ⬇️ Downloads an object from S3 into a memory stream
         * 
         * This method:
         * 1. Retrieves the specified object from S3
         * 2. Copies the content to a memory stream
         * 3. Resets the stream position for reading
         * 
         * @param s3Key - The S3 key of the object to download
         * @returns A memory stream containing the downloaded object's data
         * @throws Exception if download fails
         */
        public async Task<Stream> DownloadObjectAsync(string s3Key)
        {
            try
            {
                // Create the request to get the object from S3
                var response = await _s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                });

                // Create a memory stream to hold the downloaded content
                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // Reset position to beginning for reading

                return memoryStream;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error downloading file from S3: {ex.Message}", ex);
            }
        }

        /**
         * ⬆️ Uploads a stream to S3 and returns a presigned URL
         * 
         * This method:
         * 1. Uploads the provided stream to S3 at the specified key
         * 2. Generates a temporary presigned URL for accessing the uploaded object
         * 
         * @param fileStream - Stream containing the file data to upload
         * @param s3Key - Destination S3 key where the object will be stored
         * @param contentType - MIME type of the content (e.g., "model/obj")
         * @returns A presigned URL for accessing the uploaded object (valid for 1 hour)
         * @throws Exception if upload fails
         */
        public async Task<string> UploadObjectAsync(Stream fileStream, string s3Key, string contentType)
        {
            try
            {
                // Prepare the upload request
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key,
                    InputStream = fileStream,
                    ContentType = contentType
                };

                // Perform the upload
                await _s3Client.PutObjectAsync(putRequest);

                // Generate a pre-signed URL for the uploaded object (valid for 1 hour)
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key,
                    Expires = DateTime.UtcNow.AddHours(1)
                };

                return _s3Client.GetPreSignedURL(urlRequest);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading file to S3: {ex.Message}", ex);
            }
        }

        /**
         * 🔀 Generates an S3 key for a converted model file
         * 
         * Creates a new S3 key based on the original key and the target format.
         * Places converted files in a "converted" subdirectory of the original path.
         * 
         * @param originalKey - Original S3 key of the source model
         * @param format - Target format extension (e.g., "glb", "gltf")
         * @returns New S3 key for the converted file
         */
        public string GetOutputS3Key(string originalKey, string format)
        {
            // Extract the directory path and ensure forward slashes
            string directory = Path.GetDirectoryName(originalKey)?.Replace('\\', '/') ?? "";
            
            // Extract just the filename without extension
            string fileName = Path.GetFileNameWithoutExtension(originalKey);
            
            // Combine into a new path with the converted subdirectory
            return $"{directory}/converted/{fileName}.{format}";
        }
    }
} 