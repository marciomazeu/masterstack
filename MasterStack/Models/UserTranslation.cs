using MasterStack.Models;

public class UserTranslation
{
    public int Id { get; set; }
    
    // Chave estrangeira para o usuário
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }

    // Identificador da cultura (ex: "en-US", "pt-BR")
    public string Culture { get; set; }

    // Campos traduzíveis
    public string? Biography { get; set; }
    public string? Title { get; set; } // Ex: "Editor", "Escritor"
}