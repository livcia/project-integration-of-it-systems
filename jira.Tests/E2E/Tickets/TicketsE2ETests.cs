using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Tickets;

public class TicketsE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public TicketsE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }

    [Fact]
    public async Task CreateTicket_Should_CreateNewTask()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        var task = TestDataBuilder.CreateTask(
            boardId: board.IdTablicy,
            creatorId: creator.IdUzytkownika,
            title: "Implement new feature",
            description: "Add user profile page"
        );
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var createdTask = await db.Zadania
            .Include(t => t.TworcaZadania)
            .FirstAsync(t => t.TytulZadania == "Implement new feature");
        
        Assert.NotNull(createdTask);
        Assert.Equal("Add user profile page", createdTask.OpisZadania);
        Assert.Equal("Todo", createdTask.Status);
        Assert.Equal(creator.IdUzytkownika, createdTask.TworcaZadania.IdUzytkownika);
    }

    [Fact]
    public async Task UpdateTicketDetails_Should_ModifyTaskInfo()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(
            boardId: board.IdTablicy,
            creatorId: creator.IdUzytkownika,
            title: "Old Title",
            priority: "sredni"
        );
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toUpdate = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toUpdate);
        toUpdate.TytulZadania = "New Title";
        toUpdate.OpisZadania = "Updated description";
        toUpdate.Priorytet = "wysoki";
        await db.SaveChangesAsync();

        var updated = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("New Title", updated.TytulZadania);
        Assert.Equal("Updated description", updated.OpisZadania);
        Assert.Equal("wysoki", updated.Priorytet);
    }

    [Fact]
    public async Task DeleteTicket_Should_RemoveTask()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: creator.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toDelete = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toDelete);
        db.Zadania.Remove(toDelete);
        await db.SaveChangesAsync();

        var deleted = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task TicketStatuses_Should_TrackWorkflow()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: creator.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toUpdate = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("Todo", toUpdate.Status);

        toUpdate.Status = "InProgress";
        await db.SaveChangesAsync();
        
        var inProgress = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("InProgress", inProgress.Status);

        toUpdate.Status = "Done";
        toUpdate.DataZakonczenia = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var done = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("Done", done.Status);
        Assert.NotNull(done.DataZakonczenia);
    }

    [Fact]
    public async Task CreateTicket_Should_SavePriorityAndAssignee()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser(id: 1, username: "creator");
        var assignee = TestDataBuilder.CreateUser(id: 2, email: "assignee@example.com", username: "assignee");
        db.Uzytkownicy.AddRange(creator, assignee);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        int assigneeId = assignee.IdUzytkownika;
        var zadanie = new Zadanie
        {
            TytulZadania               = "Zadanie do wykonania",
            OpisZadania                = null,
            Priorytet                  = "wysoki",
            Status                     = "Todo",
            KolumnaTablicy             = "Todo",
            IdTablicy                  = board.IdTablicy,
            IdUzytkownikaTworcyZadania = creator.IdUzytkownika,
            IdUzytkownikaPrzypisanego  = assigneeId > 0 ? assigneeId : (int?)null,
            DataStworzenia             = DateTime.UtcNow,
        };
        db.Zadania.Add(zadanie);
        await db.SaveChangesAsync();

        var saved = await db.Zadania
            .Include(t => t.UzytkownikPrzypisany)
            .FirstAsync(t => t.TytulZadania == "Zadanie do wykonania");
        Assert.Equal("wysoki", saved.Priorytet);
        Assert.NotNull(saved.IdUzytkownikaPrzypisanego);
        Assert.Equal(assignee.IdUzytkownika, saved.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public async Task CreateTicket_WithoutAssignee_ShouldHaveNullAssigneeId()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        int rawAssigneeId = 0;
        var zadanie = new Zadanie
        {
            TytulZadania               = "Zadanie bez przypisania",
            Priorytet                  = "sredni",
            Status                     = "Todo",
            KolumnaTablicy             = "Todo",
            IdTablicy                  = board.IdTablicy,
            IdUzytkownikaTworcyZadania = creator.IdUzytkownika,
            IdUzytkownikaPrzypisanego  = rawAssigneeId > 0 ? rawAssigneeId : (int?)null,
            DataStworzenia             = DateTime.UtcNow,
        };
        db.Zadania.Add(zadanie);
        await db.SaveChangesAsync();

        var saved = await db.Zadania
            .FirstAsync(t => t.TytulZadania == "Zadanie bez przypisania");
        Assert.Null(saved.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public async Task CreateTicket_WithDescription_ShouldPersistDescription()
    {
        using var db = _fixture.CreateDbContext();
        var creator = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(creator);
        var board = TestDataBuilder.CreateBoard(ownerId: creator.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        const string description = "Szczegółowy opis zadania do wykonania przez twórcę.";
        var zadanie = new Zadanie
        {
            TytulZadania               = "Zadanie z opisem",
            OpisZadania                = description.Trim(),
            Priorytet                  = "niski",
            Status                     = "In Progress",
            KolumnaTablicy             = "In Progress",
            IdTablicy                  = board.IdTablicy,
            IdUzytkownikaTworcyZadania = creator.IdUzytkownika,
            IdUzytkownikaPrzypisanego  = null,
            DataStworzenia             = DateTime.UtcNow,
        };
        db.Zadania.Add(zadanie);
        await db.SaveChangesAsync();

        var saved = await db.Zadania
            .FirstAsync(t => t.TytulZadania == "Zadanie z opisem");
        Assert.Equal(description.Trim(), saved.OpisZadania);
        Assert.Equal("niski", saved.Priorytet);
        Assert.Equal("In Progress", saved.KolumnaTablicy);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
