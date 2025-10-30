

namespace Aniki.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual Task Enter()
    {
        return Task.CompletedTask;
    }
}
