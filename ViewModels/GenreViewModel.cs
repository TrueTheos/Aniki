namespace Aniki.ViewModels;

internal sealed partial class GenreViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsSelected { get; set; }
    public string Name { get; }

    public GenreViewModel(string name)
    {
        Name = name;
    }
}
