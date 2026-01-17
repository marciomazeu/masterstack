using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models // O namespace deve ser este
{
    public class BlogPostCreateViewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile? ImageFile { get; set; }
        [Required]
        public string SelectedCulture { get; set; }

        public List<SelectListItem> AvailableCultures { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "pt-BR", Text = "Português (Brasil)" },
        new SelectListItem { Value = "en-US", Text = "English (US)" },
        new SelectListItem { Value = "fr-CA", Text = "Français" }
    };
    }
}