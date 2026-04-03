using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    // ApplicationUser herda tudo do Identity (Email, Senha, Id, etc.)
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [Required, StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [PersonalData]
        public string? ProfileImageUrl { get; set; }

        // Campos específicos para quem é AUTOR
        [PersonalData]
        public string? Bio { get; set; }
        
        public string? TwitterUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? GitHubUrl { get; set; }

        // Relacionamento: Um usuário pode ter muitos posts (se for Autor/Admin)
        public virtual ICollection<BlogPost>? BlogPosts { get; set; }
        
        // Relacionamento: Um usuário pode ter muitos comentários
        // public virtual ICollection<Comment>? Comments { get; set; }
    }
}