using System;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Misc;

public class DependencyInjection
{
    private static DependencyInjection? _instance;
    public static DependencyInjection Instance => _instance ??= new DependencyInjection();

    public event Action? OnLogout;

    public IServiceProvider? ServiceProvider { get; private set; }

    private DependencyInjection()
    {
    }

    public void BuildServiceProvider()
    {
        var collection = new ServiceCollection();
        collection.AddCommonServices();
        ServiceProvider = collection.BuildServiceProvider();
    }

    public void Logout()
    {
        OnLogout?.Invoke();
        
        if (ServiceProvider is ServiceProvider sp)
        {
            sp.Dispose();
        }
        
        BuildServiceProvider();
    }
}