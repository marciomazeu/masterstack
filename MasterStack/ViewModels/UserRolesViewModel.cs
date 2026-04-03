namespace MasterStack.Models
{
    public class UserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public IEnumerable<string> Roles { get; set; } = new List<string>();
    }
}