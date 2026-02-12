using System.Net.Http.Headers;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    /// <summary>
    /// DelegatingHandler que agrega automáticamente el token de autenticación a todas las peticiones HTTP
    /// </summary>
    public class AuthHeaderHandler : DelegatingHandler
    {
        private const string TokenKey = "auth_token";

        public AuthHeaderHandler()
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Obtener el token de SecureStorage
                var token = await SecureStorage.GetAsync(TokenKey);

                // Si hay token, agregarlo al header Authorization
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] Token agregado a request: {request.RequestUri}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] No token found for request: {request.RequestUri}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] Error getting token: {ex.Message}");
            }

            // Continuar con la petición
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
