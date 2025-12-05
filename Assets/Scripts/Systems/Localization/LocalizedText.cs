using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;
    private TMP_Text textComponent;
    private ILocalizationSource localizationSource;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        localizationSource = LocalizationManager.Source;
    }

    private void OnEnable()
    {
        localizationSource.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void OnDisable()
    {
        localizationSource.OnLanguageChanged -= UpdateText;
    }

    private void UpdateText()
    {
        textComponent.text = localizationSource.GetLocalizedText(localizationKey);
    }

    public void SetKey(string key)
    {
        localizationKey = key;
        UpdateText();
    }
}
