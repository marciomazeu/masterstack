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
            if (file == null || file.Length == 0) return null;

            var accessKey = _configuration["DigitalOceanSpaces:AccessKey"];
            var secretKey = _configuration["DigitalOceanSpaces:SecretKey"];
            var serviceUrl = _configuration["DigitalOceanSpaces:ServiceUrl"];
            var bucketName = _configuration["DigitalOceanSpaces:BucketName"];
            var cdnUrl = _configuration["DigitalOceanSpaces:CdnUrl"];

            var cleanFileName = $"{folderName}/{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            var s3Config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            };

            using var client = new AmazonS3Client(accessKey, secretKey, s3Config);
            using var stream = file.OpenReadStream();

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = cleanFileName,
                InputStream = stream,
                ContentType = file.ContentType,
                // 💡 DEFINE PERMISSÃO DE LEITURA PÚBLICA PARA O ARQUIVO NO SPACES
                CannedACL = S3CannedACL.PublicRead 
            };

            var response = await client.PutObjectAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                if (!string.IsNullOrEmpty(cdnUrl))
                {
                    return $"{cdnUrl.TrimEnd('/')}/{cleanFileName}";
                }

                var cleanServiceUrl = serviceUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');
                return $"https://{bucketName}.{cleanServiceUrl}/{cleanFileName}";
            }

            return null;
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
        // Extrai o caminho sem a primeira barra (ex: "blog/guid_nome.jpg")
        var key = uri.AbsolutePath.TrimStart('/'); 

        var s3Config = new AmazonS3Config 
        { 
            ServiceURL = serviceUrl,
            ForcePathStyle = true 
        };
        
        using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        });

        _logger.LogInformation("Imagem antiga removida do Spaces: {Key}", key);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Não foi possível remover a imagem antiga do Spaces: {Url}", fileUrl);
    }
}
    }
}