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
            var serviceUrl = _configuration["DigitalOceanSpaces:ServiceUrl"]; // ex: https://tor1.digitaloceanspaces.com
            var bucketName = _configuration["DigitalOceanSpaces:BucketName"]; // ex: masterstackjobs-images
            var cdnUrl = _configuration["DigitalOceanSpaces:CdnUrl"];

            // 🔍 LOG DE DIAGNÓSTICO: Verifica se as chaves estão chegando no container
            _logger.LogInformation("Iniciando Upload. Bucket: {Bucket}, ServiceUrl: {Url}, KeyLength: {KeyLen}", 
                bucketName, serviceUrl, accessKey?.Length ?? 0);

            var s3Config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            };

            using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

            var fileName = $"{folderName}/{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            try
            {
                using var stream = file.OpenReadStream();
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = file.ContentType,
                    DisablePayloadSigning = true
                };

                var response = await client.PutObjectAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    return string.IsNullOrEmpty(cdnUrl)
                        ? $"https://{bucketName}.{serviceUrl.Replace("https://", "")}/{fileName}"
                        : $"{cdnUrl.TrimEnd('/')}/{fileName}";
                }

                _logger.LogError("DigitalOcean Spaces retornou status não-OK: {StatusCode}", response.HttpStatusCode);
                throw new Exception($"Erro no upload. Status: {response.HttpStatusCode}");
            }
            catch (AmazonS3Exception s3Ex)
            {
                // 💡 EXIBE O MOTIVO EXATO DA DIGITALOCEAN NO LOG DO CONTAINER
                _logger.LogError(s3Ex, "FALHA CRÍTICA S3 SPACES. StatusCode: {Status}, ErrorCode: {Code}, Message: {Msg}", 
                    s3Ex.StatusCode, s3Ex.ErrorCode, s3Ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro geral de I/O ao enviar arquivo para o Spaces.");
                throw;
            }
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