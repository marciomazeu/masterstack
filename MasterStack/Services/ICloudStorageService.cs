namespace MasterStack.Services
{
    public interface ICloudStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderName);
        Task DeleteFileAsync(string fileUrl);
    }
}