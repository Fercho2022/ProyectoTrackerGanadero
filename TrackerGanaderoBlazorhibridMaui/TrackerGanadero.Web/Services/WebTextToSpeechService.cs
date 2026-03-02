using Microsoft.JSInterop;
using TrackerGanadero.Shared.Services;

namespace TrackerGanadero.Web.Services
{
    public class WebTextToSpeechService : ITextToSpeechService
    {
        private readonly IJSRuntime _jsRuntime;

        public WebTextToSpeechService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task SpeakAsync(string text, string language = "es")
        {
            await _jsRuntime.InvokeVoidAsync("eval", $@"
                const utterance = new SpeechSynthesisUtterance('{text.Replace("'", "\\'")}');
                utterance.lang = '{language}';
                utterance.rate = 1.0;
                utterance.volume = 0.8;
                speechSynthesis.speak(utterance);
            ");
        }

        public async Task<IEnumerable<string>> GetLocalesAsync()
        {
            var locales = await _jsRuntime.InvokeAsync<string[]>("eval", @"
                speechSynthesis.getVoices().map(v => v.lang)
            ");
            return locales ?? Array.Empty<string>();
        }
    }
}
