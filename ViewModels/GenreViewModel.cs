namespace Aniki.ViewModels;

public partial class GenreViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string Name { get; }

    public GenreViewModel(string name)
    {
        Name = name;
    }
}
