namespace TrackerGanadero.Shared.Services
{
    public interface ITokenStorageService
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value);
        void Remove(string key);
    }
}
