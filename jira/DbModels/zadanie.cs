using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jira.DbModels;

[Table("ZADANIA")]
public class Zadanie
{
    [Key]
    [Column("id_zadania")]
    public int IdZadania { get; set; }

    [Column("id_tablicy")]
    public int IdTablicy { get; set; }

    [Required]
    [Column("tytul_zadania")]
    public string TytulZadania { get; set; } = null!;

    [Column("opis_zadania")]
    public string? OpisZadania { get; set; }

    [Column("data_stworzenia", TypeName = "timestamp without time zone")]
    public DateTime DataStworzenia { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Unspecified);

    [Column("id_uzytkownika_przypisanego")]
    public int? IdUzytkownikaPrzypisanego { get; set; }

    [Column("id_uzytkownika_tworcy_zadania")]
    public int IdUzytkownikaTworcyZadania { get; set; }

    [Required]
    [Column("priorytet")]
    public string Priorytet { get; set; } = "sredni";

    [Required]
    [Column("status")]
    public string Status { get; set; } = "Todo";

    [Column("data_zakonczenia", TypeName = "timestamp without time zone")]
    public DateTime? DataZakonczenia { get; set; }

    [Required]
    [Column("kolumna_tablicy")]
    public string KolumnaTablicy { get; set; } = "Todo";

    [ForeignKey(nameof(IdTablicy))]
    public Tablica Tablica { get; set; } = null!;

    [ForeignKey(nameof(IdUzytkownikaPrzypisanego))]
    public Uzytkownik? UzytkownikPrzypisany { get; set; }

    [ForeignKey(nameof(IdUzytkownikaTworcyZadania))]
    public Uzytkownik TworcaZadania { get; set; } = null!;

    public ICollection<Komentarz> Komentarze { get; set; } = new List<Komentarz>();
}