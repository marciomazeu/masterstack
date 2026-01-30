using MasterStack.Models;

namespace MasterStack.ViewModels
{
    public class DashboardViewModel
    {
        public List<BlogPost> Posts { get; set; }
        public int PaginaAtual { get; set; }
        public int TotalPaginas { get; set; }
        public bool TemPaginaAnterior => PaginaAtual > 1;
        public bool TemProximaPagina => PaginaAtual < TotalPaginas;
    }
}
