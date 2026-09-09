using System.ComponentModel.DataAnnotations;
using MasterStack.Models.Enums;

namespace MasterStack.Models
{
    public class JobApplication
    {
        public int Id { get; set; }

        [Required]
        public int JobPostingId { get; set; }
        public virtual JobPosting JobPosting { get; set; } = null!;

        [Required]
        public string CandidateId { get; set; } = string.Empty;
        public virtual ApplicationUser Candidate { get; set; } = null!;

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StatusUpdatedAt { get; set; }

        // Carta de apresentação / Notas do candidato
        public string? CoverLetter { get; set; }

        // Feedback / Notas internas privadas do Recrutador
        public string? RecruiterNotes { get; set; }
        // 🔴 Novo campo para armazenar o caminho do arquivo PDF enviado
        public string? ResumePath { get; set; }
    }
}