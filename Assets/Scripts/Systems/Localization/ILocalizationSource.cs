using System;

public interface ILocalizationSource
{
    event Action OnLanguageChanged;

    string GetLocalizedText(string key);
    void SetLanguage(string language);
    string CurrentLanguage { get; }
}
