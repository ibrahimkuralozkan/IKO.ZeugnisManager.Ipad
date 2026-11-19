namespace ZeugnisManager.Admin.Models
{
    /// <summary> Katalogdaki tek bir degerlendirme kriteri. </summary>
    public class KatalogItem
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// Bir degerlendirme alani (bereich): katalog anahtari + Word template'indeki w_ tag oneki.
    /// Cogunlukla ayni; Deutsch/Mathe alt dallarinda template kisaltma kullaniyor.
    /// </summary>
    public class RatingBereich
    {
        public string CatalogKey { get; set; } = "";
        public string TemplateTag { get; set; } = "";
        public string? SubTitle { get; set; }
        public List<string> ItemIds { get; set; } = new();
        public string? NoteAwKey { get; set; }   // bu alt-derse bagli Gesamtnote (yoksa TemplateTag ile eslesir)

        public RatingBereich() { }
        public RatingBereich(string catalogKey, string templateTag, string? subTitle = null,
            IEnumerable<string>? itemIds = null, string? noteAwKey = null)
        {
            CatalogKey = catalogKey;
            TemplateTag = templateTag;
            SubTitle = subTitle;
            ItemIds = itemIds?.ToList() ?? new();
            NoteAwKey = noteAwKey;
        }
    }

    /// <summary> Bir Gesamtnote alani (K4): 1..6 not -> aw_{AwKey}_Note. </summary>
    public class NoteField
    {
        public string Label { get; set; } = "";
        public string AwKey { get; set; } = "";  // aw_{AwKey}_Note
        public bool Indent { get; set; }          // alt-not (örn. Deutsch alti Lesen) -> girintili

        public NoteField() { }
        public NoteField(string label, string awKey, bool indent = false)
        {
            Label = label; AwKey = awKey; Indent = indent;
        }
    }

    /// <summary>
    /// Bir konu grubu (ekranda baslik): Bemerkung metni (txt_) + 0..n rating alani + 0..n Gesamtnote.
    /// </summary>
    public class SubjectGroup
    {
        public string Title { get; set; } = "";
        public string? TextTag { get; set; }   // txt_{TextTag}; null ise metin kutusu yok
        public List<RatingBereich> Bereiche { get; set; } = new();
        public List<NoteField> Notes { get; set; } = new();
        public bool NotesGrid { get; set; }     // K4: notlari 2 kolonlu "Lernbereiche/Fächer" grid'i olarak goster
    }
}
