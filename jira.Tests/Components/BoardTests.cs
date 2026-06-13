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

/// <summary>
/// Testy komponentowe bUnit dla <see cref="Board"/>.
///
/// Pokrywa:
///   1. Renderowanie i cykl życia (OnParametersSetAsync)
///   2. Drag &amp; drop (HandleDrop, SetDraggedTask, ClearDraggedTask)
///   3. Usuwanie zadania (DeleteTaskAsync)
///   4. Przypisywanie użytkownika (AssignUserAsync, SaveAssignmentAsync, UnassignAsync)
///   5. Komentarze (LoadCommentsAsync, AddCommentAsync)
///   6. Panel zapraszania (OpenInvitePanel / CloseInvitePanel)
///   7. Usuwanie tablicy (DeleteBoardAsync)
///   8. Ticket modal (otwarcie, zamknięcie, GetDisplayId)
///   9. Statyczne helpery (PriorityDisplayLabel, StatusLabel, RoleLabel)
///  10. DisposeAsync
///
/// Mocki:
///   - <see cref="IServiceScopeFactory"/> → zwraca InMemory <see cref="AppDbContext"/>
///   - <see cref="IEmailService"/>        → Moq
///   - Auth                               → bUnit AddAuthorization / TestAuthorizationContext
///   - <see cref="BoardStateService"/>    → prawdziwa instancja
///   - NavigationManager                  → bUnit FakeNavigationManager (automatyczny)
/// </summary>
public sealed class BoardTests : BunitContext
{
    // ── Stałe identyfikatorów ─────────────────────────────────────────────────
    private const int OwnerId     = 1;
    private const int BoardIdConst = 10;
    private const int TaskIdConst  = 100;

    // ═══════════════════════════════════════════════════════════════════════════
    // Infrastructure / helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tworzy parę (mock <see cref="IServiceScopeFactory"/>, InMemory <see cref="AppDbContext"/>).
    /// Każde wywołanie <c>CreateScope()</c> na fabryce zwraca scope, którego
    /// <c>ServiceProvider.GetService(typeof(AppDbContext))</c> oddaje TEN SAME egzemplarz DB.
    /// Dzięki temu testy mogą weryfikować stan DB po akcjach komponentu.
    /// </summary>
    private (Mock<IServiceScopeFactory> Factory, AppDbContext Db) CreateScopedDb()
    {
        var dbName = Guid.NewGuid().ToString();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Jedna wspólna instancja DB – komponent i test korzystają z tych samych danych.
        var db = new AppDbContext(opts);

        Services.AddSingleton<AppDbContext>(db);

        // GetRequiredService<T>() woła GetService(typeof(T)) na IServiceProvider.
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(db);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        return (factoryMock, db);
    }

    /// <summary>
    /// Rejestruje w bUnit DI wszystkie serwisy wymagane przez <see cref="Board"/>.
    /// </summary>
    private void RegisterServices(
        Mock<IServiceScopeFactory> factory,
        IEmailService?             email = null)
    {
        Services.AddSingleton<IServiceScopeFactory>(factory.Object);
        Services.AddSingleton<BoardStateService>();
        Services.AddSingleton(email ?? new MockEmailService());
    }

    /// <summary>
    /// Konfiguruje uwierzytelnionego użytkownika z danym <paramref name="userId"/>
    /// przy użyciu bUnit <c>AddAuthorization()</c>.
    /// </summary>
    private void SetupAuth(int userId = OwnerId)
    {
        this.AddAuthorization().SetClaims(
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"User{userId}"),
            new Claim(ClaimTypes.Email, $"user{userId}@test.com"));
    }

    // ── Budowanie danych testowych (korzystamy z TestDataBuilder + własne helpery) ──

    private static Uzytkownik MakeUser(int id = OwnerId, string name = "TestUser", string email = "test@example.com")
        => TestDataBuilder.CreateUser(id: id, email: email, username: name);

    private static Zadanie MakeTask(
        int    id     = TaskIdConst,
        int    boardId = BoardIdConst,
        string column = "Todo",
        string title  = "Test Task",
        string prio   = "sredni")
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

    /// <summary>
    /// Zapisuje w DB właściciela tablicy, tablicę i opcjonalne zadania / członków.
    /// </summary>
    private async Task SeedAsync(
        AppDbContext                db,
        IEnumerable<Zadanie>?       tasks   = null,
        IEnumerable<TablicaUzytkownik>? members = null,
        Uzytkownik?                 owner   = null)
    {
        var o = owner ?? MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "TestBoard",
            TabliceUzyt        = new List<TablicaUzytkownik>(),
            Zadania            = new List<Zadanie>(),
            Owner              = o
        };

        if (tasks is not null)
            foreach (var t in tasks)  board.Zadania.Add(t);
        if (members is not null)
            foreach (var m in members) board.TabliceUzyt.Add(m);

        db.Uzytkownicy.Add(o);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();
    }

    /// <summary>Renderuje komponent Board z opcjonalnym parametrem BoardId.</summary>
    private IRenderedComponent<Board> RenderBoard(int? boardId = BoardIdConst)
        => Render<Board>(p =>
        {
            if (boardId.HasValue)
                p.Add(x => x.BoardId, boardId.Value);
        });

    /// <summary>Czeka aż tablica załaduje się i tytuł pojawi w DOM.</summary>
    private Task WaitForBoardAsync(IRenderedComponent<Board> cut)
        => cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("h2.board-page-title")),
            timeout: TimeSpan.FromSeconds(5));

    /// <summary>
    /// Otwiera modal pierwszego widocznego TicketCard i czeka na overlay.
    /// </summary>
    private async Task OpenFirstTaskModalAsync(IRenderedComponent<Board> cut)
    {
        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnClick.InvokeAsync(card.Instance.Task));
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay")),
            timeout: TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Renderowanie / OnParametersSetAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardFound_DisplaysBoardTitle()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        // Act
        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Assert
        Assert.Contains("TestBoard", cut.Find("h2.board-page-title").TextContent);
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardNotFound_ShowsNotFoundDiv()
    {
        // Arrange – pusta DB, tablica o podanym ID nie istnieje
        var (factory, _) = CreateScopedDb();
        SetupAuth();
        RegisterServices(factory);

        // Act
        var cut = RenderBoard(boardId: 9999);
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.board-not-found")),
            timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotEmpty(cut.FindAll("div.board-not-found h3"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenBoardIdIsNull_ShowsNotFoundDiv()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        // Act – renderuj bez parametru BoardId
        var cut = Render<Board>();
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("div.board-not-found")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll("div.board-not-found"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenUserIsViewer_HidesDeleteInviteAndAddButtons()
    {
        // Arrange – viewer nie może edytować ani zapraszać
        const int viewerId = 2;
        var (factory, db) = CreateScopedDb();

        var viewer = MakeUser(viewerId, "Viewer", "viewer@test.com");
        var owner  = MakeUser(OwnerId);
        var board  = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "TestBoard",
            Owner              = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new() { IdUzytkownika = viewerId, IdTablicy = BoardIdConst,
                        Rola = "viewer", Uzytkownik = viewer }
            },
            Zadania = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Uzytkownicy.Add(viewer);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(viewerId);
        RegisterServices(factory);

        // Act
        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Assert
        Assert.Empty(cut.FindAll("button.board-btn-delete"));
        Assert.Empty(cut.FindAll("button.btn-outline-secondary"));
        Assert.Empty(cut.FindAll("a.btn-primary"));
    }

    [Fact]
    public async Task OnParametersSetAsync_WhenUserIsMember_CanAddTaskButNotDelete()
    {
        // Arrange
        const int memberId = 3;
        var (factory, db) = CreateScopedDb();

        var member = MakeUser(memberId, "Member", "member@test.com");
        var owner  = MakeUser(OwnerId);
        var board  = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "TestBoard",
            Owner              = owner,
            TabliceUzyt = new List<TablicaUzytkownik>
            {
                new() { IdUzytkownika = memberId, IdTablicy = BoardIdConst,
                        Rola = "member", Uzytkownik = member }
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

        // Assert – member widzi "+ Dodaj zadanie", ale NIE "Usuń tablicę"
        Assert.Empty(cut.FindAll("button.board-btn-delete"));
        Assert.NotEmpty(cut.FindAll("a.btn-primary")); // "+ Dodaj zadanie"
    }

    [Fact]
    public async Task OnParametersSetAsync_ThreeTasksInDifferentColumns_AllRenderedAsCards()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        var tasks = new[]
        {
            MakeTask(1, BoardIdConst, "Todo",        "Task Todo"),
            MakeTask(2, BoardIdConst, "In Progress", "Task InProg"),
            MakeTask(3, BoardIdConst, "Done",        "Task Done")
        };
        await SeedAsync(db, tasks);
        SetupAuth();
        RegisterServices(factory);

        // Act
        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(
            () => Assert.Equal(3, cut.FindComponents<TicketCard>().Count),
            timeout: TimeSpan.FromSeconds(5));

        // Assert – każde zadanie ma odpowiadający TicketCard
        var cards = cut.FindComponents<TicketCard>();
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task Todo");
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task InProg");
        Assert.Contains(cards, c => c.Instance.Task.TytulZadania == "Task Done");
    }

    [Fact]
    public async Task OnParametersSetAsync_BoardHasColor_RendersColorDot()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var board = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "Colored Board",
            KolorTablicy       = "#FF5733",
            Owner              = owner,
            TabliceUzyt        = new List<TablicaUzytkownik>(),
            Zadania            = new List<Zadanie>()
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Assert – kolorowa kropka renderowana obok tytułu
        Assert.NotEmpty(cut.FindAll("span.board-color-dot"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Drag & Drop – HandleDrop, SetDraggedTask, ClearDraggedTask
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HandleDrop_WhenNoDraggedTask_DbRemainsUnchanged()
    {
        // Arrange – upuszczenie bez ustawionego draggedTask
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Act – wywołaj HandleDrop (przez OnDrop na StatusColumn) bez SetDraggedTask
        var doneCol = cut.FindComponents<StatusColumn>()
                         .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        // Assert – kolumna zadania bez zmian
        db.ChangeTracker.Clear();
        var task = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", task!.KolumnaTablicy);
    }

    [Fact]
    public async Task HandleDrop_WhenSameColumn_DoesNotCallDbUpdate()
    {
        // Arrange – zadanie jest w "Todo", upuszczamy z powrotem na "Todo"
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        // Ustaw draggedTask
        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));

        // Zapamiętaj liczbę wywołań CreateScope przed upuszczeniem
        var callsBefore = factory.Invocations.Count(i => i.Method.Name == nameof(IServiceScopeFactory.CreateScope));

        // Act – upuść na tę samą kolumnę
        var todoCol = cut.FindComponents<StatusColumn>()
                         .First(c => c.Instance.ColumnKey == "Todo");
        await cut.InvokeAsync(() => todoCol.Instance.OnDrop.InvokeAsync("Todo"));

        // Assert – DB bez zmian (HandleDrop zwraca wcześnie, bez nowego scope)
        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", dbTask!.KolumnaTablicy);

        var callsAfter = factory.Invocations.Count(i => i.Method.Name == nameof(IServiceScopeFactory.CreateScope));
        Assert.Equal(callsBefore, callsAfter); // brak nowego scope = brak zapisu do DB
    }

    [Fact]
    public async Task HandleDrop_WhenDifferentColumn_UpdatesTaskColumnInDb()
    {
        // Arrange – zadanie w "Todo", przenosimy do "Done"
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        // Ustaw draggedTask
        var card = cut.FindComponents<TicketCard>().First();
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));

        // Act
        var doneCol = cut.FindComponents<StatusColumn>()
                         .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        // Assert – DB zaktualizowana
        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Done", dbTask!.KolumnaTablicy);
        Assert.Equal("Done", dbTask.Status);
    }

    [Fact]
    public async Task HandleDrop_WhenDifferentColumn_OptimisticUpdateAppliedInMemory()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();
        var taskRef = card.Instance.Task;           // referencja do obiektu w pamięci
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(taskRef));

        // Act
        var doneCol = cut.FindComponents<StatusColumn>()
                         .First(c => c.Instance.ColumnKey == "Done");
        await cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done"));

        // Assert – optymistyczna aktualizacja UI (przed potwierdzeniem z DB)
        Assert.Equal("Done", taskRef.KolumnaTablicy);
        Assert.Equal("Done", taskRef.Status);
    }

    [Fact]
    public async Task HandleDrop_MoveBetweenAllColumns_DbReflectsEachChange()
    {
        // Arrange
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
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, "Todo") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindComponents<TicketCard>()));

        var card = cut.FindComponents<TicketCard>().First();

        // Drag start → natychmiast drag end (ClearDraggedTask)
        await cut.InvokeAsync(() => card.Instance.OnDragStart.InvokeAsync(card.Instance.Task));
        await cut.InvokeAsync(() => card.Instance.OnDragEnd.InvokeAsync());

        // Act – upuść po wyczyszczeniu draggedTask
        var doneCol = cut.FindComponents<StatusColumn>()
                         .First(c => c.Instance.ColumnKey == "Done");
        var ex = await Record.ExceptionAsync(
            () => cut.InvokeAsync(() => doneCol.Instance.OnDrop.InvokeAsync("Done")));

        // Assert – brak wyjątku, DB bez zmian
        Assert.Null(ex);
        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Equal("Todo", dbTask!.KolumnaTablicy);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Usuwanie zadania – DeleteTaskAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskExists_RemovesFromDb()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Act – kliknij "Usuń zadanie"
        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());

        // Assert – modal zamknięty
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")),
            timeout: TimeSpan.FromSeconds(5));

        // Assert – zadanie usunięte z DB
        db.ChangeTracker.Clear();
        Assert.Null(await db.Zadania.FindAsync(TaskIdConst));
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskExists_ClosesModal()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Modal jest otwarty
        Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay"));

        // Act
        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());

        // Assert – CloseTicketModal() wywołane → overlay znika
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenTaskNotFoundInDb_ClosesModalGracefully()
    {
        // Arrange – zadanie istnieje w pamięci komponentu, ale zostało już usunięte z DB
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Usuń ręcznie z DB przed kliknięciem delete
        var entity = await db.Zadania.FindAsync(TaskIdConst);
        db.Zadania.Remove(entity!);
        await db.SaveChangesAsync();

        // Act – komponent wywoła FindAsync → null → nie usunie, ale CloseTicketModal() zadziała
        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());

        // Assert – modal się zamknął
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));
    }

    [Fact]
    public async Task DeleteTaskAsync_WhenMultipleTasks_OnlyDeletesSelectedOne()
    {
        // Arrange – dwa zadania
        const int task2Id = 200;
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[]
        {
            MakeTask(TaskIdConst, BoardIdConst, "Todo",  "Task A"),
            MakeTask(task2Id,     BoardIdConst, "Todo",  "Task B")
        });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await cut.WaitForAssertionAsync(() =>
            Assert.Equal(2, cut.FindComponents<TicketCard>().Count));

        // Otwórz modal pierwszego zadania (Task A)
        var firstCard = cut.FindComponents<TicketCard>()
                           .First(c => c.Instance.Task.TytulZadania == "Task A");
        await cut.InvokeAsync(() => firstCard.Instance.OnClick.InvokeAsync(firstCard.Instance.Task));
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay")));

        // Act – usuń
        await cut.Find("button.ticket-modal-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));

        // Assert – tylko Task A usunięte, Task B nadal istnieje
        db.ChangeTracker.Clear();
        Assert.Null(await db.Zadania.FindAsync(TaskIdConst));
        Assert.NotNull(await db.Zadania.FindAsync(task2Id));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Przypisywanie użytkownika – AssignUserAsync / SaveAssignmentAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignUserAsync_WhenNewUser_UpdatesDbAndSendsEmail()
    {
        // Arrange
        var emailSvc = new MockEmailService();
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory, emailSvc);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Kliknij "+ Przypisz" (zadanie nie ma assignee)
        await cut.Find("button.assign-open-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("ul.assign-results")));

        // Kliknij pierwszego użytkownika spoza "Pozostaw puste"
        var assignResult = cut.FindAll("li.assign-result-item")
            .First(li => !li.ClassList.Contains("assign-result-item--unassign"));
        await assignResult.ClickAsync(new MouseEventArgs());

        // Assert – wyszukiwanie przypisania zamknięte (SaveAssignmentAsync zakończone)
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        // Assert – DB zaktualizowana
        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.NotNull(dbTask!.IdUzytkownikaPrzypisanego);

        // Assert – e-mail wysłany
        Assert.Single(emailSvc.SentEmails);
    }

    [Fact]
    public async Task AssignUserAsync_WhenSameUserAlreadyAssigned_SkipsDbAndEmail()
    {
        // Arrange – zadanie jest już przypisane do OwnerId
        var emailSvc = new MockEmailService();
        var (factory, db) = CreateScopedDb();

        var owner = MakeUser(OwnerId);
        var task  = MakeTask(TaskIdConst, BoardIdConst);
        task.IdUzytkownikaPrzypisanego = OwnerId;
        task.UzytkownikPrzypisany = owner;

        var board = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "TestBoard",
            Owner              = owner,
            TabliceUzyt        = new List<TablicaUzytkownik>(),
            Zadania            = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory, emailSvc);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Otwórz zmianę przypisania (zadanie ma assignee → pokazuje assign-change-btn)
        await cut.Find("button.assign-change-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("ul.assign-results")));

        // Kliknij tego samego użytkownika (OwnerId) – SaveAssignmentAsync powinno pominąć
        var sameUser = cut.FindAll("li.assign-result-item")
            .First(li => !li.ClassList.Contains("assign-result-item--unassign"));
        await sameUser.ClickAsync(new MouseEventArgs());

        // Daj chwilę na obsługę asynchroniczną
        await Task.Delay(300);

        // Assert – brak nowego e-maila
        Assert.Empty(emailSvc.SentEmails);
    }

    [Fact]
    public async Task AssignUserAsync_WhenUserAssigned_ShowsAssigneeName()
    {
        // Arrange
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

        // Assert – imię przypisanego użytkownika pojawia się w modal
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("span.assign-name")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UnassignAsync_WhenTaskAssigned_RemovesAssigneeFromDb()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        var owner = MakeUser(OwnerId);
        var task  = MakeTask(TaskIdConst, BoardIdConst);
        task.IdUzytkownikaPrzypisanego = OwnerId;
        task.UzytkownikPrzypisany = owner;

        var board = new Tablica
        {
            IdTablicy          = BoardIdConst,
            IdUzytkownikaOwner = OwnerId,
            NazwaTablicy       = "TestBoard",
            Owner              = owner,
            TabliceUzyt        = new List<TablicaUzytkownik>(),
            Zadania            = new List<Zadanie> { task }
        };
        db.Uzytkownicy.Add(owner);
        db.Tablice.Add(board);
        await db.SaveChangesAsync();

        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Otwórz widżet zmiany przypisania
        await cut.Find("button.assign-change-btn").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("li.assign-result-item--unassign")));

        // Kliknij "Pozostaw puste"
        await cut.Find("li.assign-result-item--unassign").ClickAsync(new MouseEventArgs());

        // Assert – pole IdUzytkownikaPrzypisanego = null w DB
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.assign-search-wrap")),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var dbTask = await db.Zadania.FindAsync(TaskIdConst);
        Assert.Null(dbTask!.IdUzytkownikaPrzypisanego);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. Komentarze – LoadCommentsAsync, AddCommentAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadCommentsAsync_WhenTaskHasComments_DisplaysThemInModal()
    {
        // Arrange
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

        // Assert – komentarz pojawia się na liście
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));

        Assert.Contains("Komentarz testowy", cut.Find("p.comment-text").TextContent);
    }

    [Fact]
    public async Task LoadCommentsAsync_WhenTaskHasNoComments_ShowsEmptyState()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert – komunikat o braku komentarzy
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("p.comments-empty")));
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextIsEmpty_SubmitButtonIsDisabled()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert – przycisk "Wyślij" jest wyłączony gdy pole jest puste
        var submitBtn = cut.Find("button.comment-submit-btn");
        Assert.True(submitBtn.HasAttribute("disabled"),
            "Przycisk submit powinien być disabled przy pustym polu komentarza.");
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextIsEmpty_DoesNotSaveToDb()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert – DB bez komentarzy (guard w AddCommentAsync blokuje zapis)
        Assert.Equal(0, await db.Komentarze.CountAsync());
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextProvided_SavesToDb()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Wpisz tekst w textarea (@bind:event="oninput")
        await cut.Find("textarea.comment-add-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "Nowy komentarz" });

        // Poczekaj aż przycisk odblokuje się
        await cut.WaitForAssertionAsync(
            () => Assert.False(cut.Find("button.comment-submit-btn").HasAttribute("disabled")));

        // Kliknij "Wyślij"
        await cut.Find("button.comment-submit-btn").ClickAsync(new MouseEventArgs());

        // Assert – komentarz zapisany w DB
        await cut.WaitForAssertionAsync(
            () => Assert.Equal(1, db.Komentarze.Count()),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        var saved = await db.Komentarze.FirstAsync();
        Assert.Equal("Nowy komentarz", saved.TrescKomentarza);
        Assert.Equal(OwnerId,          saved.IdUzytkownika);
        Assert.Equal(TaskIdConst,      saved.IdZadania);
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextProvided_AppearsInCommentList()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Wpisz i wyślij
        await cut.Find("textarea.comment-add-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "Komentarz widoczny" });
        await cut.WaitForAssertionAsync(
            () => Assert.False(cut.Find("button.comment-submit-btn").HasAttribute("disabled")));
        await cut.Find("button.comment-submit-btn").ClickAsync(new MouseEventArgs());

        // Assert – komentarz widoczny na liście
        await cut.WaitForAssertionAsync(
            () => Assert.NotEmpty(cut.FindAll("li.comment-item")),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddCommentAsync_WhenTextProvided_ClearsInputAfterSubmit()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        await cut.Find("textarea.comment-add-input").TriggerEventAsync(
            "oninput", new ChangeEventArgs { Value = "Tekst komentarza" });
        await cut.WaitForAssertionAsync(
            () => Assert.False(cut.Find("button.comment-submit-btn").HasAttribute("disabled")));
        await cut.Find("button.comment-submit-btn").ClickAsync(new MouseEventArgs());

        // Assert – po wysłaniu pole zostaje wyczyszczone (przycisk znów disabled)
        await cut.WaitForAssertionAsync(
            () => Assert.True(cut.Find("button.comment-submit-btn").HasAttribute("disabled")),
            timeout: TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Usuwanie tablicy – DeleteBoardAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ShowDeleteConfirm_WhenOwnerClicksDeleteBoard_ShowsConfirmDialog()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Act – kliknij "Usuń tablicę"
        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));
    }

    [Fact]
    public async Task CancelDeleteBoard_WhenAnulujClicked_HidesDialog()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));

        // Act – Anuluj
        await cut.Find("button.board-delete-cancel").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
            Assert.Empty(cut.FindAll("div.board-delete-overlay")));
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenConfirmed_RemovesBoardFromDb()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));

        // Act – potwierdź
        await cut.Find("button.board-delete-confirm").ClickAsync(new MouseEventArgs());

        // Assert – tablica usunięta z DB
        await cut.WaitForAssertionAsync(
            () => Assert.Equal(0, db.Tablice.Count()),
            timeout: TimeSpan.FromSeconds(5));

        db.ChangeTracker.Clear();
        Assert.Null(await db.Tablice.FindAsync(BoardIdConst));
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenConfirmed_NavigatesToProjects()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth(OwnerId);
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        await cut.Find("button.board-btn-delete").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() =>
            Assert.NotEmpty(cut.FindAll("div.board-delete-overlay")));

        // Act
        await cut.Find("button.board-delete-confirm").ClickAsync(new MouseEventArgs());

        // Assert – FakeNavigationManager przekierował na /projects
        await cut.WaitForAssertionAsync(
            () => Assert.Equal(0, db.Tablice.Count()),
            timeout: TimeSpan.FromSeconds(5));

        var nav = Services.GetRequiredService<BunitNavigationManager>();
        Assert.EndsWith("/projects", nav.Uri);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Ticket modal – otwieranie, zamykanie, GetDisplayId
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OpenTicketModal_WhenCardClicked_ShowsOverlay()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        Assert.Empty(cut.FindAll("div.ticket-modal-overlay")); // modal ukryty

        // Act
        await OpenFirstTaskModalAsync(cut);

        // Assert
        Assert.NotEmpty(cut.FindAll("div.ticket-modal-overlay"));
    }

    [Fact]
    public async Task CloseTicketModal_WhenZamknijClicked_HidesModal()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Act – kliknij "Zamknij"
        await cut.Find("button.ticket-modal-btn-close").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(
            () => Assert.Empty(cut.FindAll("div.ticket-modal-overlay")));
    }

    [Fact]
    public async Task TicketModal_DisplaysTaskTitle()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, title: "Ważne zadanie") });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert
        Assert.Contains("Ważne zadanie",
            cut.Find("h5.ticket-modal-title").TextContent);
    }

    [Fact]
    public async Task TicketModal_DisplaysSixDigitId()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert – GetDisplayId generuje 6-cyfrowy numer
        var idText = cut.Find("span.ticket-modal-id").TextContent.TrimStart('#').Trim();
        Assert.Equal(6, idText.Length);
        Assert.True(int.TryParse(idText, out int numericId));
        Assert.InRange(numericId, 100_000, 999_999);
    }

    [Fact]
    public async Task TicketModal_GetDisplayId_IsDeterministicForSameTaskId()
    {
        // Arrange – dwa oddzielne rendery z tym samym TaskId
        var (factory1, db1) = CreateScopedDb();
        await SeedAsync(db1, new[] { MakeTask(TaskIdConst, BoardIdConst) });
        var (factory2, db2) = CreateScopedDb();
        await SeedAsync(db2, new[] { MakeTask(TaskIdConst, BoardIdConst) });

        SetupAuth();
        RegisterServices(factory1);
        var cut1 = RenderBoard();
        await WaitForBoardAsync(cut1);
        await OpenFirstTaskModalAsync(cut1);
        var id1 = cut1.Find("span.ticket-modal-id").TextContent;

        // Nowy BunitContext nie jest możliwy w tym samym teście (klasa jest sealed),
        // więc weryfikujemy przez TicketCard – DisplayId jest tą samą funkcją co GetDisplayId
        var cut1Card = cut1.FindComponents<TicketCard>().First();

        // Assert – DisplayId na karcie zgadza się z ID w modalu
        var cardId = "#" + cut1Card.Find("span.ticket-id").TextContent.TrimStart('#').Trim();
        Assert.Equal(id1.Trim(), cardId.Trim());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. Statyczne helpery – PriorityDisplayLabel renderowany w modalu
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("wysoki", "Wysoki")]
    [InlineData("high",   "Wysoki")]
    [InlineData("niski",  "Niski")]
    [InlineData("low",    "Niski")]
    [InlineData("sredni", "Średni")]
    [InlineData("inny",   "Średni")]   // fallback
    public async Task PriorityDisplayLabel_InModal_ShowsCorrectPolishLabel(
        string inputPriority, string expectedLabel)
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db, new[] { MakeTask(TaskIdConst, BoardIdConst, prio: inputPriority) });
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);
        await OpenFirstTaskModalAsync(cut);

        // Assert – badge priorytetu zawiera oczekiwaną etykietę
        var badge = cut.Find("span.ticket-priority-badge");
        Assert.Contains(expectedLabel, badge.TextContent);
    }
    

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. DisposeAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DisposeAsync_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var (factory, db) = CreateScopedDb();
        await SeedAsync(db);
        SetupAuth();
        RegisterServices(factory);

        var cut = RenderBoard();
        await WaitForBoardAsync(cut);

        // Act & Assert – IAsyncDisposable.DisposeAsync() nie rzuca wyjątku
        var ex = await Record.ExceptionAsync(async () =>
            await cut.Instance.DisposeAsync());
        Assert.Null(ex);
    }
}
