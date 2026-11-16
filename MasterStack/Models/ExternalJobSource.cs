using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public enum JobSourceProviderType
    {
        Greenhouse = 1,
        Lever = 2,
        Ashby = 3,
        CustomJson = 4
    }

    public class ExternalJobSource
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome da fonte é obrigatório.")]
        [StringLength(100)]
        [Display(Name = "Nome da Empresa / Fonte")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tipo de Provedor")]
        public JobSourceProviderType ProviderType { get; set; }

        [Required(ErrorMessage = "A URL ou Slug do board é obrigatório.")]
        [StringLength(500)]
        [Display(Name = "Endpoint JSON ou Slug do Board")]
        public string EndpointOrSlug { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Filtro de Localização (separado por vírgula)")]
        public string? LocationFilter { get; set; }

        [StringLength(200)]
        [Display(Name = "Filtro de Palavras-Chave TI (separado por vírgula)")]
        public string? TechKeywordsFilter { get; set; }

        // Mapeamento dinâmico (usado quando ProviderType == CustomJson)
        [StringLength(100)]
        public string? JsonRootArrayPath { get; set; }

        [StringLength(100)]
        public string? MapTitleField { get; set; }

        [StringLength(100)]
        public string? MapCompanyField { get; set; }

        [StringLength(100)]
        public string? MapLocationField { get; set; }

        [StringLength(100)]
        public string? MapDescriptionField { get; set; }

        [StringLength(100)]
        public string? MapUrlField { get; set; }

        // Telemetria e Controle
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncedAt { get; set; }
        public int LastImportedCount { get; set; } = 0;
        public string? LastErrorMessage { get; set; }
    }
}