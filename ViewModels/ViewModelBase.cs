

namespace Aniki.ViewModels;

internal abstract class ViewModelBase : ObservableObject
{
    public virtual Task Enter()
    {
        return Task.CompletedTask;
    }
}
