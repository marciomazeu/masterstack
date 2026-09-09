using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class CreateJobViewModel
    {
        [Required(ErrorMessage = "O título da vaga é obrigatório.")]
        [MaxLength(150, ErrorMessage = "O título deve ter no máximo 150 caracteres.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "A descrição da vaga é obrigatória.")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;

        public bool IsRemote { get; set; }

        public decimal? MinSalary { get; set; }
        public decimal? MaxSalary { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "CAD";

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}