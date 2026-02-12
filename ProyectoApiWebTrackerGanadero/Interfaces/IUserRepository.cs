using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> AddAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(User user);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetUserWithFarmsAsync(int id);
        Task<bool> EmailExistsAsync(string email);
    }
}
