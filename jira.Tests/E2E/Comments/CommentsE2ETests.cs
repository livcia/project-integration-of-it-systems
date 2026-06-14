using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Comments;

/// <summary>
/// E2E testy dla systemu komentarzy do zadań
/// </summary>
public class CommentsE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public CommentsE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }
    [Fact]
    public async Task AddComment_Should_CreateNewComment()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Act - Dodanie komentarza
        var comment = TestDataBuilder.CreateComment(
            taskId: task.IdZadania,
            authorId: author.IdUzytkownika,
            content: "This is a test comment"
        );
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        // Assert
        var createdComment = await db.Komentarze
            .Include(c => c.Uzytkownik)
            .FirstAsync(c => c.IdZadania == task.IdZadania);
        
        Assert.NotNull(createdComment);
        Assert.Equal("This is a test comment", createdComment.TrescKomentarza);
        Assert.Equal(author.IdUzytkownika, createdComment.Uzytkownik.IdUzytkownika);
    }

    [Fact]
    public async Task GetTaskComments_Should_ReturnAllCommentsForTask()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Dodanie wielu komentarzy
        db.Komentarze.AddRange(
            TestDataBuilder.CreateComment(id: 1, taskId: task.IdZadania, authorId: author.IdUzytkownika, content: "Comment 1"),
            TestDataBuilder.CreateComment(id: 2, taskId: task.IdZadania, authorId: author.IdUzytkownika, content: "Comment 2"),
            TestDataBuilder.CreateComment(id: 3, taskId: task.IdZadania, authorId: author.IdUzytkownika, content: "Comment 3")
        );
        await db.SaveChangesAsync();

        // Act
        var comments = await db.Komentarze
            .Where(c => c.IdZadania == task.IdZadania)
            .OrderBy(c => c.DataUtworzenia)
            .ToListAsync();

        // Assert
        Assert.Equal(3, comments.Count);
        Assert.Equal("Comment 1", comments[0].TrescKomentarza);
        Assert.Equal("Comment 2", comments[1].TrescKomentarza);
        Assert.Equal("Comment 3", comments[2].TrescKomentarza);
    }

    [Fact]
    public async Task EditComment_Should_UpdateCommentContent()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        
        var comment = TestDataBuilder.CreateComment(
            taskId: task.IdZadania,
            authorId: author.IdUzytkownika,
            content: "Original comment"
        );
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        // Act - Edycja komentarza
        var toEdit = await db.Komentarze.FindAsync(comment.IdKomentarza);
        Assert.NotNull(toEdit);
        toEdit.TrescKomentarza = "Edited comment";
        toEdit.DataEdycji = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Assert
        var edited = await db.Komentarze.FindAsync(comment.IdKomentarza);
        Assert.Equal("Edited comment", edited.TrescKomentarza);
        Assert.NotNull(edited.DataEdycji);
    }

    [Fact]
    public async Task DeleteComment_Should_RemoveComment()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        
        var comment = TestDataBuilder.CreateComment(taskId: task.IdZadania, authorId: author.IdUzytkownika);
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        // Act - Usunięcie komentarza
        var toDelete = await db.Komentarze.FindAsync(comment.IdKomentarza);
        Assert.NotNull(toDelete);
        db.Komentarze.Remove(toDelete);
        await db.SaveChangesAsync();

        // Assert
        var deleted = await db.Komentarze.FindAsync(comment.IdKomentarza);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task CommentsByDifferentUsers_Should_BePreserved()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var user1 = TestDataBuilder.CreateUser(id: 1, username: "user1");
        var user2 = TestDataBuilder.CreateUser(id: 2, email: "user2@example.com", username: "user2");
        var user3 = TestDataBuilder.CreateUser(id: 3, email: "user3@example.com", username: "user3");
        db.Uzytkownicy.AddRange(user1, user2, user3);
        
        var board = TestDataBuilder.CreateBoard(ownerId: user1.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: user1.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Komentarze od różnych użytkowników
        db.Komentarze.AddRange(
            TestDataBuilder.CreateComment(id: 1, taskId: task.IdZadania, authorId: user1.IdUzytkownika, content: "Comment from user1"),
            TestDataBuilder.CreateComment(id: 2, taskId: task.IdZadania, authorId: user2.IdUzytkownika, content: "Comment from user2"),
            TestDataBuilder.CreateComment(id: 3, taskId: task.IdZadania, authorId: user3.IdUzytkownika, content: "Comment from user3")
        );
        await db.SaveChangesAsync();

        // Act
        var comments = await db.Komentarze
            .Include(c => c.Uzytkownik)
            .Where(c => c.IdZadania == task.IdZadania)
            .OrderBy(c => c.DataUtworzenia)
            .ToListAsync();

        // Assert
        Assert.Equal(3, comments.Count);
        Assert.Equal(user1.IdUzytkownika, comments[0].Uzytkownik.IdUzytkownika);
        Assert.Equal(user2.IdUzytkownika, comments[1].Uzytkownik.IdUzytkownika);
        Assert.Equal(user3.IdUzytkownika, comments[2].Uzytkownik.IdUzytkownika);
    }

    [Fact]
    public async Task CommentDatesChronology_Should_BeCorrect()
    {
        // Arrange
        // handled by fixture
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        // Tworzenie komentarzy z różnymi datami
        var now = DateTime.UtcNow;
        db.Komentarze.AddRange(
            new Komentarz { IdKomentarza = 1, IdZadania = task.IdZadania, IdUzytkownika = author.IdUzytkownika, TrescKomentarza = "First", DataUtworzenia = now },
            new Komentarz { IdKomentarza = 2, IdZadania = task.IdZadania, IdUzytkownika = author.IdUzytkownika, TrescKomentarza = "Second", DataUtworzenia = now.AddSeconds(1) },
            new Komentarz { IdKomentarza = 3, IdZadania = task.IdZadania, IdUzytkownika = author.IdUzytkownika, TrescKomentarza = "Third", DataUtworzenia = now.AddSeconds(2) }
        );
        await db.SaveChangesAsync();

        // Act
        var comments = await db.Komentarze
            .Where(c => c.IdZadania == task.IdZadania)
            .OrderBy(c => c.DataUtworzenia)
            .ToListAsync();

        // Assert
        Assert.Equal(3, comments.Count);
        Assert.True(comments[0].DataUtworzenia <= comments[1].DataUtworzenia);
        Assert.True(comments[1].DataUtworzenia <= comments[2].DataUtworzenia);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}

