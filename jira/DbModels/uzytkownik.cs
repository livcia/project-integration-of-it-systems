using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace jira.DbModels;

[Table("UZYTKOWNICY")]
[Index(nameof(Email), IsUnique = true)]
public class Uzytkownik
{
    [Key]
    [Column("id_uzytkownika")]
    public int IdUzytkownika { get; set; }

    [Required]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Required]
    [Column("nazwa_uzytkownika")]
    public string NazwaUzytkownika { get; set; } = null!;

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("github_id")]
    public string? GitHubId { get; set; }

    [Column("google_id")]
    public string? GoogleId { get; set; }

    [Column("github_refresh_token_encrypted")]
    public string? GitHubRefreshTokenEncrypted { get; set; }

    [Column("google_refresh_token_encrypted")]
    public string? GoogleRefreshTokenEncrypted { get; set; }

    [Column("data_rejestracji", TypeName = "timestamp without time zone")]
    public DateTime DataRejestracji { get; set; } = DateTime.UtcNow;

    [InverseProperty(nameof(Tablica.Owner))]
    public ICollection<Tablica> TabliceOwner { get; set; } = new List<Tablica>();

    public ICollection<TablicaUzytkownik> TabliceUzyt { get; set; } = new List<TablicaUzytkownik>();

    [InverseProperty(nameof(Zadanie.TworcaZadania))]
    public ICollection<Zadanie> ZadaniaStworzone { get; set; } = new List<Zadanie>();

    [InverseProperty(nameof(Zadanie.UzytkownikPrzypisany))]
    public ICollection<Zadanie> ZadaniaPrzypisane { get; set; } = new List<Zadanie>();

    public ICollection<Komentarz> Komentarze { get; set; } = new List<Komentarz>();
}