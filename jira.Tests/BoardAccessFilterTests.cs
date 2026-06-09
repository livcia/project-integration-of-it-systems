using jira.Components.Pages.Projects;
using jira.DbModels;

namespace jira.Tests;

public class BoardAccessFilterTests
{
    [Fact]
    public void GetAccessibleBoards_ReturnsOwnedAndAssignedBoardsOnly()
    {
        const int currentUserId = 10;
        var boards = new List<Tablica>
        {
            new() { IdTablicy = 1, NazwaTablicy = "Owned board", IdUzytkownikaOwner = currentUserId },
            new() { IdTablicy = 2, NazwaTablicy = "Assigned board", IdUzytkownikaOwner = 22 },
            new() { IdTablicy = 3, NazwaTablicy = "Hidden board", IdUzytkownikaOwner = 23 }
        };
        var links = new List<TablicaUzytkownik>
        {
            new() { IdUzytkownika = currentUserId, IdTablicy = 2 },
            new() { IdUzytkownika = 99, IdTablicy = 3 }
        };

        var result = BoardAccessFilter.GetAccessibleBoards(boards.AsQueryable(), links.AsQueryable(), currentUserId)
            .Select(b => b.IdTablicy)
            .OrderBy(id => id)
            .ToList();

        Assert.Equal([1, 2], result);
    }

    [Fact]
    public void GetAccessibleBoards_DoesNotDuplicateOwnerBoardWithMembershipLink()
    {
        const int currentUserId = 10;
        var boards = new List<Tablica>
        {
            new() { IdTablicy = 1, NazwaTablicy = "Owner and member", IdUzytkownikaOwner = currentUserId }
        };
        var links = new List<TablicaUzytkownik>
        {
            new() { IdUzytkownika = currentUserId, IdTablicy = 1 }
        };

        var result = BoardAccessFilter.GetAccessibleBoards(boards.AsQueryable(), links.AsQueryable(), currentUserId).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].IdTablicy);
    }
}
