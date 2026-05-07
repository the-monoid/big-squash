using System;
using UnityEngine;

namespace Steading.Player
{
    [Serializable]
    public struct CharacterCustomization
    {
        private const string PrefKey = "Steading.Character.";

        public string characterName;
        public Color skinColor;
        public Color hairColor;
        public Color tunicColor;
        public Color pantsColor;
        public Color cloakColor;
        public float heightScale;
        public float buildScale;
        public bool beardEnabled;
        public bool helmetEnabled;

        public static CharacterCustomization Default => new CharacterCustomization
        {
            characterName = "Eirik",
            skinColor = new Color(0.70f, 0.50f, 0.37f),
            hairColor = new Color(0.19f, 0.12f, 0.07f),
            tunicColor = new Color(0.13f, 0.32f, 0.27f),
            pantsColor = new Color(0.15f, 0.19f, 0.24f),
            cloakColor = new Color(0.15f, 0.23f, 0.34f),
            heightScale = 1.0f,
            buildScale = 1.0f,
            beardEnabled = true,
            helmetEnabled = true
        };

        public static CharacterCustomization LoadLocal()
        {
            var fallback = Default;
            var loaded = new CharacterCustomization
            {
                characterName = PlayerPrefs.GetString(PrefKey + "Name", fallback.characterName),
                skinColor = LoadColor("Skin", fallback.skinColor),
                hairColor = LoadColor("Hair", fallback.hairColor),
                tunicColor = LoadColor("Tunic", fallback.tunicColor),
                pantsColor = LoadColor("Pants", fallback.pantsColor),
                cloakColor = LoadColor("Cloak", fallback.cloakColor),
                heightScale = PlayerPrefs.GetFloat(PrefKey + "Height", fallback.heightScale),
                buildScale = PlayerPrefs.GetFloat(PrefKey + "Build", fallback.buildScale),
                beardEnabled = PlayerPrefs.GetInt(PrefKey + "Beard", fallback.beardEnabled ? 1 : 0) == 1,
                helmetEnabled = PlayerPrefs.GetInt(PrefKey + "Helmet", fallback.helmetEnabled ? 1 : 0) == 1
            };
            return loaded.Sanitized();
        }

        public void SaveLocal()
        {
            var sanitized = Sanitized();
            PlayerPrefs.SetString(PrefKey + "Name", sanitized.characterName);
            SaveColor("Skin", sanitized.skinColor);
            SaveColor("Hair", sanitized.hairColor);
            SaveColor("Tunic", sanitized.tunicColor);
            SaveColor("Pants", sanitized.pantsColor);
            SaveColor("Cloak", sanitized.cloakColor);
            PlayerPrefs.SetFloat(PrefKey + "Height", sanitized.heightScale);
            PlayerPrefs.SetFloat(PrefKey + "Build", sanitized.buildScale);
            PlayerPrefs.SetInt(PrefKey + "Beard", sanitized.beardEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefKey + "Helmet", sanitized.helmetEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public CharacterCustomization Sanitized()
        {
            var fallback = Default;
            var result = this;
            result.characterName = SanitizeName(string.IsNullOrWhiteSpace(characterName) ? fallback.characterName : characterName);
            result.skinColor = SanitizeColor(skinColor, fallback.skinColor);
            result.hairColor = SanitizeColor(hairColor, fallback.hairColor);
            result.tunicColor = SanitizeColor(tunicColor, fallback.tunicColor);
            result.pantsColor = SanitizeColor(pantsColor, fallback.pantsColor);
            result.cloakColor = SanitizeColor(cloakColor, fallback.cloakColor);
            result.heightScale = Mathf.Clamp(heightScale <= 0f ? fallback.heightScale : heightScale, 0.92f, 1.10f);
            result.buildScale = Mathf.Clamp(buildScale <= 0f ? fallback.buildScale : buildScale, 0.88f, 1.14f);
            return result;
        }

        private static string SanitizeName(string value)
        {
            value = value.Trim();
            if (value.Length > 18) value = value.Substring(0, 18);
            return value.Length == 0 ? Default.characterName : value;
        }

        private static Color SanitizeColor(Color color, Color fallback)
        {
            if (color.maxColorComponent <= 0f && color.a <= 0f) return fallback;
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = 1f;
            return color;
        }

        private static Color LoadColor(string name, Color fallback)
        {
            return new Color(
                PlayerPrefs.GetFloat(PrefKey + name + "R", fallback.r),
                PlayerPrefs.GetFloat(PrefKey + name + "G", fallback.g),
                PlayerPrefs.GetFloat(PrefKey + name + "B", fallback.b),
                1f);
        }

        private static void SaveColor(string name, Color color)
        {
            PlayerPrefs.SetFloat(PrefKey + name + "R", color.r);
            PlayerPrefs.SetFloat(PrefKey + name + "G", color.g);
            PlayerPrefs.SetFloat(PrefKey + name + "B", color.b);
        }
    }
}
