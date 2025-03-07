using System;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SpinlioCompute.Models;
using SpinlioCompute.Services;
using Xunit;

namespace SpinlioCompute.Tests
{
    public class ModelConversionServiceTests
    {
        private readonly Mock<IAmazonS3> _mockS3Client;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<ModelConversionService>> _mockLogger;

        public ModelConversionServiceTests()
        {
            _mockS3Client = new Mock<IAmazonS3>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ModelConversionService>>();
            
            // Setup configuration
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(x => x.Value).Returns("test-bucket");
            _mockConfiguration.Setup(x => x["AWS:BucketName"]).Returns("test-bucket");
        }

        [Fact]
        public async Task StartConversionJob_ValidInputs_ReturnsJobWithProcessingStatus()
        {
            // Arrange
            var service = new ModelConversionService(_mockS3Client.Object, _mockConfiguration.Object);
            string modelId = "test-model-123";
            string s3Key = "models/test.obj";
            string sourceFormat = "obj";
            string[] targetFormats = new[] { "glb", "gltf" };

            // Act
            var result = await service.StartConversionJob(modelId, s3Key, sourceFormat, targetFormats);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Id);
            Assert.Equal(modelId, result.ModelId);
            Assert.Equal(s3Key, result.S3Key);
            Assert.Equal(sourceFormat, result.SourceFormat);
            Assert.Equal(targetFormats, result.TargetFormats);
            Assert.Equal("processing", result.Status);
        }

        [Fact]
        public void GetJobStatus_UnknownJobId_ThrowsException()
        {
            // Arrange
            var service = new ModelConversionService(_mockS3Client.Object, _mockConfiguration.Object);
            string unknownJobId = "unknown-job-id";

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => service.GetJobStatus(unknownJobId));
            Assert.Contains(unknownJobId, exception.Message);
        }

        [Fact]
        public async Task GetJobStatus_KnownJobId_ReturnsCorrectStatus()
        {
            // Arrange
            var service = new ModelConversionService(_mockS3Client.Object, _mockConfiguration.Object);
            string modelId = "test-model-456";
            string s3Key = "models/test2.obj";
            string sourceFormat = "obj";
            string[] targetFormats = new[] { "glb" };
            
            var job = await service.StartConversionJob(modelId, s3Key, sourceFormat, targetFormats);

            // Act
            var status = service.GetJobStatus(job.Id);

            // Assert
            Assert.NotNull(status);
            Assert.Equal("processing", status.Status);
            Assert.Null(status.Error);
        }
    }
} 