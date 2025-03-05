// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using GeneratedNamespace;

var ret = Debugger.Launch();
Console.WriteLine($"Debugger.Launch() returned {ret} {Debugger.IsAttached}");

// while (!System.Diagnostics.Debugger.IsAttached)
//     System.Threading.Thread.Sleep(500);

Console.WriteLine("Hello, World!");
new UserClass().UserMethod();
new TextClass().TextMethod();