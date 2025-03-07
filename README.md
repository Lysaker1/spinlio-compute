# Spinlio Compute

A specialized Rhino Compute server for 3D model conversion and parametric design operations, serving as a microservice for the Spinlio platform.

## Overview

Spinlio Compute is a .NET 6.0 Web API application that provides a RESTful interface to Rhino's computational geometry capabilities. It handles:

- 3D model format conversions (OBJ, STL, GLTF, GLB, 3DM, etc.)
- Parametric design operations using Grasshopper definitions
- Mesh optimization and processing

The service is designed to run on Windows (as required by Rhino) and integrates with AWS S3 for file storage.

## Requirements

- Windows OS (Windows 10/11 or Windows Server 2019/2022)
- .NET 6.0 SDK
- Rhino 7 WIP (for Rhino Compute)
- AWS account with S3 bucket

## Setup and Installation

### Local Development

1. Clone this repository:
   ```
   git clone https://github.com/Lysaker1/spinlio-compute.git
   cd spinlio-compute
   ```

2. Install dependencies:
   ```
   dotnet restore
   ```

3. Configure your environment:
   - Copy `src/appsettings.Development.json` to create your own local settings
   - Update the AWS and Rhino settings as needed

4. Start Rhino Compute locally:
   ```
   "C:\Program Files\Rhino 7 WIP\System\Rhino.Compute.exe" --port 6500
   ```

5. Run the application:
   ```
   dotnet run --project src/SpinlioCompute.csproj
   ```

### Production Deployment

#### Using Docker

1. Build the Docker image:
   ```
   docker build -t spinlio-compute .
   ```

2. Run the container:
   ```
   docker run -p 8080:80 -e AWS__BucketName=your-bucket -e RhinoCompute__ApiKey=your-api-key spinlio-compute
   ```

#### Using EC2 or Azure VM

1. Create a Windows Server VM
2. Install .NET 6.0 Runtime
3. Install Rhino 7 WIP and ensure your license is activated
4. Configure Rhino Compute as a Windows Service
5. Deploy the application as an IIS site

## API Endpoints

### Model Conversion

#### Start Conversion

```
POST /api/convert
```

Request body:
```json
{
  "modelId": "unique-model-identifier",
  "s3Key": "path/to/model.obj",
  "sourceFormat": "obj",
  "targetFormats": ["glb", "gltf"]
}
```

Response:
```json
{
  "jobId": "conversion-job-id"
}
```

#### Check Conversion Status

```
GET /api/convert/status/{jobId}
```

Response:
```json
{
  "status": "completed", // "processing", "failed", "completed"
  "outputs": [
    {
      "format": "glb",
      "url": "https://s3-presigned-url-to-file.glb",
      "s3Key": "path/to/converted/model.glb"
    }
  ],
  "error": null
}
```

### Grasshopper Operations

```
POST /api/grasshopper
```

Request body:
```json
{
  "definitionName": "ParametricCube.gh",
  "parameters": {
    "Width": 100,
    "Height": 150,
    "Depth": 200
  }
}
```

Response:
```json
{
  "success": true,
  "meshes": [...], // Array of 3D mesh data
  "outputs": {
    "Volume": 3000000,
    "SurfaceArea": 97000
  }
}
```

## Architecture

The application follows a clean architecture with separation of concerns:

- **Controllers**: Handle HTTP requests and responses
- **Services**: Contain business logic
- **Models**: Define data structures
- **Utils**: Provide helper functions

## AWS Integration

Spinlio Compute integrates with AWS S3 for storing input models and output conversions. The service requires:

- An S3 bucket with appropriate CORS configuration
- IAM credentials with permissions to read and write to the bucket

## License and Rhino Integration

This application requires a valid Rhino license to operate. Rhino Compute uses Rhino's core functionality, so appropriate licensing must be in place for production use.

## Development and Contributions

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a new branch for your feature
3. Add tests for your changes
4. Submit a pull request

## Troubleshooting

Common issues:

- **Rhino Compute Connection Errors**: Ensure Rhino Compute is running and accessible
- **AWS S3 Access Issues**: Verify IAM permissions and bucket configuration
- **Model Conversion Failures**: Check supported formats and file integrity

## Contact

For questions or support, contact us at support@spinlio.com 