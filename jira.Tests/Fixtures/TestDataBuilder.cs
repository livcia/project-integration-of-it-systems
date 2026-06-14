using jira.DbModels;

namespace jira.Tests.Fixtures;

public class TestDataBuilder
{
    public static Uzytkownik CreateUser(
        int id = 1,
        string email = "test@example.com",
        string username = "testuser",
        string? googleId = null,
        string? githubId = null)
    {
        return new Uzytkownik
        {
            IdUzytkownika = id,
            Email = email,
            NazwaUzytkownika = username,
            AvatarUrl = $"https://api.dicebear.com/10.x/lorelei/svg?seed={username}",
            PasswordHash = "hashed_password",
            DataRejestracji = DateTime.UtcNow,
            GoogleId = googleId,
            GitHubId = githubId
        };
    }

    public static Tablica CreateBoard(
        int id = 1,
        int ownerId = 1,
        string name = "Test Board",
        string? description = null,
        string? color = "#FF5733")
    {
        return new Tablica
        {
            IdTablicy = id,
            IdUzytkownikaOwner = ownerId,
            NazwaTablicy = name,
            OpisTablicy = description,
            KolorTablicy = color,
            DataStworzenia = DateTime.UtcNow
        };
    }

    public static Zadanie CreateTask(
        int id = 1,
        int boardId = 1,
        int creatorId = 1,
        string title = "Test Task",
        string? description = null,
        string status = "Todo",
        string priority = "sredni",
        int? assignedToId = null,
        string column = "Todo")
    {
        return new Zadanie
        {
            IdZadania = id,
            IdTablicy = boardId,
            TytulZadania = title,
            OpisZadania = description,
            Status = status,
            Priorytet = priority,
            IdUzytkownikaPrzypisanego = assignedToId,
            IdUzytkownikaTworcyZadania = creatorId,
            KolumnaTablicy = column,
            DataStworzenia = DateTime.UtcNow
        };
    }

    public static Komentarz CreateComment(
        int id = 1,
        int taskId = 1,
        int authorId = 1,
        string content = "Test comment")
    {
        return new Komentarz
        {
            IdKomentarza = id,
            IdZadania = taskId,
            IdUzytkownika = authorId,
            TrescKomentarza = content,
            DataUtworzenia = DateTime.UtcNow
        };
    }

    public static TablicaUzytkownik CreateBoardAccess(int boardId = 1, int userId = 1)
    {
        return new TablicaUzytkownik
        {
            IdTablicy = boardId,
            IdUzytkownika = userId
        };
    }
}

