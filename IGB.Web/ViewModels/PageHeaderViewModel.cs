namespace IGB.Web.ViewModels;

public sealed record BreadcrumbItem(string Text, string? Url = null);

public sealed record PageHeaderActionButton(
    string Text,
    string? Url = null,
    string? Icon = null,
    string ButtonType = "primary", // primary, secondary, outline-primary, etc.
    string? ModalTarget = null, // For Bootstrap modal triggers
    bool IsSubmit = false,
    string? OnClick = null,
    string? BadgeText = null, // For badge/notification count
    string? BadgeVariant = "warning" // warning, danger, info, etc.
);

public sealed record PageHeaderSearch(
    string? Placeholder = null,
    string? QueryParamName = "q",
    string? CurrentValue = null,
    string? FormAction = null,
    string? FormMethod = "get",
    bool ShowClearButton = true,
    string? ClearButtonUrl = null
);

public sealed record PageHeaderFilter(
    string Label,
    string Name,
    string Type = "select", // select, input, etc.
    string? Value = null,
    Dictionary<string, string>? Options = null, // For select dropdowns
    string? Placeholder = null,
    string? CssClass = null
);

public sealed record PageHeaderViewModel(
    string Title,
    IReadOnlyList<BreadcrumbItem>? Breadcrumbs = null,
    string? Subtitle = null,
    bool Centered = false,
    PageHeaderSearch? Search = null,
    IReadOnlyList<PageHeaderActionButton>? ActionButtons = null,
    IReadOnlyList<PageHeaderFilter>? Filters = null
);


