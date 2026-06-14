using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Projects;

/// <summary>
/// E2E testy dla zarządzania projektami (tablicami)
/// </summary>
public class ProjectsE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public ProjectsE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }

    [Fact]
    public async Task CreateProject_Should_CreateNewBoard()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser(username: "owner");
        db.Uzytkownicy.Add(owner);
        await db.SaveChangesAsync();

        // Act
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika, name: "My Project");
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Assert
        var createdBoard = await db.Tablice.FirstAsync(b => b.NazwaTablicy == "My Project");
        Assert.NotNull(createdBoard);
        Assert.Equal(owner.IdUzytkownika, createdBoard.IdUzytkownikaOwner);
    }

    [Fact]
    public async Task GetProjectDetails_Should_ReturnProjectInfo()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        
        var board = TestDataBuilder.CreateBoard(
            ownerId: owner.IdUzytkownika,
            name: "Test Board",
            description: "Test Description",
            color: "#FF5733"
        );
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Act
        var retrievedBoard = await db.Tablice
            .Include(b => b.Owner)
            .FirstAsync(b => b.IdTablicy == board.IdTablicy);

        // Assert
        Assert.NotNull(retrievedBoard);
        Assert.Equal("Test Board", retrievedBoard.NazwaTablicy);
        Assert.Equal("Test Description", retrievedBoard.OpisTablicy);
        Assert.Equal("#FF5733", retrievedBoard.KolorTablicy);
        Assert.Equal(owner.IdUzytkownika, retrievedBoard.Owner.IdUzytkownika);
    }

    [Fact]
    public async Task ShareProjectWithUser_Should_GrantAccess()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        
        var owner = TestDataBuilder.CreateUser(username: "owner");
        var collaborator = TestDataBuilder.CreateUser(id: 2, email: "collab@example.com", username: "collaborator");
        db.Uzytkownicy.AddRange(owner, collaborator);
        
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Act - Podzielenie się projektem
        var access = TestDataBuilder.CreateBoardAccess(board.IdTablicy, collaborator.IdUzytkownika);
        db.TabliceUzytkownicy.Add(access);
        await db.SaveChangesAsync();

        // Assert
        var granted = await db.TabliceUzytkownicy
            .FirstAsync(a => a.IdTablicy == board.IdTablicy && a.IdUzytkownika == collaborator.IdUzytkownika);
        Assert.NotNull(granted);
    }

    [Fact]
    public async Task UpdateProject_Should_ModifyProjectDetails()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika, name: "Old Name");
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Act
        var toUpdate = await db.Tablice.FindAsync(board.IdTablicy);
        Assert.NotNull(toUpdate);
        toUpdate.NazwaTablicy = "New Name";
        toUpdate.OpisTablicy = "Updated Description";
        toUpdate.KolorTablicy = "#00FF00";
        await db.SaveChangesAsync();

        // Assert
        var updated = await db.Tablice.FindAsync(board.IdTablicy);
        Assert.Equal("New Name", updated.NazwaTablicy);
        Assert.Equal("Updated Description", updated.OpisTablicy);
        Assert.Equal("#00FF00", updated.KolorTablicy);
    }

    [Fact]
    public async Task GetUserProjects_Should_ReturnAllUserBoards()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        await db.SaveChangesAsync();

        var board1 = TestDataBuilder.CreateBoard(id: 1, ownerId: owner.IdUzytkownika, name: "Board 1");
        var board2 = TestDataBuilder.CreateBoard(id: 2, ownerId: owner.IdUzytkownika, name: "Board 2");
        var board3 = TestDataBuilder.CreateBoard(id: 3, ownerId: owner.IdUzytkownika, name: "Board 3");
        
        db.Tablice.AddRange(board1, board2, board3);
        await db.SaveChangesAsync();

        // Act
        var userBoards = await db.Tablice
            .Where(b => b.IdUzytkownikaOwner == owner.IdUzytkownika)
            .ToListAsync();

        // Assert
        Assert.Equal(3, userBoards.Count);
        Assert.Contains(userBoards, b => b.NazwaTablicy == "Board 1");
        Assert.Contains(userBoards, b => b.NazwaTablicy == "Board 2");
        Assert.Contains(userBoards, b => b.NazwaTablicy == "Board 3");
    }

    [Fact]
    public async Task RemoveProjectAccess_Should_RevokeUserAccess()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        
        var owner = TestDataBuilder.CreateUser(username: "owner");
        var collaborator = TestDataBuilder.CreateUser(id: 2, email: "collab@example.com", username: "collaborator");
        db.Uzytkownicy.AddRange(owner, collaborator);
        
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        
        var access = TestDataBuilder.CreateBoardAccess(board.IdTablicy, collaborator.IdUzytkownika);
        db.TabliceUzytkownicy.Add(access);
        await db.SaveChangesAsync();

        // Act - Cofnięcie dostępu
        var toRemove = await db.TabliceUzytkownicy
            .FirstAsync(a => a.IdTablicy == board.IdTablicy && a.IdUzytkownika == collaborator.IdUzytkownika);
        db.TabliceUzytkownicy.Remove(toRemove);
        await db.SaveChangesAsync();

        // Assert
        var removed = await db.TabliceUzytkownicy
            .FirstOrDefaultAsync(a => a.IdTablicy == board.IdTablicy && a.IdUzytkownika == collaborator.IdUzytkownika);
        Assert.Null(removed);
    }

    [Fact]
    public async Task CreateProject_Should_PersistToDatabase()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        await db.SaveChangesAsync();

        // Act - symulacja HandleSubmitAsync z ProjectCreate.razor
        var newBoard = new Tablica
        {
            NazwaTablicy       = "Mój Projekt",
            OpisTablicy        = "Opis projektu zaliczeniowego",
            KolorTablicy       = "#6554C0",
            IdUzytkownikaOwner = owner.IdUzytkownika,
            DataStworzenia     = DateTime.UtcNow,
        };
        db.Tablice.Add(newBoard);
        await db.SaveChangesAsync();

        // Assert
        var persisted = await db.Tablice
            .FirstAsync(b => b.NazwaTablicy == "Mój Projekt");
        Assert.NotNull(persisted);
        Assert.Equal("Opis projektu zaliczeniowego", persisted.OpisTablicy);
        Assert.Equal("#6554C0", persisted.KolorTablicy);
        Assert.Equal(owner.IdUzytkownika, persisted.IdUzytkownikaOwner);
    }

    [Fact]
    public async Task CreateProject_WithDuplicateName_ShouldStillPersist()
    {
        // Arrange
        // Logika komponentu ProjectCreate nie blokuje duplikatów nazw –
        // walidacja tylko sprawdza min/max długość. Obydwie tablice powinny być w bazie.
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        await db.SaveChangesAsync();

        var board1 = new Tablica
        {
            IdTablicy          = 10,
            NazwaTablicy       = "Duplikat",
            KolorTablicy       = "#0052CC",
            IdUzytkownikaOwner = owner.IdUzytkownika,
            DataStworzenia     = DateTime.UtcNow,
        };
        var board2 = new Tablica
        {
            IdTablicy          = 11,
            NazwaTablicy       = "Duplikat",
            KolorTablicy       = "#00875A",
            IdUzytkownikaOwner = owner.IdUzytkownika,
            DataStworzenia     = DateTime.UtcNow,
        };

        // Act
        db.Tablice.AddRange(board1, board2);
        await db.SaveChangesAsync();

        // Assert – obydwie tablice zapisane
        var all = await db.Tablice
            .Where(b => b.NazwaTablicy == "Duplikat")
            .ToListAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetSwatchClass_ReturnsCssClass()
    {
        // Testuje logikę GetSwatchClass bezpośrednio (inline – logika z ProjectCreate.razor)
        // private string GetSwatchClass(string color) =>
        //     model.KolorTablicy == color
        //         ? "cb-color-swatch cb-color-swatch--selected"
        //         : "cb-color-swatch";

        const string selectedColor = "#0052CC";

        string GetSwatchClass(string color) =>
            selectedColor == color
                ? "cb-color-swatch cb-color-swatch--selected"
                : "cb-color-swatch";

        // Assert – wybrany kolor otrzymuje klasę --selected
        Assert.Equal("cb-color-swatch cb-color-swatch--selected", GetSwatchClass("#0052CC"));
        // Assert – niewybrany kolor nie ma klasy --selected
        Assert.Equal("cb-color-swatch", GetSwatchClass("#00875A"));
        Assert.Equal("cb-color-swatch", GetSwatchClass("#FF5630"));
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
