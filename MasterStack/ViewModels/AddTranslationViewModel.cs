using MasterStack.Attributes; // Adicione esta linha

namespace MasterStack.ViewModels
{
    public class AddTranslationViewModel
    {
        public int BlogPostId { get; set; }
        public string SelectedCulture { get; set; } // O idioma escolhido no dropdown
        public string Title { get; set; }
        public string Content { get; set; }

        [MaxFileSize(5 * 1024 * 1024)] // 5MB
        [AllowedExtensions(new string[] { ".jpg", ".jpeg", ".png", ".webp" })]
        public IFormFile? ImageFile { get; set; } // O arquivo da imagem
    }
}
