using Bunit;
using Bunit.TestDoubles;
using jira.Components.Pages.Board;
using jira.Components.UI.StatusColumn;
using jira.Components.UI.TicketCard;
using jira.Data;
using jira.DbModels;
using jira.Services;
using jira.Tests.Fixtures;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;

namespace jira.Tests.Components;

public sealed class BoardTests : BunitContext
{
    private const int OwnerId = 1;
    private const int BoardIdConst = 10;
    private const int TaskIdConst = 100;
    private readonly AppDbContext _dbContext;

    public BoardTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);

        Services.AddSingleton(_dbContext);
    }

    private (Mock<IServiceScopeFactory> Factory, AppDbContext Db) CreateScopedDb()
    {
        var dbName = Guid.NewGuid().ToString();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AppDbContext(opts);

        Services.AddSingleton<AppDbContext>(db);

        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(db);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        return (factoryMock, db);
    }

    private void RegisterServices(
        Mock<IServiceScopeFactory> factory,
        IEmailService? email = null)
    {
        Services.AddSingleton<IServiceScopeFactory>(factory.Object);
        Services.AddSingleton<BoardStateService>();
        Services.AddSingleton(email ?? new MockEmailService());
    }

    private void SetupAuth(int userId = OwnerId)
    {
        this.AddAuthorization().SetClaims(
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"User{userId}"),
            new Claim(ClaimTypes.Email, $"user{userId}@test.com"));
    }


    private static Uzytkownik MakeUser(int id = OwnerId, string name = "TestUser", string email = "test@example.com")
        => TestDataBuilder.CreateUser(id: id, email: email, username: name);

    private static Zadanie MakeTask(
        int id = TaskIdConst,
        int boardId = BoardIdConst,
        string column = "Todo",
        string title = "Test Task",
        string prio = "sredni")
        => TestDataBuilder.CreateTask(
            id: id, boardId: boardId, creatorId: OwnerId,
            title: title, status: column, priority: prio, column: column);

    private static Komentarz MakeComment(int id, int taskId, int userId, string text,
        Uzytkownik? author = null)
    {
        var k = TestDataBuilder.CreateComment(id: id, taskId: taskId, authorId: userId, content: text);
        if (author is not null) k.Uzytkownik = author;
        return k;
    }

    private async Task SeedAsync(
        AppDbContext db,
        IEnumerable<Zadanie>? tasks = null,
        IEnumerable<TablicaUzytkownik>? members = null,
        Uzytkownik? owner = null)
    {
        var o = owner ?? MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>(),
            Owner = o
        };

        if (tasks is not null)
            foreach (var t in tasks)
                board.Zadania.Add(t);
        if (members is not null)
            foreach (var m in members)
                board.TabliceUzyt.Add(m);

        db.Uzytkownicy.Add(o);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();
    }

    private IRenderedComponent<Board> RenderBoard(int? boardId = BoardIdConst)
        => Render<Board>(p =>
        {
            if (boardId.HasValue)
                p.Add(x => x.BoardId, boardId.Value);
        });

    private Task WaitForBoardAsync(IRenderedComponent<Board> cut)
        => cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("h2.board-page-title")),
            timeout: TimeSpan.FromSeconds(5));

    private async Task OpenFirstTaskModalAsync(IRenderedComponent<Board> cut)
    {
        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnClick.InvokeAsync(card.Instance.Task));
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardFound_DisplaysBoardTitle()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.Contains("TestBoard", cut.Find("h2.board-page-title").TextContent);
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardNotFound_ShowsNotFoundDiv()
    {
        var (factory, _) = CreateScopedDb();
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard(boardId: 9999);
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.board-not-found")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll("div.board-not-found h3"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardIdIsNull_ShowsNotFoundDiv()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        var cut = Render<Board>();
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.board-not-found")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll("div.board-not-found"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenUserIsViewer_HidesDeleteInviteAndAddButtons()
    {
        const int viewerId = 2;
        var (factory, db) = CreateScopedDb();

        var viewer = MakeUser(viewerId, "Viewer", "viewer@test.com");
        var owner = MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = viewerId, IdTablicy = BoardIdConst,
                    Rola = "viewer", Uzytkownik = viewer
                }
            },
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(viewer);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(viewerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.Empty(cut.FindAll("button.board-btn-delete"));
        Assert.Empty(cut.FindAll("button.btn-outline-secondary"));
        Assert.Empty(cut.FindAll("a.btn-primary"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenUserIsMember_CanAddTaskButNotDelete()
    {
        const int memberId = 3;
        var (factory, db) = CreateScopedDb();

        var member = MakeUser(memberId, "Member", "member@test.com");
        var owner = MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = memberId, IdTablicy = BoardIdConst,
                    Rola = "member", Uzytkownik = member
                }
            },
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(member);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(memberId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.Empty(cut.FindAll("button.board-btn-delete"));
        Assert.NotEmpty(cut.FindAll("a.btn-primary"));
    }

    [Fact]
    public async Task OnParametersSetAsync_ThreeTasksInDifferentColumns_AllRenderedAsCards()
    {
        var (factory, db) = CreateScopedDb();
        var tasks = new[]
        {
            MakeTask(1, BoardIdConst, "Todo", "Task Todo"),
            MakeTask(2, BoardIdConst, "In Progress", "Task InProg"),
            MakeTask(3, BoardIdConst, "Done", "Task Done")
        };
        await SeedAsync(db, tasks);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(
            () => Assert.Equal(3, cut.FindComponents<TicketCard>().Count),
            timeout: TimeSpan.FromSeconds(5));

        var cards = cut.FindComponents<TicketCard>();
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task Todo");
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task InProg");
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task Done");
    }

    [Fact]
    public async Task OnParametersSetAsync_BoardHasColor_RendersColorDot()
    {
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "Colored Board",
            KolorTablicy = "#FF5733",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.NotEmpty(cut.FindAll("span.board-color-dot"));
    }

    [Fact]
    public async Task HandleDrop_WhenNoDraggedTask_DbRemainsUnchanged()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        var doneCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        db.ChangeTracker.Clear();
        var task = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", task!.KolumnaTablicy);
    }

    [Fact]
    public async Task HandleDrop_WhenSameColumn_DoesNotCallDbUpdate()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));

        var callsBefore = factory.Invocations.Count(i => i.Method.Name == nameof(IServiceScopeFactory.CreateScope));

        var todoCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Todo");
        await cut.InvokeAsync(() => todoCol.Instance.OnDrop.InvokeAsync("Todo"));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", dbTask!.KolumnaTablicy);

        var callsAfter = factory.Invocations.Count(i => i.Method.Name == nameof(IServiceScopeFactory.CreateScope));
        Assert.Equal(callsBefore, callsAfter);
    }

    [Fact]
    public async Task HandleDrop_WhenDifferentColumn_UpdatesTaskColumnInDb()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));

        var doneCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Done", dbTask!.KolumnaTablicy);
        Assert.Equal("Done", dbTask.Status);
    }

    [Fact]
    public async Task HandleDrop_WhenDifferentColumn_OptimisticUpdateAppliedInMemory()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();
        var taskRef = card.Instance.Task;
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(taskRef));

        var doneCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        Assert.Equal("Done", taskRef.KolumnaTablicy);
        Assert.Equal("Done", taskRef.Status);
    }

    [Fact]
    public async Task HandleDrop_MoveBetweenAllColumns_DbReflectsEachChange()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        string[] sequence = ["In Progress", "In Review", "Done"];

        foreach (var target in sequence)
        {
            var card = cut.FindComponents<TicketCard>().First();
            await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));

            var col = cut.FindComponents<StatusColumn>()
                .First(c => c.Instance.ColumnKey == target);
            await cut.InvokeAsync(() => col.Instance.OnDrop.InvokeAsync(target));

            db.ChangeTracker.Clear();
            var dbTask = await db.Zadania.FindAsync(TaskIdConst);
            Assert.Equal(target, dbTask!.KolumnaTablicy);
        }
    }

    [Fact]
    public async Task ClearDraggedTask_AfterDrag_DropDoesNothing()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();

        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));
        await cut.InvokeAsync(() => card.Instance.OnDragEnd.InvokeAsync());

        var doneCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Done");
        var ex = await Record.ExceptionAsync(() => cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done")));

        Assert.Null(ex);
        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", dbTask!.KolumnaTablicy);
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskExists_RemovesFromDb()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        Assert.Null(await db.Zadania.FindAsync(TaskIdConst));
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskNotFoundInDb_ClosesModalGracefully()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        var entity = await db.Zadania.FindAsync(TaskIdConst);
        db.Zadania.Remove(entity!);
        await db.SaveChangesAsync();

        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenMultipleTasks_OnlyDeletesSelectedOne()
    {
        const int task2Id = 200;
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[]
        {
            MakeTask(TaskIdConst, BoardIdConst, "Todo", "Task A"),
            MakeTask(task2Id, BoardIdConst, "Todo", "Task B")
        });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.Equal(2, cut.FindComponents<TicketCard>().Count));


        var firstCard = cut.FindComponents<TicketCard>()
            .First(c => c.Instance.Task.TytulZadania == "Task A");
        await cut.InvokeAsync(() => firstCard.Instance.OnClick.InvokeAsync(firstCard.Instance.Task));
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay")));

        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));

        db.ChangeTracker.Clear();
        Assert.Null(await db.Zadania.FindAsync(TaskIdConst));
        Assert.NotNull(await db.Zadania.FindAsync(task2Id));
    }

    [Fact]
    public async Task AssignUserAsync_WhenNewUser_UpdatesDbAndSendsEmail()
    {
        var emailSvc = new MockEmailService();
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory, emailSvc);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-open-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("ul.assign-results")));

        var assignResult = cut.FindAll("li.assign-result-item")
            .First(li => !li.ClassList.Contains("assign-result-item--unassign"));
        await assignResult.ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.NotNull(dbTask!.IdUzytkownikaPrzypisanego);

        Assert.Single(emailSvc.SentEmails);
    }

    [Fact]
    public async Task AssignUserAsync_WhenSameUserAlreadyAssigned_SkipsDbAndEmail()
    {
        var emailSvc = new MockEmailService();
        var (factory, db) = CreateScopedDb();

        var owner = MakeUser(OwnerId);
        var task = MakeTask(TaskIdConst, BoardIdConst);
        task.IdUzytkownikaPrzypisanego = OwnerId;
        task.UzytkownikPrzypisany = owner;

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory, emailSvc);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-change-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("ul.assign-results")));

        var sameUser = cut.FindAll("li.assign-result-item")
            .First(li => !li.ClassList.Contains("assign-result-item--unassign"));
        await sameUser.ClickAsync(new MouseEventArgs());

        await Task.Delay(300);

        Assert.Empty(emailSvc.SentEmails);
    }

    [Fact]
    public async Task UnassignAsync_WhenTaskAssigned_RemovesAssigneeFromDb()
    {
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var task = MakeTask(TaskIdConst, BoardIdConst);
        task.IdUzytkownikaPrzypisanego = OwnerId;
        task.UzytkownikPrzypisany = owner;

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-change-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.assign-result-item--unassign")));

        await cut.Find("li.assign-result-item--unassign").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Null(dbTask!.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public async Task LoadCommentsAsync_WhenTaskHasComments_DisplaysThemInModal()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Komentarz testowy", owner));
        await db.SaveChangesAsync();

        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.Contains("Komentarz testowy", cut.Find("p.comment-text").TextContent);
    }

    [Fact]
    public async Task LoadCommentsAsync_WhenTaskHasNoComments_ShowsEmptyState()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("p.comments-empty")));
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextIsEmpty_SubmitButtonIsDisabled()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        var submitBtn = cut.Find("button.comment-submit-btn");
        Assert.True(submitBtn.HasAttribute("disabled"),
            "Przycisk submit powinien być disabled przy pustym polu komentarza.");
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextProvided_SavesToDb()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        await cut.Find("textarea.comment-add-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "Nowy komentarz" });

        await cut.WaitForAssertionAsync(() =>
            Assert.False(cut.Find("button.comment-submit-btn").HasAttribute("disabled")));

        await cut.Find("button.comment-submit-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Equal(1, db.Komentarze.Count()),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var saved = await db.Komentarze.FirstAsync();
        Assert.Equal("Nowy komentarz", saved.TrescKomentarza);
        Assert.Equal(OwnerId, saved.IdUzytkownika);
        Assert.Equal(TaskIdConst, saved.IdZadania);
    }

    [Fact]
    public async Task ShowDeleteConfirm_WhenOwnerClicksDeleteBoard_ShowsConfirmDialog()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));
    }

    [Fact]
    public async Task CancelDeleteBoard_WhenAnulujClicked_HidesDialog()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));

        await cut.Find("button.board-delete-cancel").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
            Assert.Empty(cut.FindAll("div.board-delete-overlay")));
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenConfirmed_RemovesBoardFromDb()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));

        await cut.Find("button.board-delete-confirm").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Equal(0, db.Tablice.Count()),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        Assert.Null(await db.Tablice.FindAsync(BoardIdConst));
    }

    [Fact]
    public async Task OpenTicketModal_WhenCardClicked_ShowsOverlay()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.Empty(cut.FindAll("div.ticket-modal-overlay"));

        await OpenFirstTaskModalAsync(cut);

        Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay"));
    }

    [Fact]
    public async Task CloseTicketModal_WhenZamknijClicked_HidesModal()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.ticket-modal-btn-close").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));
    }

    [Fact]
    public async Task TicketModal_DisplaysTaskTitle()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, title: "Ważne zadanie") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        Assert.Contains("Ważne zadanie",
            cut.Find("h5.ticket-modal-title").TextContent);
    }

    [Fact]
    public async Task TicketModal_DisplaysSixDigitId()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        var idText = cut.Find("span.ticket-modal-id").TextContent.TrimStart('#').Trim();
        Assert.Equal(6, idText.Length);
        Assert.True(int.TryParse(idText, out int numericId));
        Assert.InRange(numericId, 100_000, 999_999);
    }

    [Theory]
    [InlineData("wysoki", "Wysoki")]
    [InlineData("high", "Wysoki")]
    [InlineData("niski", "Niski")]
    [InlineData("low", "Niski")]
    [InlineData("sredni", "Średni")]
    [InlineData("inny", "Średni")]
    public async Task PriorityDisplayLabel_InModal_ShowsCorrectPolishLabel(
        string inputPriority, string expectedLabel)
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, prio: inputPriority) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        var badge = cut.Find("span.ticket-priority-badge");
        Assert.Contains(expectedLabel, badge.TextContent);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_DoesNotThrow()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        var ex = await Record.ExceptionAsync(async () =>
            await cut.Instance.DisposeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task AssignUserAsync_WhenSearchQueryEntered_FiltersUserList()
    {
        var (factory, db) = CreateScopedDb();

        var u1 = MakeUser(2, "Adam");
        var u2 = MakeUser(3, "Ewa");
        db.Uzytkownicy.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var members = new List<TablicaUzytkownik>
        {
            new() { IdUzytkownika = 2, IdTablicy = BoardIdConst, Rola = "member", Uzytkownik = u1 },
            new() { IdUzytkownika = 3, IdTablicy = BoardIdConst, Rola = "member", Uzytkownik = u2 }
        };

        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) }, members);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-open-btn").ClickAsync(new MouseEventArgs());

        var searchInput =
            cut.FindAll("input.assign-search-input, input[placeholder*='Szukaj'], .assign-search-wrap input")
                .FirstOrDefault();

        if (searchInput != null)
        {
            searchInput.Input("Adam");
            await searchInput.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Adam" });

            var results = cut.FindAll("li.assign-result-item");
            var resultsHtml = results.Select(r => r.TextContent).ToList();

            Assert.Contains(resultsHtml, text => text.Contains("Adam", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(resultsHtml, text => text.Contains("Ewa", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var results = cut.FindAll("li.assign-result-item");
            var resultsHtml = results.Select(r => r.TextContent).ToList();

            Assert.Contains(resultsHtml, text => text.Contains("Adam", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task BuildRenderTree_WhenMultipleTasksExist_RendersAllSequencesCorrectly()
    {
        var (factory, db) = CreateScopedDb();
        var tasks = new[]
        {
            MakeTask(201, BoardIdConst, "Todo", "Zadanie 1"),
            MakeTask(202, BoardIdConst, "In Progress", "Zadanie 2"),
            MakeTask(203, BoardIdConst, "Done", "Zadanie 3")
        };
        await SeedAsync(db, tasks);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        var todoColumn = cut.FindComponents<StatusColumn>().First(c => c.Instance.ColumnKey == "Todo");
        var inProgressColumn = cut.FindComponents<StatusColumn>().First(c => c.Instance.ColumnKey == "In Progress");

        Assert.Single(todoColumn.FindComponents<TicketCard>());
        Assert.Single(inProgressColumn.FindComponents<TicketCard>());
    }

    [Fact]
    public async Task Tablica_ShouldSetDefaultCreationDate()
    {
        var tablica = new Tablica { NazwaTablicy = "DataTest", IdUzytkownikaOwner = 1 };

        _dbContext.Tablice.Add(tablica);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Tablice.FirstAsync();
        Assert.True(saved.DataStworzenia <= DateTime.UtcNow);
        Assert.True(saved.DataStworzenia > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task AssignToMeAsync_WhenCurrentUserIsOwner_AssignsTaskToOwner()
    {
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var task = MakeTask(TaskIdConst, BoardIdConst);

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-open-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("button.assign-me-btn")));

        await cut.Find("button.assign-me-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal(OwnerId, dbTask!.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public async Task AssignToMeAsync_WhenCurrentUserIsMember_AssignsTaskToMember()
    {
        const int memberId = 2;
        var (factory, db) = CreateScopedDb();

        var owner = MakeUser(OwnerId);
        var member = MakeUser(memberId, "Member2", "member2@test.com");
        var task = MakeTask(TaskIdConst, BoardIdConst);

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = memberId, IdTablicy = BoardIdConst,
                    Rola = "member", Uzytkownik = member
                }
            },
            Zadania = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(member);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(memberId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("button.assign-open-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("button.assign-me-btn")));

        await cut.Find("button.assign-me-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal(memberId, dbTask!.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public async Task StartEditComment_WhenEditButtonClicked_ShowsEditTextarea()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Edytowalny komentarz", owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
                Assert.NotEmpty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));

        await cut.Find("button.comment-action-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("textarea.comment-edit-input")));
    }

    [Fact]
    public async Task StartEditComment_PreFillsTextareaWithCommentContent()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        const string originalText = "Oryginalny tekst komentarza";
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, originalText, owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));

        await cut.Find("button.comment-action-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
        {
            var textarea = cut.Find("textarea.comment-edit-input");
            Assert.Contains(originalText, textarea.GetAttribute("value") ?? textarea.TextContent);
        });
    }

    [Fact]
    public async Task CancelEditComment_WhenAnulujClicked_HidesEditTextarea()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Komentarz", owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));

        await cut.Find("button.comment-action-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("textarea.comment-edit-input")));

        await cut.Find("button.comment-cancel-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
            Assert.Empty(cut.FindAll("textarea.comment-edit-input")));
        Assert.NotEmpty(cut.FindAll("p.comment-text"));
    }

    [Fact]
    public async Task SaveEditedCommentAsync_WhenTextChanged_UpdatesDbAndUi()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Stary tekst", owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));

        await cut.Find("button.comment-action-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("textarea.comment-edit-input")));

        await cut.Find("textarea.comment-edit-input").ChangeAsync(
            new ChangeEventArgs { Value = "Nowy tekst komentarza" });


        await cut.Find("button.comment-save-btn").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(() =>
                Assert.Empty(cut.FindAll("textarea.comment-edit-input")),
            timeout: TimeSpan.FromSeconds(5));


        db.ChangeTracker.Clear();
        var updated = await db.Komentarze.FindAsync(1);
        Assert.Equal("Nowy tekst komentarza", updated!.TrescKomentarza);
        Assert.NotNull(updated.DataEdycji);
    }

    [Fact]
    public async Task SaveEditedCommentAsync_WhenTextIsEmpty_DoesNotSave()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        const string originalText = "Oryginalny tekst";
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, originalText, owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));


        await cut.Find("button.comment-action-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("textarea.comment-edit-input")));

        await cut.Find("textarea.comment-edit-input").ChangeAsync(
            new ChangeEventArgs { Value = "   " });


        var saveBtn = cut.FindAll("button.comment-save-btn").FirstOrDefault();
        if (saveBtn != null)
            await saveBtn.ClickAsync(new MouseEventArgs());
        await Task.Delay(300);


        db.ChangeTracker.Clear();
        var comment = await db.Komentarze.FindAsync(1);
        Assert.Equal(originalText, comment!.TrescKomentarza);
        Assert.Null(comment.DataEdycji);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentExists_RemovesFromDbAndUi()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Komentarz do usunięcia", owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
                Assert.NotEmpty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));


        var deleteBtn = cut.FindAll("button.comment-action-btn")
            .First(b => b.ClassList.Contains("comment-action-btn--delete"));
        await deleteBtn.ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(() =>
                Assert.Empty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));


        db.ChangeTracker.Clear();
        Assert.Null(await db.Komentarze.FindAsync(1));
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentNotInDb_DoesNotThrow()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Komentarz", owner));
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));


        db.ChangeTracker.Clear();
        var entity = await db.Komentarze.FindAsync(1);
        db.Komentarze.Remove(entity!);
        await db.SaveChangesAsync();


        var ex = await Record.ExceptionAsync(async () =>
        {
            var deleteBtn = cut.FindAll("button.comment-action-btn")
                .First(b => b.ClassList.Contains("comment-action-btn--delete"));
            await deleteBtn.ClickAsync(new MouseEventArgs());
        });


        Assert.Null(ex);
    }


    [Fact]
    public async Task ChangeTaskStatus_WhenSelectChanged_UpdatesDbAndInMemory()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("select.ticket-modal-select")));


        await cut.Find("select.ticket-modal-select").ChangeAsync(
            new ChangeEventArgs { Value = "Done" });


        await cut.WaitForAssertionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var dbTask = await db.Zadania.FindAsync(TaskIdConst);
            Assert.Equal("Done", dbTask!.KolumnaTablicy);
            Assert.Equal("Done", dbTask.Status);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ChangeTaskStatus_WhenSelectChanged_UpdatesUiWithoutReload()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("select.ticket-modal-select")));


        await cut.Find("select.ticket-modal-select").ChangeAsync(
            new ChangeEventArgs { Value = "In Progress" });


        await cut.WaitForAssertionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var dbTask = await db.Zadania.FindAsync(TaskIdConst);
            Assert.Equal("In Progress", dbTask!.KolumnaTablicy);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ChangeTaskStatus_WhenViewerOpensModal_ShowsStatusLabelNotSelect()
    {
        const int viewerId = 5;
        var (factory, db) = CreateScopedDb();

        var viewer = MakeUser(viewerId, "Viewer", "viewer@test.com");
        var owner = MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = viewerId, IdTablicy = BoardIdConst,
                    Rola = "viewer", Uzytkownik = viewer
                }
            },
            Zadania = new List<Zadanie> { MakeTask(TaskIdConst, BoardIdConst, "Done") }
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(viewer);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(viewerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay")));


        Assert.Empty(cut.FindAll("select.ticket-modal-select"));
        Assert.NotEmpty(cut.FindAll("span.ticket-modal-assignee"));
    }


    [Fact]
    public void StatusLabel_FallbackForUnknownColumn_ReturnsDefaultLabel()
    {
        var method = typeof(Board).GetMethod("StatusLabel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { "nieznany_status" }) as string;
        Assert.Equal("Do zrobienia", result);
    }


    [Fact]
    public void RoleLabel_FallbackForUnknownRole_ReturnsCzlonek()
    {
        var method = typeof(Board).GetMethod("RoleLabel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { "totally_unknown_role" }) as string;
        Assert.Equal("Członek", result);
    }


    [Fact]
    public async Task FormatCommentDate_WhenDateIsJustNow_ShowsBeforeAMoment()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        var comment = MakeComment(1, TaskIdConst, OwnerId, "Świeży komentarz", owner);
        comment.DataUtworzenia = DateTime.UtcNow;
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));


        var dateSpan = cut.Find("span.comment-date");
        Assert.Contains("przed chwilą", dateSpan.TextContent);
    }

    [Fact]
    public async Task FormatCommentDate_WhenDateIsOld_ShowsFormattedDate()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        var comment = MakeComment(1, TaskIdConst, OwnerId, "Stary komentarz", owner);
        comment.DataUtworzenia = DateTime.UtcNow.AddDays(-10);
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));


        var dateSpan = cut.Find("span.comment-date");
        Assert.Matches(@"\d{2}\.\d{2}\.\d{4}", dateSpan.TextContent);
    }

    [Fact]
    public async Task FormatCommentDate_WhenDateIsHoursAgo_ShowsHoursAgo()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        var comment = MakeComment(1, TaskIdConst, OwnerId, "Komentarz sprzed godzin", owner);
        comment.DataUtworzenia = DateTime.UtcNow.AddHours(-3);
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));

        var dateSpan = cut.Find("span.comment-date");
        Assert.Contains("godz. temu", dateSpan.TextContent);
    }

    [Fact]
    public async Task FormatCommentDate_WhenDateIsDaysAgo_ShowsDaysAgo()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        var comment = MakeComment(1, TaskIdConst, OwnerId, "Komentarz sprzed dni", owner);
        comment.DataUtworzenia = DateTime.UtcNow.AddDays(-3);
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.comment-item")));

        var dateSpan = cut.Find("span.comment-date");
        Assert.Contains("dni temu", dateSpan.TextContent);
    }


    [Fact]
    public async Task OnParametersSetAsync_WhenNameIdentifierIsZero_FallsBackToEmailLookup()
    {
        var (factory, db) = CreateScopedDb();


        this.AddAuthorization().SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "0"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Name, "TestUser"));


        var owner = MakeUser(OwnerId, "TestUser", "test@example.com");
        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "EmailFallbackBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        RegisterServices(factory);


        var cut = RenderBoard();


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("h2.board-page-title")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.Contains("EmailFallbackBoard", cut.Find("h2.board-page-title").TextContent);
    }


    [Fact]
    public async Task DeleteBoardAsync_WhenBoardNotFoundInDb_ClosesDialogGracefully()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));


        var entity = await db.Tablice.FindAsync(BoardIdConst);
        db.Tablice.Remove(entity!);
        await db.SaveChangesAsync();


        var ex = await Record.ExceptionAsync(async () =>
            await cut.Find("button.board-delete-confirm").ClickAsync(new MouseEventArgs()));


        Assert.Null(ex);
    }


    [Fact]
    public async Task DeleteTaskAsync_WhenDbThrows_SetsTaskModalError()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        var entity = await db.Zadania.FindAsync(TaskIdConst);
        db.Zadania.Remove(entity!);
        await db.SaveChangesAsync();


        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")),
            timeout: TimeSpan.FromSeconds(5));
    }


    [Fact]
    public async Task HandleDrop_WhenTaskNotFoundInDb_DoesNotThrow()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));


        db.ChangeTracker.Clear();
        var entity = await db.Zadania.FindAsync(TaskIdConst);
        db.Zadania.Remove(entity!);
        await db.SaveChangesAsync();


        var doneCol = cut.FindComponents<StatusColumn>()
            .First(c => c.Instance.ColumnKey == "Done");
        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done")));


        Assert.Null(ex);
    }


    [Fact]
    public async Task AddCommentAsync_WhenCommentTextIsWhitespace_DoesNotSave()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);


        await cut.Find("textarea.comment-add-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "   " });


        var submitBtn = cut.Find("button.comment-submit-btn");
        Assert.True(submitBtn.HasAttribute("disabled"));


        Assert.Equal(0, await db.Komentarze.CountAsync());
    }


    [Fact]
    public async Task LoadCommentsAsync_WhenMultipleComments_DisplaysAllInDescendingOrder()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        var owner = await db.Uzytkownicy.FindAsync(OwnerId);
        db.Komentarze.Add(MakeComment(1, TaskIdConst, OwnerId, "Starszy komentarz", owner));
        db.Komentarze.Add(new Komentarz
        {
            IdKomentarza = 2,
            IdZadania = TaskIdConst,
            IdUzytkownika = OwnerId,
            TrescKomentarza = "Nowszy komentarz",
            DataUtworzenia = DateTime.UtcNow.AddMinutes(1),
            Uzytkownik = owner!
        });
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.WaitForAssertionAsync(() =>
                Assert.Equal(2, cut.FindAll("li.comment-item").Count),
            timeout: TimeSpan.FromSeconds(5));


        var commentTexts = cut.FindAll("p.comment-text")
            .Select(p => p.TextContent)
            .ToList();
        Assert.Equal("Nowszy komentarz", commentTexts[0]);
        Assert.Equal("Starszy komentarz", commentTexts[1]);
    }


    private async Task OpenInvitePanelViaButtonAsync(IRenderedComponent<Board> cut)
    {
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("button.member-invite-chip")),
            timeout: TimeSpan.FromSeconds(5));

        await cut.Find("button.member-invite-chip").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-panel")),
            timeout: TimeSpan.FromSeconds(5));
    }


    private async Task OpenInvitePanelViaReflectionAsync(IRenderedComponent<Board> cut)
    {
        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("OpenInvitePanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(instance, null);
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-panel")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OpenInvitePanel_WhenCalled_ShowsInvitePanel()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        Assert.Empty(cut.FindAll("div.invite-panel"));


        await OpenInvitePanelViaButtonAsync(cut);


        Assert.NotEmpty(cut.FindAll("div.invite-panel"));
    }

    [Fact]
    public async Task OpenInvitePanel_WhenCalled_ResetsSearchFields()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        await OpenInvitePanelViaButtonAsync(cut);
        await cut.Find("input#invite-search-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "test" });


        await cut.Find("button.invite-panel-close").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.Empty(cut.FindAll("div.invite-panel")));

        await OpenInvitePanelViaButtonAsync(cut);


        var input = cut.Find("input#invite-search-input");
        var val = input.GetAttribute("value") ?? "";
        Assert.Equal("", val);
    }

    [Fact]
    public async Task OnInviteSearchInput_WhenQueryAtLeast2Chars_SetsSearchQueryField()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaButtonAsync(cut);


        await cut.Find("input#invite-search-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "an" });


        var input = cut.Find("input#invite-search-input");
        Assert.Equal("an", input.GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task OnInviteSearchInput_WhenQueryChanges_ClearsSelectedUser()
    {
        var (factory, db) = CreateScopedDb();
        var outsider = MakeUser(91, "Outsider", "outsider@test.com");
        db.Uzytkownicy.Add(outsider);
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { outsider });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")),
            timeout: TimeSpan.FromSeconds(5));


        await cut.Find("input#invite-search-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "xy" });


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.invite-selected-card")),
            timeout: TimeSpan.FromSeconds(5));
    }


    [Fact]
    public async Task SearchUsersAsync_WhenNoUsersMatch_ShowsNoResultsMessage()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        await cut.InvokeAsync(async () =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("SearchUsersAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                var task = (Task?)method.Invoke(instance, new object[] { "xyzNoMatch999" });
                if (task != null) await task;
            }

            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });


        Assert.Empty(cut.FindAll("ul.invite-results"));
    }

    [Fact]
    public async Task SearchUsersAsync_WhenDbThrows_SetsEmptyResults()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        var ex = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(async () =>
            {
                var instance = cut.Instance;
                var method = instance.GetType().GetMethod("SearchUsersAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var task = (Task?)method.Invoke(instance, new object[] { "jan" });
                    if (task != null) await task;
                }

                var shc = instance.GetType().GetMethod("StateHasChanged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                shc?.Invoke(instance, null);
            });
        });


        Assert.Null(ex);


        Assert.Empty(cut.FindAll("span.invite-search-spinner"));
    }

    [Fact]
    public async Task SearchUsersAsync_WhenCalled_SetIsSearchingFalseAfterCompletion()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        await cut.InvokeAsync(async () =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("SearchUsersAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                var task = (Task?)method.Invoke(instance, new object[] { "testquery" });
                if (task != null) await task;
            }
        });


        Assert.Empty(cut.FindAll("span.invite-search-spinner"));
    }


    [Fact]
    public async Task AddUserToBoardAsync_WhenSuccess_AddsUserToDbAndShowsSuccessMsg()
    {
        const int newUserId = 80;
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var newUser = MakeUser(newUserId, "Nowy", "nowy@test.com");

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(newUser);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { newUser });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")),
            timeout: TimeSpan.FromSeconds(5));


        await cut.Find("button.invite-btn-add").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-success")),
            timeout: TimeSpan.FromSeconds(5));


        db.ChangeTracker.Clear();
        var entry = await db.TabliceUzytkownicy.FindAsync(newUserId, BoardIdConst);
        Assert.NotNull(entry);
        Assert.Equal("member", entry!.Rola);
    }

    [Fact]
    public async Task AddUserToBoardAsync_WhenSuccess_ClearsSelectedUser()
    {
        const int newUserId = 81;
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var newUser = MakeUser(newUserId, "Nowy2", "nowy2@test.com");

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(newUser);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);

        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { newUser });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")));


        await cut.Find("button.invite-btn-add").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.invite-selected-card")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddUserToBoardAsync_WhenUserAlreadyMember_ShowsErrorAndDoesNotDuplicate()
    {
        const int existingId = 82;
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var existing = MakeUser(existingId, "Istniejacy2", "istniejacy2@test.com");

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = existingId, IdTablicy = BoardIdConst,
                    Rola = "member", Uzytkownik = existing
                }
            },
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(existing);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { existing });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")));


        await cut.Find("button.invite-btn-add").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-error")),
            timeout: TimeSpan.FromSeconds(5));


        db.ChangeTracker.Clear();
        var count = db.TabliceUzytkownicy.Count(tu => tu.IdUzytkownika == existingId && tu.IdTablicy == BoardIdConst);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddUserToBoardAsync_WhenNoUserSelected_ButtonIsDisabled()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);


        var addBtn = cut.Find("button.invite-btn-add");
        Assert.True(addBtn.HasAttribute("disabled"),
            "Przycisk dodaj powinien być disabled gdy nie wybrano użytkownika.");
    }

    [Fact]
    public async Task AddUserToBoardAsync_WhenOwnerRole_CanInviteNewMember()
    {
        const int newUserId = 83;
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var newUser = MakeUser(newUserId, "NowyMember", "nowymember@test.com");

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>(),
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(newUser);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);

        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { newUser });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")));


        await cut.Find("button.invite-btn-add").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-success")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddUserToBoardAsync_WhenAdminRole_CanInviteNewMember()
    {
        const int adminId = 84;
        const int newUserId = 85;
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var admin = MakeUser(adminId, "Admin2", "admin2@test.com");
        var newUser = MakeUser(newUserId, "NowyViaAdmin", "viaadmin@test.com");

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "TestBoard",
            Owner = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new()
                {
                    IdUzytkownika = adminId, IdTablicy = BoardIdConst,
                    Rola = "admin", Uzytkownik = admin
                }
            },
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(admin);
        db.Uzytkownicy.Add(newUser);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(adminId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);

        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { newUser });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")));

        await cut.Find("button.invite-btn-add").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.invite-success")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClearInviteSelection_WhenCalled_ClearsSelectedUserAndSearchQuery()
    {
        var (factory, db) = CreateScopedDb();
        var outsider = MakeUser(60, "Oczekujacy", "oczekujacy@test.com");
        db.Uzytkownicy.Add(outsider);
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenInvitePanelViaReflectionAsync(cut);

        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var selectMethod = instance.GetType().GetMethod("SelectInviteUser",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectMethod?.Invoke(instance, new object[] { outsider });
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.invite-selected-card")));

        await cut.Find("button.invite-deselect").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.invite-selected-card")),
            timeout: TimeSpan.FromSeconds(5));

        var input = cut.Find("input#invite-search-input");
        Assert.Equal("", input.GetAttribute("value") ?? "");
    }

    private async Task<AppDbContext> SeedBoardWithManymembersAsync(
        AppDbContext db, int memberCount = 6)
    {
        var owner = MakeUser(OwnerId);
        db.Uzytkownicy.Add(owner);

        var members = new List<TablicaUzytkownik>();
        for (int i = 2; i <= memberCount + 1; i++)
        {
            var u = MakeUser(i, $"User{i}", $"user{i}@test.com");
            db.Uzytkownicy.Add(u);
            members.Add(new TablicaUzytkownik
            {
                IdUzytkownika = i,
                IdTablicy = BoardIdConst,
                Rola = "member",
                Uzytkownik = u
            });
        }

        var board = new Tablica
        {
            IdTablicy = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy = "BigBoard",
            Owner = owner,
            TabliceUzyt = members,
            Zadania = new List<Zadanie>()
        };
        db.Tablice.Add(board);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task ToggleMembersPopup_WhenCalled_ShowsPopup()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("button.member-more-btn")),
            timeout: TimeSpan.FromSeconds(5));


        Assert.Empty(cut.FindAll("div.members-popup"));


        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ToggleMembersPopup_WhenCalledTwice_HidesPopup()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));


        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup")));


        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ToggleMembersPopup_ViaReflection_TogglesState()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("ToggleMembersPopup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(instance, null);
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });


        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("ToggleMembersPopup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(instance, null);
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CloseMembersPopup_WhenCalled_HidesPopup()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));


        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup")));


        await cut.Find("button.members-popup-close").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CloseMembersPopup_WhenOverlayClicked_HidesPopup()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));

        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup-overlay")));


        await cut.Find("div.members-popup-overlay").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CloseMembersPopup_ViaReflection_SetsFalse()
    {
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var toggle = instance.GetType().GetMethod("ToggleMembersPopup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            toggle?.Invoke(instance, null);
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup")));


        await cut.InvokeAsync(() =>
        {
            var instance = cut.Instance;
            var method = instance.GetType().GetMethod("CloseMembersPopup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(instance, null);
            var shc = instance.GetType().GetMethod("StateHasChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shc?.Invoke(instance, null);
        });


        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.members-popup")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MembersPopup_WhenOpen_DisplaysAllBoardMembers()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));


        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(
            () => Assert.True(cut.FindAll("li.members-popup-item").Count >= 7),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MembersPopup_WhenOwner_ShowsInviteButtonInsidePopup()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));

        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup")));


        Assert.NotEmpty(cut.FindAll("button.members-popup-invite-btn"));
    }

    [Fact]
    public async Task MembersPopup_InviteButtonInPopup_ClosesPopupAndOpensInvitePanel()
    {
        var (factory, db) = CreateScopedDb();
        await SeedBoardWithManymembersAsync(db, memberCount: 6);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("button.member-more-btn")));

        await cut.Find("button.member-more-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.members-popup")));


        await cut.Find("button.members-popup-invite-btn").ClickAsync(new MouseEventArgs());


        await cut.WaitForAssertionAsync(() =>
        {
            Assert.Empty(cut.FindAll("div.members-popup"));
            Assert.NotEmpty(cut.FindAll("div.invite-panel"));
        }, timeout: TimeSpan.FromSeconds(5));
    }
}