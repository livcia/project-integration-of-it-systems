using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Comments;

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
        using var db = _fixture.CreateDbContext();
        
        var author = TestDataBuilder.CreateUser();
        db.Uzytkownicy.Add(author);
        
        var board = TestDataBuilder.CreateBoard(ownerId: author.IdUzytkownika);
        db.Tablice.Add(board);
        
        var task = TestDataBuilder.CreateTask(boardId: board.IdTablicy, creatorId: author.IdUzytkownika);
        db.Zadania.Add(task);
        await db.SaveChangesAsync();

        var comment = TestDataBuilder.CreateComment(
            taskId: task.IdZadania,
            authorId: author.IdUzytkownika,
            content: "This is a test comment"
        );
        db.Komentarze.Add(comment);
        await db.SaveChangesAsync();

        var createdComment = await db.Komentarze
            .Include(c => c.Uzytkownik)
            .FirstAsync(c => c.IdZadania == task.IdZadania);
        
        Assert.NotNull(createdComment);
        Assert.Equal("This is a test comment", createdComment.TrescKomentarza);
        Assert.Equal(author.IdUzytkownika, createdComment.Uzytkownik.IdUzytkownika);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}

