namespace MasterStack.Models
{
    public class Language
    {
        public int Id { get; set; }
        public string Name { get; set; }  // Ex: "English", "Português"
        public string Code { get; set; }  // Ex: "en-US", "pt-BR"
        public string? FlagIcon { get; set; } // Opcional: ícone ou classe CSS
        public bool IsActive { get; set; } = true;
    }
}
