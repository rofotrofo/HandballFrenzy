using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LanguageDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null)
        {
            Debug.LogError("Dropdown is not assigned and could not be found automatically.");
            return;
        }

        InitializeDropdown();
        dropdown.onValueChanged.AddListener(OnLanguageSelected);
    }

    private void Start()
    {
        LocalizationManager.Source.OnLanguageChanged += UpdateDropdownLabels;
        UpdateDropdownLabels();
    }

    private void OnDestroy()
    {
        LocalizationManager.Source.OnLanguageChanged -= UpdateDropdownLabels;
        dropdown.onValueChanged.RemoveListener(OnLanguageSelected);
    }

    private void InitializeDropdown()
    {
        List<string> options = new List<string>();

        foreach (string key in LocalizationExtensions.LanguageKeys)
        {
            options.Add(key.Localize());
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);

        dropdown.value = GetCurrentLanguageIndex();
        dropdown.RefreshShownValue();
    }

    private void OnLanguageSelected(int index)
    {
        LocalizationManager.Source.SetLanguage(LocalizationExtensions.LanguageKeys[index]);
    }

    private void UpdateDropdownLabels()
    {
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var key in LocalizationExtensions.LanguageKeys)
            options.Add(new TMP_Dropdown.OptionData(key.Localize()));

        dropdown.options = options;
        dropdown.captionText.text = options[GetCurrentLanguageIndex()].text;
    }

    private int GetCurrentLanguageIndex()
    {
        string current = LocalizationManager.Source.CurrentLanguage;
        for (int i = 0; i < LocalizationExtensions.LanguageKeys.Length; i++)
        {
            if (LocalizationExtensions.LanguageKeys[i] == current)
                return i;
        }
        return 0;
    }
}
