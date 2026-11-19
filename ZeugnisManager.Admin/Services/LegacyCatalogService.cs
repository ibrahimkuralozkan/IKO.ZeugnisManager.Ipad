using System.Text.Json;
using ZeugnisManager.Admin.Models;

namespace ZeugnisManager.Admin.Services
{
    /// <summary>
    /// Degerlendirme kataloglarini (bereich -> kriter listesi) AppData/Kataloge'dan okur
    /// ve sinif duzeyine gore ekran yerlesimini (SubjectGroup) saglar.
    /// Suanki POC: Klasse1 (2hj).
    /// </summary>
    public class LegacyCatalogService
    {
        private readonly string _dir;
        private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

        public LegacyCatalogService(LegacyDataSourceService dataSource)
        {
            _dir = Path.Combine(dataSource.GetConfiguredPath(), "AppData", "Kataloge");
        }

        /// <summary> className ("1a") -> sinif duzeyi (1). </summary>
        public static int Klassenstufe(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return 0;
            return char.IsDigit(className[0]) ? (className[0] - '0') : 0;
        }

        /// <summary> bereich -> kriter listesi. isSE: SE katalogunu okur. </summary>
        public Dictionary<string, List<KatalogItem>> Load(int klassenstufe, bool isSE = false, int halbjahr = 2)
        {
            try
            {
                var prefix = isSE ? "SEKlasse" : "Klasse";
                var path = Path.Combine(_dir, $"{prefix}{klassenstufe}_{halbjahr}hj.json");
                if (!File.Exists(path)) return new();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<string, List<KatalogItem>>>(json, Opts) ?? new();
            }
            catch
            {
                return new();
            }
        }

        /// <summary> Sinif duzeyi + SE'ye gore ekran yerlesimi. Desteklenmiyorsa bos liste. </summary>
        public static List<SubjectGroup> GetLayout(int klassenstufe, bool isSE = false, int halbjahr = 2)
        {
            if (klassenstufe == 4)
            {
                var klasse4Layout = isSE ? Klasse4SELayout() : Klasse4Layout();
                ApplyHalfYearLayout(klasse4Layout, klassenstufe, halbjahr);
                return klasse4Layout;
            }

            var layout = klassenstufe switch
            {
                1 => Klasse1Layout(),
                2 => Klasse1Layout(), // K2 yapisi K1 ile ayni
                3 => Klasse3Layout(),
                _ => new List<SubjectGroup>()
            };
            if (isSE && layout.Count > 0)
                ApplySEDeutsch(layout, klassenstufe, halbjahr);
            ApplyHalfYearLayout(layout, klassenstufe, halbjahr);
            return layout;
        }

        private static void ApplyHalfYearLayout(List<SubjectGroup> layout, int klassenstufe, int halbjahr)
        {
            if (halbjahr != 1) return;
            var arbeitsverhalten = layout.FirstOrDefault(g => g.Title == "Arbeitsverhalten");
            if (arbeitsverhalten is null || arbeitsverhalten.Bereiche.Count < 2) return;
            if (klassenstufe == 4)
            {
                arbeitsverhalten.Bereiche[0].ItemIds = new() { "01", "02", "03", "04", "05", "06" };
                arbeitsverhalten.Bereiche[1].ItemIds = new() { "07", "08", "09", "10" };
            }
            else
            {
                arbeitsverhalten.Bereiche[0].ItemIds = new() { "01", "02", "03", "04", "05" };
                arbeitsverhalten.Bereiche[1].ItemIds = new() { "06", "07", "08" };
            }
        }

        /// <summary>
        /// SE'de Deutsch grubunun alt-dallari SE_* katalog alanlariyla degisir.
        /// Template tag'i doneme/sablona gore farkli olabilir.
        /// </summary>
        private static void ApplySEDeutsch(List<SubjectGroup> layout, int klassenstufe, int halbjahr)
        {
            var deutsch = layout.FirstOrDefault(g => g.Title == "Deutsch");
            if (deutsch == null) return;

            // SE Klasse 3 / 1. Halbjahr sablonu, gorunen SE bolumleri icin
            // eski normal placeholder oneklerini kullaniyor:
            //   Hoeren/Sprechen -> Rechtschreiben
            //   Rechtschreiben/Schreiben -> Lesen
            //   Lesen -> Sprachgebrauch
            // Katalog anahtarlari SE_* kalir; boylece UI dogru JSON bolumlerini okur.
            var firstHalfYear = klassenstufe == 3 && halbjahr == 1;
            deutsch.Bereiche = new List<RatingBereich>
            {
                new("SE_HoerenSprechen", firstHalfYear ? "Rechtschreiben" : "SE_HoerenSprechen",
                    "Hören / Sprechen", noteAwKey: firstHalfYear ? "Rechtschreiben" : null),
                new("SE_RechtschreibenSchreiben", firstHalfYear ? "Lesen" : "SE_RechtschreibenSchreiben",
                    "Rechtschreiben / Schreiben", noteAwKey: firstHalfYear ? "Lesen" : null),
                new("SE_Lesen", firstHalfYear ? "Sprachgebrauch" : "SE_Lesen",
                    "Lesen – mit Texten und Medien umgehen", noteAwKey: firstHalfYear ? "Sprachgebrauch" : null),
            };
            // SE Deutsch Gesamtnoten (varsa) -> SE_* alanlari + Deutsch genel notu
            if (deutsch.Notes.Count > 0)
            {
                deutsch.Notes = firstHalfYear
                    ? new List<NoteField>
                    {
                        new("Deutsch - Gesamtnote", "Deutsch"),
                        new("Hören und Sprechen - Note", "Rechtschreiben"),
                        new("Rechtschreiben / Schreiben - Note", "Lesen"),
                        new("Lesen - Note", "Sprachgebrauch"),
                    }
                    : new List<NoteField>
                {
                    new("Deutsch - Gesamtnote", "Deutsch"),
                    new("Hören und Sprechen - Note", "SE_HoerenSprechen"),
                    new("Rechtschreiben / Schreiben - Note", "SE_RechtschreibenSchreiben"),
                    new("Lesen - Note", "SE_Lesen"),
                };
            }
        }

        /// <summary> Sinif duzeyi + SE'ye gore Word sablon dosya adi. Yoksa null. </summary>
        public static string? GetTemplateFile(int klassenstufe, bool isSE = false, int halbjahr = 2)
        {
            if (klassenstufe < 1 || klassenstufe > 4 || halbjahr is not (1 or 2)) return null;
            var prefix = isSE ? "SEKlasse" : "Klasse";
            return $"{prefix}{klassenstufe}_{halbjahr}hj.docx";
        }

        /// <summary>
        /// Klasse1 ekran yerlesimi: gruplar, alt-dallar ve template tag eslemeleri.
        /// (Deutsch/Mathe alt dallarinda katalog uzun ad, template kisa ad kullanir.)
        /// </summary>
        public static List<SubjectGroup> Klasse1Layout() => new()
        {
            new SubjectGroup { Title = "Arbeitsverhalten", TextTag = "Arbeitsverhalten",
                Bereiche = {
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Leistungsbereitschaft", new[] { "01", "02", "03", "04", "05", "06" }),
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Zuverlässigkeit/Sorgfalt", new[] { "07", "08", "09", "10" })
                } },
            new SubjectGroup { Title = "Sozialverhalten", TextTag = "Sozialverhalten",
                Bereiche = { new("Sozialverhalten", "Sozialverhalten") } },
            new SubjectGroup { Title = "Deutsch", TextTag = "Deutsch", Bereiche = {
                new("Rechtschreiben", "Rechtschreiben", "Rechtschreiben"),
                new("LesenMitTextenUndMedienUmgehen", "Lesen", "Lesen / mit Texten und Medien umgehen"),
                new("SprachgebrauchSprechenUndZuhoerenSchreiben", "Sprachgebrauch", "Sprachgebrauch / Sprechen und Zuhören / Schreiben")
            } },
            new SubjectGroup { Title = "Mathematik", TextTag = "Mathematik", Bereiche = {
                new("Arithmetik", "Arithmetik", "Arithmetik"),
                new("Geometrie", "Geometrie", "Geometrie"),
                new("SachrechnenDatenUndWahrscheinlichkeit", "Sachrechnen", "Sachrechnen / Daten und Wahrscheinlichkeit")
            } },
            new SubjectGroup { Title = "Sachunterricht", TextTag = "Sachunterricht",
                Bereiche = { new("Sachunterricht", "Sachunterricht") } },
            new SubjectGroup { Title = "Sport", TextTag = "Sport",
                Bereiche = { new("Sport", "Sport") } },
            new SubjectGroup { Title = "Kunst", TextTag = "Kunst",
                Bereiche = { new("Kunst", "Kunst") } },
            new SubjectGroup { Title = "Musik", TextTag = "Musik",
                Bereiche = { new("Musik", "Musik") } },
            new SubjectGroup { Title = "Religion", TextTag = "Religion",
                Bereiche = { new("Religion", "Religion") } },
            // K1/K2 Berichtszeugnis: Gesamtnoten YOK (notlar yalniz K3/K4).
            new SubjectGroup { Title = "Allgemeine Bemerkungen", TextTag = "Allgemein",
                Bereiche = { } },
        };

        /// <summary>
        /// Klasse3 yerlesimi: katalog anahtarlari template tag'leriyle birebir ayni
        /// (kisa adlar). Englisch ayri bir derstir (txt_Englisch).
        /// </summary>
        public static List<SubjectGroup> Klasse3Layout() => new()
        {
            new SubjectGroup { Title = "Arbeitsverhalten", TextTag = "Arbeitsverhalten",
                Bereiche = {
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Leistungsbereitschaft", new[] { "01", "02", "03", "04", "05", "06" }),
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Zuverlässigkeit/Sorgfalt", new[] { "07", "08", "09", "10" })
                } },
            new SubjectGroup { Title = "Sozialverhalten", TextTag = "Sozialverhalten",
                Bereiche = { new("Sozialverhalten", "Sozialverhalten") } },
            // K3: her dersin Gesamtnote'u kendi bolumunde (rating tablosunun altinda)
            new SubjectGroup { Title = "Deutsch", TextTag = "Deutsch",
                Bereiche = {
                    new("Rechtschreiben", "Rechtschreiben", "Rechtschreiben"),
                    new("Lesen", "Lesen", "Lesen"),
                    new("Sprachgebrauch", "Sprachgebrauch", "Sprachgebrauch")
                },
                Notes = {
                    new("Deutsch - Gesamtnote", "Deutsch"),
                    new("Rechtschreiben - Note", "Rechtschreiben"),
                    new("Lesen - Note", "Lesen"),
                    new("Sprachgebrauch - Note", "Sprachgebrauch"),
                } },
            new SubjectGroup { Title = "Mathematik", TextTag = "Mathematik",
                Bereiche = {
                    new("Arithmetik", "Arithmetik", "Arithmetik"),
                    new("Geometrie", "Geometrie", "Geometrie"),
                    new("Sachrechnen", "Sachrechnen", "Sachrechnen")
                },
                Notes = { new("Mathematik - Gesamtnote", "Mathematik") } },
            new SubjectGroup { Title = "Englisch", TextTag = "Englisch",
                Bereiche = { new("Englisch", "Englisch") },
                Notes = { new("Englisch - Gesamtnote", "Englisch") } },
            new SubjectGroup { Title = "Sachunterricht", TextTag = "Sachunterricht",
                Bereiche = { new("Sachunterricht", "Sachunterricht") },
                Notes = { new("Sachunterricht - Gesamtnote", "Sachunterricht") } },
            new SubjectGroup { Title = "Sport", TextTag = "Sport",
                Bereiche = { new("Sport", "Sport") },
                Notes = { new("Sport - Gesamtnote", "Sport") } },
            new SubjectGroup { Title = "Kunst", TextTag = "Kunst",
                Bereiche = { new("Kunst", "Kunst") },
                Notes = { new("Kunst - Gesamtnote", "Kunst") } },
            new SubjectGroup { Title = "Musik", TextTag = "Musik",
                Bereiche = { new("Musik", "Musik") },
                Notes = { new("Musik - Gesamtnote", "Musik") } },
            new SubjectGroup { Title = "Religion", TextTag = "Religion",
                Bereiche = { new("Religion", "Religion") },
                Notes = { new("Religion - Gesamtnote", "Religion") } },
            new SubjectGroup { Title = "Allgemeine Bemerkungen", TextTag = "Allgemein",
                Bereiche = { } },
        };

        /// <summary>
        /// Klasse4: davranis (rating) + dersler Gesamtnote (1-6 combobox) + metinler.
        /// Deutsch'un genel notu + alt notlari (Rechtschreiben/Lesen/Sprachgebrauch) vardir.
        /// </summary>
        public static List<SubjectGroup> Klasse4Layout() => new()
        {
            new SubjectGroup { Title = "Arbeitsverhalten",
                Bereiche = {
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Leistungsbereitschaft", new[] { "01", "02", "03", "04", "05", "06" }),
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Zuverlässigkeit/Sorgfalt", new[] { "07", "08", "09", "10" })
                } },
            new SubjectGroup { Title = "Sozialverhalten", TextTag = "Sozialverhalten",
                Bereiche = { new("Sozialverhalten", "Sozialverhalten") } },
            // K4: dersler 2 kolonlu "Lernbereiche / Fächer" grid'i (Word sirasi: once sol kolon, sonra sag)
            new SubjectGroup { Title = "Lernbereiche / Fächer", NotesGrid = true, Notes = {
                new("Religion", "Religion"),
                new("Deutsch", "Deutsch"),
                new("Sprachgebrauch", "Sprachgebrauch", indent: true),
                new("Lesen", "Lesen", indent: true),
                new("Rechtschreiben", "Rechtschreiben", indent: true),
                new("Englisch", "Englisch"),
                new("Sachunterricht", "Sachunterricht"),
                new("Mathematik", "Mathematik"),
                new("Sport", "Sport"),
                new("Musik", "Musik"),
                new("Kunst", "Kunst"),
            } },
            new SubjectGroup { Title = "Allgemeine Bemerkungen", TextTag = "Allgemein",
                Bereiche = { } },
        };

        /// <summary>
        /// SE Klasse4: Tum Gesamtnoten "Lernbereiche / Fächer" panelinde;
        /// ayrintili Deutsch bolumunde yalnizca SE rating'leri ve Bemerkung bulunur.
        /// </summary>
        public static List<SubjectGroup> Klasse4SELayout() => new()
        {
            new SubjectGroup { Title = "Arbeitsverhalten",
                Bereiche = {
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Leistungsbereitschaft", new[] { "01", "02", "03", "04", "05", "06" }),
                    new("Arbeitsverhalten", "Arbeitsverhalten", "Zuverlässigkeit/Sorgfalt", new[] { "07", "08", "09", "10" })
                } },
            new SubjectGroup { Title = "Sozialverhalten", TextTag = "Sozialverhalten",
                Bereiche = { new("Sozialverhalten", "Sozialverhalten") } },
            new SubjectGroup { Title = "Deutsch", TextTag = "Deutsch",
                Bereiche = {
                    new("SE_HoerenSprechen", "SE_HoerenSprechen", "Hören / Sprechen", noteAwKey: "Sprachgebrauch"),
                    new("SE_RechtschreibenSchreiben", "SE_RechtschreibenSchreiben", "Rechtschreiben / Schreiben", noteAwKey: "Rechtschreiben"),
                    new("SE_Lesen", "SE_Lesen", "Lesen – mit Texten und Medien umgehen", noteAwKey: "Lesen"),
                } },
            new SubjectGroup { Title = "Lernbereiche / Fächer", NotesGrid = true, Notes = {
                new("Religion", "Religion"),
                new("Deutsch", "Deutsch"),
                new("Sprachgebrauch", "Sprachgebrauch", indent: true),
                new("Lesen", "Lesen", indent: true),
                new("Rechtschreiben", "Rechtschreiben", indent: true),
                new("Englisch", "Englisch"),
                new("Sachunterricht", "Sachunterricht"),
                new("Mathematik", "Mathematik"),
                new("Sport", "Sport"),
                new("Musik", "Musik"),
                new("Kunst", "Kunst"),
            } },
            new SubjectGroup { Title = "Allgemeine Bemerkungen", TextTag = "Allgemein",
                Bereiche = { } },
        };
    }
}
