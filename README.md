Simple.Wpf.Terminal
===================

A simple console\\terminal window for use in a WPF application, this is a user control that will allow the user to enter a line of text (script\\command) and\or display a list of items in a console\\terminal style.

This user control does NOT actually do anything more than display data - the user control is the View (UI) in a MVVM implementation, the ViewModel would be responsible for the actual behaviour & contents of the console\\terminal window. The user control is designed to use XAML binding for all UI properties the user can configure.

Examples of how this user control could be used:

1.  Custom F# Interactive window,

2.  Log viewer - see the 'Logging Workspace' example in https://github.com/oriches/Simple.Wpf.Composition

3.  Directory browser.


This control is being used as part of blog posts about a custom F# Interactive UI in WPF - currently in progress.

http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-1.html

http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-2.html
