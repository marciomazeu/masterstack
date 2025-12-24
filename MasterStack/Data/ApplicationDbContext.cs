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
    }
}
