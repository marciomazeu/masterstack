using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class EditTranslationViewModel
    {
        public int TranslationId { get; set; }
        public string Culture { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string? CurrentImageUrl { get; set; }
        public IFormFile? NewImage { get; set; } // Imagem específica deste idioma
    }
}
