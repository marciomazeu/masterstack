namespace MasterStack.ViewModels
{
    public class UserListViewModel
    {
        public required string Id { get; set; }
        public required string DisplayName { get; set; }
        public required string Email { get; set; }
        public required IEnumerable<string> Roles { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsLockedOut { get; set; }
    }
}