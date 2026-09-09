using MasterStack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Data
{
    public static class SeedData
    {
        public static async Task SeedLanguagesAndRolesAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed de Idiomas
            var seedLanguages = new List<Language>
            {
                new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br", IsActive = true },
                new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us", IsActive = true },
                new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca", IsActive = true }
            };

            foreach (var lang in seedLanguages)
            {
                if (!await context.Languages.AnyAsync(l => l.Culture == lang.Culture))
                {
                    await context.Languages.AddAsync(lang);
                }
            }
            await context.SaveChangesAsync();

            // 2. Seed de Roles (Atualizado com Recruiter e Candidate)
            string[] roles = { "Admin", "Author", "User", "Recruiter", "Candidate" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 3. Seed do Usuário Administrador
            string adminEmail = "seu-email-real@dominio.com";
            
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser 
                { 
                    UserName = adminEmail, 
                    Email = adminEmail, 
                    DisplayName = "Admin MasterStack", 
                    EmailConfirmed = true 
                };
                
                var result = await userManager.CreateAsync(adminUser, "SenhaProvisoria#2026!Secured");
                if (result.Succeeded) 
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}