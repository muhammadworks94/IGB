namespace IGB.Web.ViewModels.Components;

public sealed record StarRatingViewModel(
    string Name,
    int Value,
    string Label,
    string? HelpText = null,
    bool Required = true
);


