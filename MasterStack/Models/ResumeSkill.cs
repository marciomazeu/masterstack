using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    public class ResumeSkill
    {
        public int Id { get; set; }

        public int ResumeId { get; set; }
        public string? Culture { get; set; } // ex: "pt-BR", "fr-CA", "en-US"

        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // Ex: C#, PostgreSQL, React
    }
}