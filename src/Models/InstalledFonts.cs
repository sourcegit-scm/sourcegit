using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Markup;
using System.Windows.Media;

namespace SourceGit.Models {
    public class InstalledFont {
        public string Name { get; set; }
        public int FamilyIndex { get; set; }

        public static List<InstalledFont> GetFonts {
            get {
                var fontList = new List<InstalledFont>();

                var fontCollection = Fonts.SystemFontFamilies;
                var familyCount = fontCollection.Count;

                for (int i = 0; i < familyCount; i++) {
                    var fontFamily = fontCollection.ElementAt(i);
                    var familyNames = fontFamily.FamilyNames;

                    if (!familyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out var name)) {
                        if (!familyNames.TryGetValue(XmlLanguage.GetLanguage("en-us"), out name)) {
                            name = familyNames.FirstOrDefault().Value;
                        }
                    }

                    fontList.Add(new InstalledFont() {
                        Name = name,
                        FamilyIndex = i
                    });
                }

                fontList.Sort((p, n) => string.Compare(p.Name, n.Name, StringComparison.Ordinal));

                return fontList;
            }
        }
    }
}
