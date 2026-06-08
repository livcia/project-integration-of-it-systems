using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jira.DbModels;

[Table("TABLICE")]
public class Tablica
{
    [Key]
    [Column("id_tablicy")]
    public int IdTablicy { get; set; }

    [Required]
    [Column("nazwa_tablicy")]
    public string NazwaTablicy { get; set; } = null!;

    [Column("opis_tablicy")]
    public string? OpisTablicy { get; set; }

    [Column("id_uzytkownika_owner")]
    public int IdUzytkownikaOwner { get; set; }

    [Column("data_stworzenia", TypeName = "timestamp without time zone")]
    public DateTime DataStworzenia { get; set; } = DateTime.UtcNow;

    [Column("kolor_tablicy")]
    public string? KolorTablicy { get; set; }

    [ForeignKey(nameof(IdUzytkownikaOwner))]
    public Uzytkownik Owner { get; set; } = null!;

    public ICollection<TablicaUzytkownik> TabliceUzyt { get; set; } = new List<TablicaUzytkownik>();
    public ICollection<Zadanie> Zadania { get; set; } = new List<Zadanie>();
}