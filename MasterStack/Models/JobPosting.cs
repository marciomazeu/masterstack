using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MasterStack.Models;

namespace MasterStack.Models
{
    public class JobPosting
    {
        public int Id { get; set; }
        public string? UserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;

        public bool IsRemote { get; set; }

        // Campos Financeiros (Opcionais)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinSalary { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxSalary { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "CAD"; // CAD, USD, BRL, EUR

        // Origem da Vaga
        public bool IsInternal { get; set; } = false; // true = Criada na plataforma / false = Agregador
        public string? RedirectUrl { get; set; } // Utilizado para vagas agregadas
        public string SourceProvider { get; set; } = "Internal";

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsExactLocation { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Indica se a vaga está ativa ou encerrada
        public bool IsClosed { get; set; } = false;

        // Registra a data exata do encerramento para calcular os 30 dias
        public DateTime? ClosedAt { get; set; }

        // Controle de Cache / Agregador (Declarados apenas uma vez)
        public string? SearchQuery { get; set; }
        public string? SearchCity { get; set; }
        public DateTime? FetchedAt { get; set; }

        // Vínculos com Recrutador e Empresa
        public int? CompanyId { get; set; }
        public virtual Company? Company { get; set; }

        public string? RecruiterId { get; set; } // Usuário recrutador que criou
        public virtual ApplicationUser? Recruiter { get; set; }

        // Relacionamento com Candidatos Inscritos
        public virtual ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}