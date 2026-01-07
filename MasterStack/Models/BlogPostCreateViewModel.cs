using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models // O namespace deve ser este
{
    public class BlogPostCreateViewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}