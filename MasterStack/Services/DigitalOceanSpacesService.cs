using Amazon.S3;
using Amazon.S3.Model;

namespace MasterStack.Services
{
    public class DigitalOceanSpacesService : ICloudStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DigitalOceanSpacesService> _logger;

        public DigitalOceanSpacesService(IConfiguration configuration, ILogger<DigitalOceanSpacesService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0) return string.Empty;

            var accessKey = _configuration["DigitalOceanSpaces:AccessKey"];
            var secretKey = _configuration["DigitalOceanSpaces:SecretKey"];
            var serviceUrl = _configuration["DigitalOceanSpaces:ServiceUrl"];
            var bucketName = _configuration["DigitalOceanSpaces:BucketName"];
            var cdnUrl = _configuration["DigitalOceanSpaces:CdnUrl"];

            var s3Config = new AmazonS3Config { ServiceURL = serviceUrl };
            using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

            var fileName = $"{folderName}/{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = stream,
                ContentType = file.ContentType,
                CannedACL = S3CannedACL.PublicRead
            };

            var response = await client.PutObjectAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                return string.IsNullOrEmpty(cdnUrl)
                    ? $"https://{bucketName}.{serviceUrl.Replace("https://", "")}/{fileName}"
                    : $"{cdnUrl.TrimEnd('/')}/{fileName}";
            }

            _logger.LogError("Falha ao enviar arquivo para o DigitalOcean Spaces.");
            throw new Exception("Erro ao salvar o arquivo no armazenamento em nuvem.");
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            try
            {
                var accessKey = _configuration["DigitalOceanSpaces:AccessKey"];
                var secretKey = _configuration["DigitalOceanSpaces:SecretKey"];
                var serviceUrl = _configuration["DigitalOceanSpaces:ServiceUrl"];
                var bucketName = _configuration["DigitalOceanSpaces:BucketName"];

                var uri = new Uri(fileUrl);
                var key = uri.AbsolutePath.TrimStart('/');

                var s3Config = new AmazonS3Config { ServiceURL = serviceUrl };
                using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

                await client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível remover a imagem do Spaces: {Url}", fileUrl);
            }
        }
    }
}