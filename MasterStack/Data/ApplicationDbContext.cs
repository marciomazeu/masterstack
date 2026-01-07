using MasterStack.Models;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Isso aqui vai virar a tabela de posts no banco de dados
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<BlogPostTranslation> BlogPostTranslations { get; set; }
        public DbSet<Language> Languages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // MANTENHA ESTA LINHA SEMPRE NO TOPO DO MÉTODO
            base.OnModelCreating(modelBuilder);

            // --- INSERIR DAQUI PARA BAIXO ---

            // 1. Configuração de Chaves (opcional, mas recomendado)
            modelBuilder.Entity<Language>().HasKey(l => l.Culture);

            // 2. SEED DATA: Os dados que "nascem" com o banco
            modelBuilder.Entity<Language>().HasData(
                new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br" },
                new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us" },
                new Language { Culture = "fr-FR", Name = "Français", FlagClass = "fi-ca" }
            );

            // 3. Configuração da Relação entre Tradução e Idioma
            modelBuilder.Entity<BlogPostTranslation>()
                .HasOne(t => t.Language)
                .WithMany() // Um idioma pode estar em muitas traduções
                .HasForeignKey(t => t.Culture);

            // --- ATÉ AQUI ---
        }

    }
    }
