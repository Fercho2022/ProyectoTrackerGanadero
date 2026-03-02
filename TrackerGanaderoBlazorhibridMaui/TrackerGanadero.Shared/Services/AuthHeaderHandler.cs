using System.Net.Http.Headers;

namespace TrackerGanadero.Shared.Services
{
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly ITokenStorageService _tokenStorage;
        private const string TokenKey = "auth_token";

        public AuthHeaderHandler(ITokenStorageService tokenStorage)
        {
            _tokenStorage = tokenStorage;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                var token = await _tokenStorage.GetAsync(TokenKey);

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] Error getting token: {ex.Message}");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
