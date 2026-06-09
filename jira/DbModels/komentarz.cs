using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jira.DbModels;

[Table("KOMENTARZE")]
public class Komentarz
{
    [Key]
    [Column("id_komentarza")]
    public int IdKomentarza { get; set; }

    [Column("id_zadania")]
    public int IdZadania { get; set; }

    [Required]
    [Column("tresc_komentarza")]
    public string TrescKomentarza { get; set; } = null!;

    [Column("id_uzytkownika")]
    public int IdUzytkownika { get; set; }

    [Column("data_utworzenia", TypeName = "timestamp without time zone")]
    public DateTime DataUtworzenia { get; set; } = DateTime.UtcNow;

    [Column("data_edycji", TypeName = "timestamp without time zone")]
    public DateTime? DataEdycji { get; set; }

    [ForeignKey(nameof(IdZadania))]
    public Zadanie Zadanie { get; set; } = null!;

    [ForeignKey(nameof(IdUzytkownika))]
    public Uzytkownik Uzytkownik { get; set; } = null!;
}