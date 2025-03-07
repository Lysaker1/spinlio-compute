using System;
using System.Collections.Generic;

namespace SpinlioCompute.Models
{
    /**
     * 📄 Represents an incoming request to convert a 3D model
     * 
     * This model is used when clients send a request to start a model conversion process.
     * It contains all the information needed to locate the source model and specify the 
     * desired output formats.
     */
    public class ConversionRequest
    {
        /// <summary>Unique identifier for the model in the client system</summary>
        public string ModelId { get; set; }
        
        /// <summary>S3 key where the source model is stored</summary>
        public string S3Key { get; set; }
        
        /// <summary>File format of the source model (e.g., "obj", "stl")</summary>
        public string SourceFormat { get; set; }
        
        /// <summary>Array of target formats to convert to (e.g., ["glb", "gltf"])</summary>
        public string[] TargetFormats { get; set; }
    }

    /**
     * 🔄 Represents an active or completed conversion job
     * 
     * This model tracks the full lifecycle of a conversion job, from initiation
     * through processing to completion. It stores both input parameters and outputs,
     * as well as timing and status information.
     */
    public class ConversionJob
    {
        /// <summary>Unique identifier for this conversion job</summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>Client-provided model identifier (from the request)</summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>S3 key of the source model</summary>
        public string S3Key { get; set; } = string.Empty;
        
        /// <summary>Format of the source model file</summary>
        public string SourceFormat { get; set; } = string.Empty;
        
        /// <summary>Requested output formats</summary>
        public string[] TargetFormats { get; set; } = Array.Empty<string>();
        
        /// <summary>Current status: "pending", "processing", "completed", or "failed"</summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>When the job was initiated</summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>When the job finished (or null if still in progress)</summary>
        public DateTime? EndTime { get; set; }
        
        /// <summary>Array of successfully converted outputs</summary>
        public ConversionOutput[] Outputs { get; set; } = Array.Empty<ConversionOutput>();
        
        /// <summary>Error message if the job failed</summary>
        public string? Error { get; set; }
    }

    /**
     * 📊 Represents the current status of a conversion job
     * 
     * This is a simplified view of ConversionJob that contains only the
     * information needed for status updates to clients.
     */
    public class ConversionJobStatus
    {
        /// <summary>Current status: "pending", "processing", "completed", or "failed"</summary>
        public string Status { get; set; }
        
        /// <summary>Array of successfully converted outputs (only present when status is "completed")</summary>
        public ConversionOutput[] Outputs { get; set; }
        
        /// <summary>Error message if the job failed</summary>
        public string Error { get; set; }
    }

    /**
     * 📦 Represents a single converted output file
     * 
     * Contains information about where the converted file is stored and
     * how to access it.
     */
    public class ConversionOutput
    {
        /// <summary>Format of this output file (e.g., "glb")</summary>
        public string Format { get; set; }
        
        /// <summary>Pre-signed URL to access this file (temporary)</summary>
        public string Url { get; set; }
        
        /// <summary>S3 key where this output is stored</summary>
        public string S3Key { get; set; }
    }
} 