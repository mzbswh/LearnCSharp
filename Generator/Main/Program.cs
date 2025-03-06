// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using GeneratedNamespace;

var ret = Debugger.Launch();
Console.WriteLine($"Debugger.Launch() returned {ret} {Debugger.IsAttached}");

// while (!System.Diagnostics.Debugger.IsAttached)
//     System.Threading.Thread.Sleep(500);

Console.WriteLine("Hello, World!");
new TextClass().TextMethod();

var userClass = new UserClass();
userClass.UserMethod();
userClass.PropertyChanged += (sender, args) => Console.WriteLine($"Property changed: {args.PropertyName}");

userClass.BoolProp = true;
userClass.Count = 42;
