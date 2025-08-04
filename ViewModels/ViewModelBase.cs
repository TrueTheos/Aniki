using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace Aniki.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        public virtual async Task Enter() { }
    }
}
