using Microsoft.JSInterop;
using TrackerGanadero.Shared.Services;

namespace TrackerGanadero.Web.Services
{
    public class WebTokenStorageService : ITokenStorageService
    {
        private readonly IJSRuntime _jsRuntime;

        public WebTokenStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<string?> GetAsync(string key)
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }

        public async Task SetAsync(string key, string value)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }

        public void Remove(string key)
        {
            _ = _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
    }
}
