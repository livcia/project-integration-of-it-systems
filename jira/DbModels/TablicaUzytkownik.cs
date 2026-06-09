using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace jira.DbModels;

[Table("TABLICE_UZYTKOWNICY")]
[PrimaryKey(nameof(IdUzytkownika), nameof(IdTablicy))]
public class TablicaUzytkownik
{
    [Column("id_uzytkownika")]
    public int IdUzytkownika { get; set; }

    [Column("id_tablicy")]
    public int IdTablicy { get; set; }

    [Required]
    [Column("rola")]
    public string Rola { get; set; } = "member";

    [Column("data_dolaczenia", TypeName = "timestamp without time zone")]
    public DateTime DataDolaczenia { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(IdUzytkownika))]
    public Uzytkownik Uzytkownik { get; set; } = null!;

    [ForeignKey(nameof(IdTablicy))]
    public Tablica Tablica { get; set; } = null!;
}