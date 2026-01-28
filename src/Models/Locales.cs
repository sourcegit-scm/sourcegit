using System.Collections.Generic;

namespace SourceGit.Models
{
    public class Locale
    {
        public string Name { get; set; }
        public string Key { get; set; }

        public static readonly List<Locale> Supported = new List<Locale>() {
            new Locale("Deutsch", "de_DE"),
            new Locale("English", "en_US"),
            new Locale("Español", "es_ES"),
            new Locale("Français", "fr_FR"),
            new Locale("Bahasa Indonesia", "id_ID"),
            new Locale("Italiano", "it_IT"),
            new Locale("Português (Brasil)", "pt_BR"),
            new Locale("Українська", "uk_UA"),
            new Locale("Русский", "ru_RU"),
            new Locale("简体中文", "zh_CN"),
            new Locale("繁體中文", "zh_TW"),
            new Locale("日本語", "ja_JP"),
            new Locale("தமிழ் (Tamil)", "ta_IN"),
            new Locale("한국어", "ko_KR"),
        };

        public Locale(string name, string key)
        {
            Name = name;
            Key = key;
        }
    }
}
