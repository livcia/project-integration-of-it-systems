using jira.Data;
using jira.DbModels;
using jira.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.Data;

/// <summary>
/// Testy jednostkowe dla AppDbContext.
/// Używają bazy danych InMemory – nie wymagają połączenia z PostgreSQL.
/// </summary>
public class AppDbContextTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;
    private readonly AppDbContext _db;

    public AppDbContextTests()
    {
        _fixture = new TestDatabaseFixture();
        _db = _fixture.CreateDbContext();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ─── Tworzenie kontekstu ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithInMemoryOptions_DoesNotThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // Act & Assert – nie rzuca wyjątku
        using var ctx = new AppDbContext(options);
        Assert.NotNull(ctx);
    }

    // ─── DbSets ────────────────────────────────────────────────────────────────

    [Fact]
    public void DbSets_AreAccessible()
    {
        // Assert – wszystkie DbSety są dostępne (nie null)
        Assert.NotNull(_db.Komentarze);
        Assert.NotNull(_db.Tablice);
        Assert.NotNull(_db.TabliceUzytkownicy);
        Assert.NotNull(_db.Uzytkownicy);
        Assert.NotNull(_db.Zadania);
    }

    [Fact]
    public void DbSet_Uzytkownicy_IsQueryable()
    {
        // Act & Assert – query nie rzuca
        var count = _db.Uzytkownicy.Count();
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbSet_Tablice_IsQueryable()
    {
        var count = _db.Tablice.Count();
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbSet_Zadania_IsQueryable()
    {
        var count = _db.Zadania.Count();
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbSet_Komentarze_IsQueryable()
    {
        var count = _db.Komentarze.Count();
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbSet_TabliceUzytkownicy_IsQueryable()
    {
        var count = _db.TabliceUzytkownicy.Count();
        Assert.Equal(0, count);
    }

    // ─── OnModelCreating / relacje ─────────────────────────────────────────────

    [Fact]
    public async Task SaveChanges_AddUzytkownik_PersistsCorrectly()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 10, email: "rel@example.com", username: "reluser");

        // Act
        _db.Uzytkownicy.Add(user);
        await _db.SaveChangesAsync();

        // Assert
        var saved = await _db.Uzytkownicy.FindAsync(10);
        Assert.NotNull(saved);
        Assert.Equal("rel@example.com", saved!.Email);
    }

    [Fact]
    public async Task SaveChanges_AddTablica_WithOwner_PersistsRelation()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 20, email: "owner@example.com", username: "owneruser");
        var board = TestDataBuilder.CreateBoard(id: 20, ownerId: 20, name: "Moja tablica");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);

        // Act
        await _db.SaveChangesAsync();

        // Assert
        var savedBoard = await _db.Tablice.FindAsync(20);
        Assert.NotNull(savedBoard);
        Assert.Equal(20, savedBoard!.IdUzytkownikaOwner);
    }

    [Fact]
    public async Task SaveChanges_AddZadanie_WithCreator_PersistsRelation()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 30, email: "creator@example.com", username: "creator");
        var board = TestDataBuilder.CreateBoard(id: 30, ownerId: 30);
        var task = TestDataBuilder.CreateTask(id: 30, boardId: 30, creatorId: 30, title: "Zadanie A");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.Add(task);

        // Act
        await _db.SaveChangesAsync();

        // Assert
        var savedTask = await _db.Zadania.FindAsync(30);
        Assert.NotNull(savedTask);
        Assert.Equal(30, savedTask!.IdUzytkownikaTworcyZadania);
        Assert.Equal("Zadanie A", savedTask.TytulZadania);
    }

    [Fact]
    public async Task SaveChanges_AddKomentarz_PersistsCorrectly()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 40, email: "commenter@example.com", username: "commenter");
        var board = TestDataBuilder.CreateBoard(id: 40, ownerId: 40);
        var task = TestDataBuilder.CreateTask(id: 40, boardId: 40, creatorId: 40);
        var comment = TestDataBuilder.CreateComment(id: 40, taskId: 40, authorId: 40, content: "Treść komentarza");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.Add(task);
        _db.Komentarze.Add(comment);

        // Act
        await _db.SaveChangesAsync();

        // Assert
        var saved = await _db.Komentarze.FindAsync(40);
        Assert.NotNull(saved);
        Assert.Equal("Treść komentarza", saved!.TrescKomentarza);
    }

    [Fact]
    public async Task SaveChanges_AddTablicaUzytkownik_PersistsRelation()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 50, email: "member@example.com", username: "member");
        var board = TestDataBuilder.CreateBoard(id: 50, ownerId: 50);
        var access = TestDataBuilder.CreateBoardAccess(boardId: 50, userId: 50);

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.TabliceUzytkownicy.Add(access);

        // Act
        await _db.SaveChangesAsync();

        // Assert
        var savedAccess = await _db.TabliceUzytkownicy.FindAsync(50, 50);
        Assert.NotNull(savedAccess);
        Assert.Equal("member", savedAccess!.Rola);
    }

    [Fact]
    public async Task OnModelCreating_BoardOwnerRelation_IsConfiguredCorrectly()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 60, email: "cfg@example.com", username: "cfguser");
        var board1 = TestDataBuilder.CreateBoard(id: 61, ownerId: 60, name: "B1");
        var board2 = TestDataBuilder.CreateBoard(id: 62, ownerId: 60, name: "B2");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.AddRange(board1, board2);
        await _db.SaveChangesAsync();

        // Act – załaduj właściciela z nawigacją
        var boards = await _db.Tablice
            .Where(t => t.IdUzytkownikaOwner == 60)
            .ToListAsync();

        // Assert
        Assert.Equal(2, boards.Count);
    }

    [Fact]
    public async Task OnModelCreating_TaskCreatorRelation_IsConfiguredCorrectly()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 70, email: "tworca@example.com", username: "tworca");
        var board = TestDataBuilder.CreateBoard(id: 70, ownerId: 70);
        var task1 = TestDataBuilder.CreateTask(id: 71, boardId: 70, creatorId: 70, title: "T1");
        var task2 = TestDataBuilder.CreateTask(id: 72, boardId: 70, creatorId: 70, title: "T2");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.AddRange(task1, task2);
        await _db.SaveChangesAsync();

        // Act
        var tasks = await _db.Zadania
            .Where(z => z.IdUzytkownikaTworcyZadania == 70)
            .ToListAsync();

        // Assert
        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public async Task OnModelCreating_AssignedUserRelation_NullableIsAllowed()
    {
        // Arrange – zadanie bez przypisanego użytkownika
        var user = TestDataBuilder.CreateUser(id: 80, email: "noassign@example.com", username: "noassign");
        var board = TestDataBuilder.CreateBoard(id: 80, ownerId: 80);
        var task = TestDataBuilder.CreateTask(id: 80, boardId: 80, creatorId: 80, assignedToId: null);

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.Add(task);
        await _db.SaveChangesAsync();

        // Assert
        var saved = await _db.Zadania.FindAsync(80);
        Assert.NotNull(saved);
        Assert.Null(saved!.IdUzytkownikaPrzypisanego);
    }
}
