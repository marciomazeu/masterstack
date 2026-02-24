using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    public class AuthorProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // ID do IdentityUser

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        public string? Bio { get; set; }

        public string? ProfileImageUrl { get; set; }

        public string? TwitterUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? GitHubUrl { get; set; }
    }
}