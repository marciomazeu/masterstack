using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
public class ResumeTranslation
    {
        public int Id { get; set; }
        public int ResumeId { get; set; }
        public Resume Resume { get; set; }

        public string Culture { get; set; } // "pt-BR", "en-US", "fr-CA"
        public string JobTitle { get; set; } // ex: "Desenvolvedor Full Stack" vs "Full Stack Developer"
        public string Summary { get; set; }  // Resumo / Perfil Profissional
    }
}