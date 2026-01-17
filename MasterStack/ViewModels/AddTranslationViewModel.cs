namespace MasterStack.ViewModels
{
    public class AddTranslationViewModel
    {
        public int BlogPostId { get; set; }
        public string SelectedCulture { get; set; } // O idioma escolhido no dropdown
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile? ImageFile { get; set; } // O arquivo da imagem
    }
}
