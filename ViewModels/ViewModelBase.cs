using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Aniki.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        public virtual async Task Enter() { }
    }
}
