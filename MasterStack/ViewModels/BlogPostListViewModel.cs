using MasterStack.Models; // Certifique-se que este é o namespace das suas classes BlogPost e BlogPostTranslation

namespace MasterStack.ViewModels
{
    public class BlogPostListViewModel
    {
        // A lista de posts que serão exibidos na página atual
        public IEnumerable<BlogPost> Posts { get; set; } = new List<BlogPost>();

        // Dados de controle da Paginação
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string Culture { get; set; }

        // Propriedades auxiliares para facilitar a criação dos botões "Voltar" e "Avançar" na View
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}