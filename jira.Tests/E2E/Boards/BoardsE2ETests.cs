using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Boards;

public class BoardsE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public BoardsE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }

    [Fact]
    public async Task AssignTaskToUser_Should_LinkUserToTask()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser(id: 1, username: "creator");
        var assignee = TestDataBuilder.CreateUser(id: 2, email: "assignee@example.com", username: "assignee");
        db.Uzytkownicy.AddRange(creator, assignee);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: creator.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toAssign = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toAssign);
        toAssign.IdUzytkownikaPrzypisanego = assignee.IdUzytkownika;
        await db.SaveChangesAsync();

        var assigned = await db.Zadania
            .Include(t => t.UzytkownikPrzypisany)
            .FirstAsync(t => t.IdZadania == task.IdZadania);
        Assert.NotNull(assigned.UzytkownikPrzypisany);
        Assert.Equal("assignee", assigned.UzytkownikPrzypisany.NazwaUzytkownika);
    }

    [Fact]
    public async Task AddUserToBoard_Should_CreateTablicaUzytkownikRecord()
    {
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser(id: 1, username: "owner");
        var newMember = TestDataBuilder.CreateUser(id: 2, email: "member@example.com", username: "newmember");
        db.Uzytkownicy.AddRange(owner, newMember);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        var entry = new TablicaUzytkownik
        {
            IdTablicy = board.IdTablicy,
            IdUzytkownika = newMember.IdUzytkownika,
            Rola = "member",
            DataDolaczenia = DateTime.UtcNow,
        };
        db.TabliceUzytkownicy.Add(entry);
        await db.SaveChangesAsync();

        var persisted = await db.TabliceUzytkownicy
            .FirstOrDefaultAsync(tu =>
                tu.IdTablicy == board.IdTablicy &&
                tu.IdUzytkownika == newMember.IdUzytkownika);
        Assert.NotNull(persisted);
        Assert.Equal("member", persisted.Rola);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}

