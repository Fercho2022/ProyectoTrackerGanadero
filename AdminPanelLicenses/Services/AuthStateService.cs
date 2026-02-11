namespace AdminPanelLicenses.Services
{
    public class AuthStateService
    {
        public event Action? OnAuthStateChanged;

        public bool IsAuthenticated { get; private set; }
        public string? Username { get; private set; }
        public string? Role { get; private set; }

        public void SetAuthenticated(string username, string role)
        {
            IsAuthenticated = true;
            Username = username;
            Role = role;
            OnAuthStateChanged?.Invoke();
        }

        public void SetUnauthenticated()
        {
            IsAuthenticated = false;
            Username = null;
            Role = null;
            OnAuthStateChanged?.Invoke();
        }
    }
}
