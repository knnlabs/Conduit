using Terminal.Gui;

namespace ConduitLLM.TUI.Utils;

/// <summary>
/// Helper class for creating common dialog patterns.
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Creates a help dialog with the specified title and content.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="content">The help content to display.</param>
    /// <param name="width">The dialog width. Default is 70.</param>
    /// <param name="height">The dialog height. Default is 22.</param>
    /// <returns>A configured help dialog.</returns>
    public static Dialog CreateHelpDialog(string title, string content, int width = 70, int height = 22)
    {
        var dialog = new Dialog(title, width, height);
        
        var textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = content,
            ReadOnly = true,
            WordWrap = true
        };

        var okButton = new Button(Constants.UIConstants.ButtonLabels.OK)
        {
            X = Pos.Center(),
            Y = Pos.Bottom(dialog) - 3
        };
        okButton.Clicked += () => dialog.Running = false;

        dialog.Add(textView, okButton);
        return dialog;
    }

    /// <summary>
    /// Creates a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <param name="onYes">Action to execute when Yes is clicked.</param>
    /// <param name="onNo">Optional action to execute when No is clicked.</param>
    /// <returns>A configured confirmation dialog.</returns>
    public static Dialog CreateConfirmDialog(string title, string message, Action onYes, Action? onNo = null)
    {
        var dialog = new Dialog(title, 60, 10);
        
        var label = new Label(message)
        {
            X = Pos.Center(),
            Y = 2,
            TextAlignment = Terminal.Gui.TextAlignment.Centered,
            Width = Dim.Fill(2)
        };

        var yesButton = new Button("Yes")
        {
            X = Pos.Center() - 10,
            Y = Pos.Bottom(dialog) - 3
        };
        
        var noButton = new Button("No")
        {
            X = Pos.Center() + 5,
            Y = Pos.Bottom(dialog) - 3
        };

        yesButton.Clicked += () =>
        {
            onYes();
            dialog.Running = false;
        };

        noButton.Clicked += () =>
        {
            onNo?.Invoke();
            dialog.Running = false;
        };

        dialog.Add(label, yesButton, noButton);
        return dialog;
    }

    /// <summary>
    /// Creates an error dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="errorMessage">The error message to display.</param>
    /// <returns>A configured error dialog.</returns>
    public static Dialog CreateErrorDialog(string title, string errorMessage)
    {
        var dialog = new Dialog(title, 60, 12);
        
        var label = new Label(errorMessage)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            TextAlignment = Terminal.Gui.TextAlignment.Left
        };

        var okButton = new Button(Constants.UIConstants.ButtonLabels.OK)
        {
            X = Pos.Center(),
            Y = Pos.Bottom(dialog) - 3
        };
        okButton.Clicked += () => dialog.Running = false;

        dialog.Add(label, okButton);
        return dialog;
    }

    /// <summary>
    /// Shows a help dialog and runs it.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="content">The help content to display.</param>
    public static void ShowHelp(string title, string content)
    {
        var dialog = CreateHelpDialog(title, content);
        Application.Run(dialog);
    }

    /// <summary>
    /// Shows a confirmation dialog and runs it.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <param name="onYes">Action to execute when Yes is clicked.</param>
    /// <param name="onNo">Optional action to execute when No is clicked.</param>
    public static void ShowConfirmation(string title, string message, Action onYes, Action? onNo = null)
    {
        var dialog = CreateConfirmDialog(title, message, onYes, onNo);
        Application.Run(dialog);
    }

    /// <summary>
    /// Shows an error dialog and runs it.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="errorMessage">The error message to display.</param>
    public static void ShowError(string title, string errorMessage)
    {
        var dialog = CreateErrorDialog(title, errorMessage);
        Application.Run(dialog);
    }
}