using IGB.Application.DTOs;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels;

public class PendingUsersViewModel
{
    public List<UserDto> Users { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new PaginationViewModel(1, 10, 0, Action: "Index", Controller: "Approvals");
    public string? Query { get; set; }
}


