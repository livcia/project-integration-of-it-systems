using jira.DbModels;

namespace jira.Components.Pages.Projects;

public static class BoardAccessFilter
{
    public static IQueryable<Tablica> GetAccessibleBoards(
        IQueryable<Tablica> boards,
        IQueryable<TablicaUzytkownik> boardUsers,
        int userId)
    {
        return boards.Where(board =>
            board.IdUzytkownikaOwner == userId ||
            boardUsers.Any(link => link.IdUzytkownika == userId && link.IdTablicy == board.IdTablicy));
    }
}
