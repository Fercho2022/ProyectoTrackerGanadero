namespace TrackerGanadero.Shared.Services
{
    public interface ITextToSpeechService
    {
        Task SpeakAsync(string text, string language = "es");
        Task<IEnumerable<string>> GetLocalesAsync();
    }
}
