using jira.DbModels;
using jira.Tests.Fixtures;

namespace jira.Tests.Data;

/// <summary>
/// Testy jednostkowe dla TestDataBuilder – helpera do tworzenia encji testowych.
/// </summary>
public class TestDataBuilderTests
{
    // ─── CreateUser() ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateUser_WithDefaults_ReturnsUserWithId1()
    {
        // Act
        var user = TestDataBuilder.CreateUser();

        // Assert
        Assert.Equal(1, user.IdUzytkownika);
    }

    [Fact]
    public void CreateUser_WithDefaults_ReturnsUserWithDefaultEmail()
    {
        var user = TestDataBuilder.CreateUser();
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public void CreateUser_WithDefaults_ReturnsUserWithDefaultUsername()
    {
        var user = TestDataBuilder.CreateUser();
        Assert.Equal("testuser", user.NazwaUzytkownika);
    }

    [Fact]
    public void CreateUser_WithDefaults_HasNonNullAvatarUrl()
    {
        var user = TestDataBuilder.CreateUser();
        Assert.NotNull(user.AvatarUrl);
        Assert.NotEmpty(user.AvatarUrl!);
    }

    [Fact]
    public void CreateUser_WithDefaults_HasPasswordHash()
    {
        var user = TestDataBuilder.CreateUser();
        Assert.Equal("hashed_password", user.PasswordHash);
    }

    [Fact]
    public void CreateUser_WithDefaults_GoogleIdAndGithubIdAreNull()
    {
        var user = TestDataBuilder.CreateUser();
        Assert.Null(user.GoogleId);
        Assert.Null(user.GitHubId);
    }

    [Fact]
    public void CreateUser_WithCustomParameters_ReturnsCorrectValues()
    {
        // Act
        var user = TestDataBuilder.CreateUser(
            id: 42,
            email: "custom@domain.com",
            username: "customname",
            googleId: "gid-123",
            githubId: "ghid-456");

        // Assert
        Assert.Equal(42, user.IdUzytkownika);
        Assert.Equal("custom@domain.com", user.Email);
        Assert.Equal("customname", user.NazwaUzytkownika);
        Assert.Equal("gid-123", user.GoogleId);
        Assert.Equal("ghid-456", user.GitHubId);
    }

    [Fact]
    public void CreateUser_AvatarUrl_ContainsUsername()
    {
        var user = TestDataBuilder.CreateUser(username: "myuser");
        Assert.Contains("myuser", user.AvatarUrl);
    }

    [Fact]
    public void CreateUser_DataRejestracji_IsRecentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var user = TestDataBuilder.CreateUser();
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.InRange(user.DataRejestracji, before, after);
    }

    // ─── CreateBoard() ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateBoard_WithProvidedOwnerId_SetsOwnerIdCorrectly()
    {
        // Act
        var board = TestDataBuilder.CreateBoard(id: 7, ownerId: 99);

        // Assert
        Assert.Equal(7, board.IdTablicy);
        Assert.Equal(99, board.IdUzytkownikaOwner);
    }

    [Fact]
    public void CreateBoard_WithDefaults_HasDefaultName()
    {
        var board = TestDataBuilder.CreateBoard();
        Assert.Equal("Test Board", board.NazwaTablicy);
    }

    [Fact]
    public void CreateBoard_WithDefaults_HasDefaultColor()
    {
        var board = TestDataBuilder.CreateBoard();
        Assert.Equal("#FF5733", board.KolorTablicy);
    }

    [Fact]
    public void CreateBoard_WithDefaults_DescriptionIsNull()
    {
        var board = TestDataBuilder.CreateBoard();
        Assert.Null(board.OpisTablicy);
    }

    [Fact]
    public void CreateBoard_WithCustomName_SetsNameCorrectly()
    {
        var board = TestDataBuilder.CreateBoard(name: "Sprint 1");
        Assert.Equal("Sprint 1", board.NazwaTablicy);
    }

    [Fact]
    public void CreateBoard_WithDescription_SetsDescriptionCorrectly()
    {
        var board = TestDataBuilder.CreateBoard(description: "Opis testowy");
        Assert.Equal("Opis testowy", board.OpisTablicy);
    }

    [Fact]
    public void CreateBoard_DataStworzenia_IsRecentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var board = TestDataBuilder.CreateBoard();
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.InRange(board.DataStworzenia, before, after);
    }

    // ─── CreateTask() ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateTask_SetsKolumnaTablicy_FromColumnParameter()
    {
        // Act
        var task = TestDataBuilder.CreateTask(column: "InProgress");

        // Assert
        Assert.Equal("InProgress", task.KolumnaTablicy);
    }

    [Fact]
    public void CreateTask_SetsStatus_FromStatusParameter()
    {
        // Act
        var task = TestDataBuilder.CreateTask(status: "Done");

        // Assert
        Assert.Equal("Done", task.Status);
    }

    [Fact]
    public void CreateTask_DefaultColumn_IsTodo()
    {
        var task = TestDataBuilder.CreateTask();
        Assert.Equal("Todo", task.KolumnaTablicy);
    }

    [Fact]
    public void CreateTask_DefaultStatus_IsTodo()
    {
        var task = TestDataBuilder.CreateTask();
        Assert.Equal("Todo", task.Status);
    }

    [Fact]
    public void CreateTask_WithCustomId_SetsIdCorrectly()
    {
        var task = TestDataBuilder.CreateTask(id: 55);
        Assert.Equal(55, task.IdZadania);
    }

    [Fact]
    public void CreateTask_WithBoardId_SetsIdTablicy()
    {
        var task = TestDataBuilder.CreateTask(boardId: 33);
        Assert.Equal(33, task.IdTablicy);
    }

    [Fact]
    public void CreateTask_WithCreatorId_SetsIdUzytkownikaTworcyZadania()
    {
        var task = TestDataBuilder.CreateTask(creatorId: 7);
        Assert.Equal(7, task.IdUzytkownikaTworcyZadania);
    }

    [Fact]
    public void CreateTask_WithAssignedToId_SetsAssignedUser()
    {
        var task = TestDataBuilder.CreateTask(assignedToId: 5);
        Assert.Equal(5, task.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public void CreateTask_DefaultAssignedToId_IsNull()
    {
        var task = TestDataBuilder.CreateTask();
        Assert.Null(task.IdUzytkownikaPrzypisanego);
    }

    [Fact]
    public void CreateTask_DefaultPriority_IsSredni()
    {
        var task = TestDataBuilder.CreateTask();
        Assert.Equal("sredni", task.Priorytet);
    }

    [Fact]
    public void CreateTask_WithTitle_SetsTytulZadania()
    {
        var task = TestDataBuilder.CreateTask(title: "Moje zadanie");
        Assert.Equal("Moje zadanie", task.TytulZadania);
    }

    [Fact]
    public void CreateTask_StatusAndColumn_CanBeSetIndependently()
    {
        // Arrange & Act
        var task = TestDataBuilder.CreateTask(status: "InProgress", column: "Done");

        // Assert – status i kolumna mogą się różnić
        Assert.Equal("InProgress", task.Status);
        Assert.Equal("Done", task.KolumnaTablicy);
    }

    // ─── CreateComment() ───────────────────────────────────────────────────────

    [Fact]
    public void CreateComment_FillsTrescKomentarza()
    {
        // Act
        var comment = TestDataBuilder.CreateComment(content: "Świetna robota!");

        // Assert
        Assert.Equal("Świetna robota!", comment.TrescKomentarza);
    }

    [Fact]
    public void CreateComment_DefaultContent_IsTestComment()
    {
        var comment = TestDataBuilder.CreateComment();
        Assert.Equal("Test comment", comment.TrescKomentarza);
    }

    [Fact]
    public void CreateComment_WithDefaultParams_HasId1()
    {
        var comment = TestDataBuilder.CreateComment();
        Assert.Equal(1, comment.IdKomentarza);
    }

    [Fact]
    public void CreateComment_WithTaskId_SetsIdZadania()
    {
        var comment = TestDataBuilder.CreateComment(taskId: 99);
        Assert.Equal(99, comment.IdZadania);
    }

    [Fact]
    public void CreateComment_WithAuthorId_SetsIdUzytkownika()
    {
        var comment = TestDataBuilder.CreateComment(authorId: 17);
        Assert.Equal(17, comment.IdUzytkownika);
    }

    [Fact]
    public void CreateComment_DataUtworzenia_IsRecentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var comment = TestDataBuilder.CreateComment();
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.InRange(comment.DataUtworzenia, before, after);
    }

    [Fact]
    public void CreateComment_WithCustomId_SetsIdCorrectly()
    {
        var comment = TestDataBuilder.CreateComment(id: 42);
        Assert.Equal(42, comment.IdKomentarza);
    }

    // ─── CreateBoardAccess() ───────────────────────────────────────────────────

    [Fact]
    public void CreateBoardAccess_WithDefaults_HasBoardId1AndUserId1()
    {
        var access = TestDataBuilder.CreateBoardAccess();
        Assert.Equal(1, access.IdTablicy);
        Assert.Equal(1, access.IdUzytkownika);
    }

    [Fact]
    public void CreateBoardAccess_WithCustomIds_SetsCorrectly()
    {
        var access = TestDataBuilder.CreateBoardAccess(boardId: 5, userId: 10);
        Assert.Equal(5, access.IdTablicy);
        Assert.Equal(10, access.IdUzytkownika);
    }
}
