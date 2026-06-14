using jira.Data;
using jira.DbModels;
using jira.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.Data;

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

    [Fact]
    public void Constructor_WithInMemoryOptions_DoesNotThrow()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new AppDbContext(options);
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task SaveChanges_AddUzytkownik_PersistsCorrectly()
    {
        var user = TestDataBuilder.CreateUser(id: 10, email: "rel@example.com", username: "reluser");

        _db.Uzytkownicy.Add(user);
        await _db.SaveChangesAsync();

        var saved = await _db.Uzytkownicy.FindAsync(10);
        Assert.NotNull(saved);
        Assert.Equal("rel@example.com", saved!.Email);
    }

    [Fact]
    public async Task SaveChanges_AddTablica_WithOwner_PersistsRelation()
    {
        var user = TestDataBuilder.CreateUser(id: 20, email: "owner@example.com", username: "owneruser");
        var board = TestDataBuilder.CreateBoard(id: 20, ownerId: 20, name: "Moja tablica");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);

        await _db.SaveChangesAsync();

        var savedBoard = await _db.Tablice.FindAsync(20);
        Assert.NotNull(savedBoard);
        Assert.Equal(20, savedBoard!.IdUzytkownikaOwner);
    }

    [Fact]
    public async Task SaveChanges_AddZadanie_WithCreator_PersistsRelation()
    {
        var user = TestDataBuilder.CreateUser(id: 30, email: "creator@example.com", username: "creator");
        var board = TestDataBuilder.CreateBoard(id: 30, ownerId: 30);
        var task = TestDataBuilder.CreateTask(id: 30, boardId: 30, creatorId: 30, title: "Zadanie A");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.Add(task);

        await _db.SaveChangesAsync();

        var savedTask = await _db.Zadania.FindAsync(30);
        Assert.NotNull(savedTask);
        Assert.Equal(30, savedTask!.IdUzytkownikaTworcyZadania);
        Assert.Equal("Zadanie A", savedTask.TytulZadania);
    }

    [Fact]
    public async Task SaveChanges_AddKomentarz_PersistsCorrectly()
    {
        var user = TestDataBuilder.CreateUser(id: 40, email: "commenter@example.com", username: "commenter");
        var board = TestDataBuilder.CreateBoard(id: 40, ownerId: 40);
        var task = TestDataBuilder.CreateTask(id: 40, boardId: 40, creatorId: 40);
        var comment = TestDataBuilder.CreateComment(id: 40, taskId: 40, authorId: 40, content: "Treść komentarza");

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.Zadania.Add(task);
        _db.Komentarze.Add(comment);

        await _db.SaveChangesAsync();

        var saved = await _db.Komentarze.FindAsync(40);
        Assert.NotNull(saved);
        Assert.Equal("Treść komentarza", saved!.TrescKomentarza);
    }

    [Fact]
    public async Task SaveChanges_AddTablicaUzytkownik_PersistsRelation()
    {
        var user = TestDataBuilder.CreateUser(id: 50, email: "member@example.com", username: "member");
        var board = TestDataBuilder.CreateBoard(id: 50, ownerId: 50);
        var access = TestDataBuilder.CreateBoardAccess(boardId: 50, userId: 50);

        _db.Uzytkownicy.Add(user);
        _db.Tablice.Add(board);
        _db.TabliceUzytkownicy.Add(access);

        await _db.SaveChangesAsync();

        var savedAccess = await _db.TabliceUzytkownicy.FindAsync(50, 50);
        Assert.NotNull(savedAccess);
        Assert.Equal("member", savedAccess!.Rola);
    }
    
}
