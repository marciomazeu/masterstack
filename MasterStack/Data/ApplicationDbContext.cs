using MasterStack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MasterStack.Data
{
    // 1. IMPORTANTE: Usar ApplicationUser para que o Identity entenda seus campos customizados
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<BlogPostTranslation> BlogPostTranslations { get; set; }
        public DbSet<Language> Languages { get; set; }

        public DbSet<StaticPage> StaticPages { get; set; }
        public DbSet<StaticPageTranslation> StaticPageTranslations { get; set; }

        // O DbSet de AuthorProfile FOI REMOVIDO daqui (Opção 2)

        public DbSet<UserTranslation> UserTranslations { get; set; }

        public DbSet<Company> Companies { get; set; }

        public DbSet<JobPosting> JobPostings { get; set; }
        public DbSet<AffiliateProduct> AffiliateProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // MANTENHA ESTA LINHA SEMPRE NO TOPO (Configura as tabelas do Identity)
            base.OnModelCreating(modelBuilder);

            // Força todas as propriedades DateTime a serem tratadas como UTC no Postgres
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                }
            }

            // 1. Configuração de Chaves para Idiomas
            modelBuilder.Entity<Language>().HasKey(l => l.Culture);

            // 2. SEED DATA: Idiomas iniciais
            modelBuilder.Entity<Language>().HasData(
                new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br" },
                new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us" },
                new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca" }
            );

            // 3. Relacionamento Tradução -> Idioma
            modelBuilder.Entity<BlogPostTranslation>()
                .HasOne(t => t.Language)
                .WithMany()
                .HasForeignKey(t => t.Culture);

            // 4. Índice Único: Impede duplicata de tradução para o mesmo post e idioma
            modelBuilder.Entity<BlogPostTranslation>()
                .HasIndex(t => new { t.BlogPostId, t.Culture })
                .IsUnique();

            // 5. Configuração da Relação Post -> Autor (ApplicationUser)
            modelBuilder.Entity<BlogPost>()
                .HasOne(p => p.Author)
                .WithMany(u => u.BlogPosts)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Restrict); 
                // Restrict: Se deletar o autor, o post não some automaticamente (segurança)

            //slug unico de cada página
                modelBuilder.Entity<StaticPage>()
                .HasIndex(p => p.Slug)
                .IsUnique();
        }
    }
}