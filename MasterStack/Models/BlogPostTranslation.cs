using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// 1. VOCÊ PRECISA DESTE USING PARA O IFormFile FUNCIONAR
using Microsoft.AspNetCore.Http;

namespace MasterStack.Models
{
    public class BlogPostTranslation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BlogPostId { get; set; }

        [ForeignKey("BlogPostId")]
        public virtual BlogPost? BlogPost { get; set; }

        [Required]
        [StringLength(15)]
        public string Culture { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Título")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Conteúdo")]
        public string Content { get; set; }

        [Required]
        [StringLength(250)]
        [Display(Name = "SEO Slug")]
        public string Slug { get; set; }

        // 2. ADICIONE ESTE CAMPO PARA GUARDAR O NOME NO BANCO
        public string? ImageUrl { get; set; }

        // 3. USE [NotMapped] PARA O EF IGNORAR ESTE CAMPO NO SQL
        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        public virtual Language? Language { get; set; }

        [StringLength(160)]
    public string? MetaDescription { get; set; } // Resumo para o Google

    public string? MetaKeywords { get; set; }
    }
}