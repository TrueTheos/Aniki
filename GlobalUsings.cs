global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Aniki.Models;
global using Aniki.Models.MAL;
global using Aniki.ViewModels;
global using Aniki.Services;
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using Serilog;
global using Aniki.Misc;

global using SeasonCache = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, Aniki.Services.SeasonData>>;
