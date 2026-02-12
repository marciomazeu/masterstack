using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class Language
    {
        [Key]// Isso garante que o EF crie uma Primary Key
        [StringLength(15)]
    public int Id { get; set; }
        public string Culture { get; set; } // Mude de Code para Culture aqui
        public string Name { get; set; }
        public string FlagClass { get; set; }
        public bool IsActive { get; set; }

    }
}
