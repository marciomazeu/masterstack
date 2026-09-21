using Amazon.S3;
using Amazon.S3.Model;
using SkiaSharp;
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

    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
    var cleanFileName = $"{folderName}/{Guid.NewGuid()}_{fileNameWithoutExt}.webp";

    var s3Config = new AmazonS3Config
    {
        ServiceURL = serviceUrl,
        ForcePathStyle = true
    };

    using var client = new AmazonS3Client(accessKey, secretKey, s3Config);
    using var outputStream = new MemoryStream();

    try
    {
        using var inputStream = file.OpenReadStream();
        using var originalBitmap = SKBitmap.Decode(inputStream);

        // Redimensiona para no máximo 1200px de largura mantendo a proporção
        int targetWidth = originalBitmap.Width;
        int targetHeight = originalBitmap.Height;

        if (originalBitmap.Width > 1200)
        {
            targetWidth = 1200;
            targetHeight = (int)(originalBitmap.Height * (1200.0 / originalBitmap.Width));
        }

        using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
        using var image = SKImage.FromBitmap(resizedBitmap ?? originalBitmap);
        
        // Codifica para WebP com 80% de qualidade
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        data.SaveTo(outputStream);
        outputStream.Position = 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao processar e comprimir imagem com SkiaSharp. Enviando arquivo original.");
        await file.CopyToAsync(outputStream);
        outputStream.Position = 0;
    }

    var request = new PutObjectRequest
    {
        BucketName = bucketName,
        Key = cleanFileName,
        InputStream = outputStream,
        ContentType = "image/webp",
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