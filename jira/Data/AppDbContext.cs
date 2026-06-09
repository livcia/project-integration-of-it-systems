using Microsoft.EntityFrameworkCore;
using jira.DbModels;

namespace jira.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Uzytkownik> Uzytkownicy => Set<Uzytkownik>();
    public DbSet<Tablica> Tablice => Set<Tablica>();
    public DbSet<TablicaUzytkownik> TabliceUzytkownicy => Set<TablicaUzytkownik>();
    public DbSet<Zadanie> Zadania => Set<Zadanie>();
    public DbSet<Komentarz> Komentarze => Set<Komentarz>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tablica>()
            .HasOne(d => d.Owner)
            .WithMany(p => p.TabliceOwner)
            .HasForeignKey(d => d.IdUzytkownikaOwner)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Zadanie>()
            .HasOne(d => d.TworcaZadania)
            .WithMany(p => p.ZadaniaStworzone)
            .HasForeignKey(d => d.IdUzytkownikaTworcyZadania)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Zadanie>()
            .HasOne(d=>d.UzytkownikPrzypisany)
            .WithMany(p=>p.ZadaniaPrzypisane)
            .HasForeignKey(d=>d.IdUzytkownikaPrzypisanego)
            .OnDelete(DeleteBehavior.SetNull);
    }
}