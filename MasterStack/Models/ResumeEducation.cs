using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    
    public class ResumeEducation
    {
        public int Id { get; set; }

        public int ResumeId { get; set; }

        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }
        public string Culture { get; set; } = "pt-BR";

        [Required]
        public string Institution { get; set; } = string.Empty;

        [Required]
        public string Degree { get; set; } = string.Empty; // Ex: Bacharel em Ciência da Computação

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

}