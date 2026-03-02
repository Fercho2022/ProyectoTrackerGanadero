using TrackerGanadero.Shared.Services;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class MauiTokenStorageService : ITokenStorageService
    {
        public Task<string?> GetAsync(string key)
        {
            return SecureStorage.GetAsync(key);
        }

        public Task SetAsync(string key, string value)
        {
            return SecureStorage.SetAsync(key, value);
        }

        public void Remove(string key)
        {
            SecureStorage.Remove(key);
        }
    }
}
