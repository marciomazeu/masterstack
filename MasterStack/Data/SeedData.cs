using MasterStack.Models;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                // Se já existirem idiomas, não faz nada
                if (context.Languages.Any()) return;

                context.Languages.AddRange(
                    new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br",IsActive = true },
                    new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us",IsActive = true },
                    new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca",IsActive = true }
                );

                context.SaveChanges();
            }
        }
    }
}