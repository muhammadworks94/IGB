using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IGB.Web.ViewModels.Components;

public sealed record AlertViewModel(
    string Message,
    string Variant = "info", // success|danger|warning|info
    bool Dismissible = true,
    string? Title = null,
    string? CssClass = null
);

public sealed record LoadingViewModel(
    string Label = "Loading...",
    bool FullWidth = false,
    string? CssClass = null
);

public sealed record ModalViewModel(
    string Id,
    string Title,
    Func<IHtmlHelper, IHtmlContent> Body,
    Func<IHtmlHelper, IHtmlContent>? Footer = null,
    bool StaticBackdrop = false,
    string? CssClass = null
);

public sealed record ConfirmDialogViewModel(
    string Id,
    string Title,
    string Message,
    string ConfirmText = "Confirm",
    string CancelText = "Cancel",
    string ConfirmButtonVariant = "danger",
    string? FormAction = null,
    string? FormController = null,
    object? FormRouteValues = null
);


