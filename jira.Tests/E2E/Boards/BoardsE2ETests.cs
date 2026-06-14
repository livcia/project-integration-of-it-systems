using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Boards;

/// <summary>
/// E2E testy dla zarządzania tablicami kanban i statusami zadań
/// </summary>
public class BoardsE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public BoardsE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }

    [Fact]
    public async Task BoardColumns_Should_HaveTodoInProgressDoneColumns()
    {
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        var task1 = TestDataBuilder.CreateTask(id: 1, boardId: board.IdTablicy, creatorId: owner.IdUzytkownika, column: "Todo");
        var task2 = TestDataBuilder.CreateTask(id: 2, boardId: board.IdTablicy, creatorId: owner.IdUzytkownika, column: "InProgress");
        var task3 = TestDataBuilder.CreateTask(id: 3, boardId: board.IdTablicy, creatorId: owner.IdUzytkownika, column: "Done");
        db.Zadania.AddRange(task1, task2, task3);
        await db.SaveChangesAsync();

        var todoTasks = await db.Zadania.Where(t => t.KolumnaTablicy == "Todo").ToListAsync();
        var inProgressTasks = await db.Zadania.Where(t => t.KolumnaTablicy == "InProgress").ToListAsync();
        var doneTasks = await db.Zadania.Where(t => t.KolumnaTablicy == "Done").ToListAsync();

        Assert.Single(todoTasks);
        Assert.Single(inProgressTasks);
        Assert.Single(doneTasks);
    }

    [Fact]
    public async Task MoveTask_Should_ChangeTaskColumn()
    {
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: owner.IdUzytkownika, column: "Todo");
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toMove = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toMove);
        toMove.KolumnaTablicy = "InProgress";
        toMove.Status = "InProgress";
        await db.SaveChangesAsync();

        var moved = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("InProgress", moved.KolumnaTablicy);
    }

    [Fact]
    public async Task CompleteTask_Should_MoveTaskToDone()
    {
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: owner.IdUzytkownika, status: "InProgress");
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var toComplete = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toComplete);
        toComplete.Status = "Done";
        toComplete.KolumnaTablicy = "Done";
        toComplete.DataZakonczenia = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var completed = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("Done", completed.Status);
        Assert.NotNull(completed.DataZakonczenia);
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
    public async Task DeleteBoard_Should_RemoveFromDatabase()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Act
        var toDelete = await db.Tablice.FindAsync(board.IdTablicy);
        Assert.NotNull(toDelete);
        db.Tablice.Remove(toDelete);
        await db.SaveChangesAsync();

        // Assert
        var deleted = await db.Tablice.FindAsync(board.IdTablicy);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteTask_Should_RemoveFromDatabase()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: owner.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Act
        var toDelete = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(toDelete);
        db.Zadania.Remove(toDelete);
        await db.SaveChangesAsync();

        // Assert
        var deleted = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task ChangeTaskStatus_FromTodo_ToInProgress()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(owner);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        var task = TestDataBuilder.CreateTask(
            boardId: board.IdTablicy,
            creatorId: owner.IdUzytkownika,
            status: "Todo",
            column: "Todo"
        );
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Act - symulacja ChangeTaskStatus z komponentu Board
        var dbTask = await db.Zadania.FindAsync(task.IdZadania);
        Assert.NotNull(dbTask);
        dbTask.KolumnaTablicy = "In Progress";
        dbTask.Status = "In Progress";
        await db.SaveChangesAsync();

        // Assert
        var updated = await db.Zadania.FindAsync(task.IdZadania);
        Assert.Equal("In Progress", updated!.KolumnaTablicy);
        Assert.Equal("In Progress", updated.Status);
    }

    [Fact]
    public async Task SearchUsers_ByEmail_ReturnsMatchingUsers()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var user1 = TestDataBuilder.CreateUser(id: 1, email: "alice@example.com", username: "alice");
        var user2 = TestDataBuilder.CreateUser(id: 2, email: "bob@example.com", username: "bob");
        var user3 = TestDataBuilder.CreateUser(id: 3, email: "charlie@test.org", username: "charlie");
        db.Uzytkownicy.AddRange(user1, user2, user3);
        await db.SaveChangesAsync();

        // Act - symulacja wyszukiwania użytkowników po emailu
        // (SearchUsersAsync w Board.razor filtruje po NazwaUzytkownika i Email)
        var query = "example.com";
        var results = await db.Uzytkownicy
            .Where(u => u.Email.Contains(query) || u.NazwaUzytkownika.Contains(query))
            .OrderBy(u => u.NazwaUzytkownika)
            .ToListAsync();

        // Assert – alice i bob mają @example.com, charlie nie
        Assert.Equal(2, results.Count);
        Assert.Contains(results, u => u.NazwaUzytkownika == "alice");
        Assert.Contains(results, u => u.NazwaUzytkownika == "bob");
        Assert.DoesNotContain(results, u => u.NazwaUzytkownika == "charlie");
    }

    [Fact]
    public async Task AddUserToBoard_Should_CreateTablicaUzytkownikRecord()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var owner = TestDataBuilder.CreateUser(id: 1, username: "owner");
        var newMember = TestDataBuilder.CreateUser(id: 2, email: "member@example.com", username: "newmember");
        db.Uzytkownicy.AddRange(owner, newMember);
        var board = TestDataBuilder.CreateBoard(ownerId: owner.IdUzytkownika);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        // Act - symulacja AddUserToBoardAsync z komponentu Board
        var entry = new TablicaUzytkownik
        {
            IdTablicy = board.IdTablicy,
            IdUzytkownika = newMember.IdUzytkownika,
            Rola = "member",
            DataDolaczenia = DateTime.UtcNow,
        };
        db.TabliceUzytkownicy.Add(entry);
        await db.SaveChangesAsync();

        // Assert
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

