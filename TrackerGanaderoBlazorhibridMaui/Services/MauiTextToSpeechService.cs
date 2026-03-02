using TrackerGanadero.Shared.Services;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class MauiTextToSpeechService : ITextToSpeechService
    {
        private readonly ITextToSpeech _textToSpeech;

        public MauiTextToSpeechService(ITextToSpeech textToSpeech)
        {
            _textToSpeech = textToSpeech;
        }

        public async Task SpeakAsync(string text, string language = "es")
        {
            var locales = await _textToSpeech.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith(language)) ?? locales.FirstOrDefault();

            await _textToSpeech.SpeakAsync(text, new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Volume = 0.8f
            });
        }

        public async Task<IEnumerable<string>> GetLocalesAsync()
        {
            var locales = await _textToSpeech.GetLocalesAsync();
            return locales.Select(l => l.Language);
        }
    }
}
