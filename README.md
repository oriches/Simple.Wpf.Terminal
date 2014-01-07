Simple.Wpf.Terminal
===================

A simple console\\terminal window for use in a WPF application, this is a user control that will allow the user to enter a line of text (script\\command) and\or display a list of items in a console\\terminal style.

This user control does NOT actually do anything more than display data - the user control is the View (UI) in a MVVM implementation, the ViewModel would be responsible for the actual behaviour & contents of the console\\terminal window. The user control is designed to use XAML binding for all UI properties the user can configure.

The control supports theming and there are a couple of example themes supplied in the Simple.Wpf.Terminal.Themes project.

Currently we support the following .Net versions:

Supported versions:

	.NET framework 4.0 and higher,
	
This library is available as a nuget [package] (https://www.nuget.org/packages/Simple.Wpf.Terminal/).

Examples of usage:

### Custom F# Interactive window in a WPF application
![alt text](https://raw.github.com/oriches/Simple.Wpf.Terminal/master/Readme%20Images/fsharp_repl.png "F# Interactive window")

The code for this is available here - https://github.com/oriches/Simple.Wpf.FSharp.Repl

### In-app Log viewer in a WPF application
![alt text](https://raw.github.com/oriches/Simple.Wpf.Terminal/master/Readme%20Images/log_window.png "Log window")

The code for this is available here - https://github.com/oriches/Simple.Wpf.Composition


I'd be interested to hear about other uses :)

