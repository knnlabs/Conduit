using Terminal.Gui;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Utils;

/// <summary>
/// Helper class for common UI patterns and operations.
/// </summary>
public static class UIHelper
{
    /// <summary>
    /// Updates status label with thread-safe UI invocation.
    /// </summary>
    /// <param name="statusLabel">The status label to update.</param>
    /// <param name="message">The status message.</param>
    public static void UpdateStatus(Label statusLabel, string message)
    {
        Application.MainLoop.Invoke(() =>
        {
            statusLabel.Text = message;
            Application.Refresh();
        });
    }

    /// <summary>
    /// Handles common error logging and status update pattern.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="statusLabel">The status label to update.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="operation">The operation that failed.</param>
    public static void HandleError<T>(ILogger<T> logger, Label statusLabel, Exception exception, string operation)
    {
        logger.LogError(exception, Constants.UIConstants.ErrorMessages.OperationFailed, operation);
        UpdateStatus(statusLabel, string.Format(Constants.UIConstants.ErrorMessages.ErrorFormat, exception.Message));
    }

    /// <summary>
    /// Creates a standard button panel with common buttons.
    /// </summary>
    /// <param name="includeAdd">Whether to include an Add button.</param>
    /// <param name="includeEdit">Whether to include an Edit button.</param>
    /// <param name="includeDelete">Whether to include a Delete button.</param>
    /// <param name="includeRefresh">Whether to include a Refresh button.</param>
    /// <returns>A configured frame view with buttons.</returns>
    public static FrameView CreateButtonPanel(bool includeAdd = true, bool includeEdit = true, 
        bool includeDelete = true, bool includeRefresh = true)
    {
        var buttonPanel = new FrameView()
        {
            X = 0,
            Y = Pos.Bottom(null!) - 4,
            Width = Dim.Fill(),
            Height = 3,
            Border = null!
        };

        var buttons = new List<Button>();
        int xPos = 0;

        if (includeAdd)
        {
            buttons.Add(new Button(Constants.UIConstants.ButtonLabels.Add) { X = xPos });
            xPos += 10;
        }

        if (includeEdit)
        {
            buttons.Add(new Button(Constants.UIConstants.ButtonLabels.Edit) { X = xPos });
            xPos += 10;
        }

        if (includeDelete)
        {
            buttons.Add(new Button(Constants.UIConstants.ButtonLabels.Delete) { X = xPos });
            xPos += 12;
        }

        if (includeRefresh)
        {
            buttons.Add(new Button(Constants.UIConstants.ButtonLabels.Refresh) { X = xPos });
        }

        buttonPanel.Add(buttons.ToArray());
        return buttonPanel;
    }

    /// <summary>
    /// Creates a standard list view with frame.
    /// </summary>
    /// <param name="title">The frame title.</param>
    /// <returns>A tuple containing the frame and list view.</returns>
    public static (FrameView frame, ListView listView) CreateListViewWithFrame(string title)
    {
        var frame = new FrameView(title)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 8
        };

        var listView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        frame.Add(listView);
        return (frame, listView);
    }

    /// <summary>
    /// Creates a standard status bar.
    /// </summary>
    /// <param name="initialStatus">The initial status text.</param>
    /// <returns>A configured status bar.</returns>
    public static StatusBar CreateStatusBar(string initialStatus = "Ready")
    {
        return new StatusBar(new StatusItem[]
        {
            new StatusItem(Application.QuitKey, "~^Q~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.Null, initialStatus, null!)
        });
    }

    /// <summary>
    /// Adds a label and text field combination to a view.
    /// </summary>
    /// <param name="parent">The parent view.</param>
    /// <param name="label">The label text.</param>
    /// <param name="x">The X position.</param>
    /// <param name="y">The Y position.</param>
    /// <param name="fieldWidth">The field width.</param>
    /// <param name="defaultValue">The default field value.</param>
    /// <returns>The created text field.</returns>
    public static TextField AddLabelAndField(View parent, string label, int x, int y, 
        int fieldWidth = 40, string defaultValue = "")
    {
        var labelView = new Label(label)
        {
            X = x,
            Y = y
        };

        var field = new TextField(defaultValue)
        {
            X = x + label.Length + 1,
            Y = y,
            Width = fieldWidth
        };

        parent.Add(labelView, field);
        return field;
    }

    /// <summary>
    /// Gets color scheme for connection status.
    /// </summary>
    /// <param name="isConnected">Whether the service is connected.</param>
    /// <returns>The appropriate color scheme.</returns>
    public static ColorScheme GetConnectionColorScheme(bool isConnected)
    {
        return new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(
                isConnected ? Color.Green : Color.Red,
                Color.Black)
        };
    }

    /// <summary>
    /// Gets color scheme for task status.
    /// </summary>
    /// <param name="status">The task status.</param>
    /// <returns>The appropriate color scheme.</returns>
    public static ColorScheme GetTaskStatusColorScheme(Constants.TaskStatus status)
    {
        Color foreground = status switch
        {
            Constants.TaskStatus.Completed => Color.Green,
            Constants.TaskStatus.Failed => Color.Red,
            Constants.TaskStatus.InProgress => Color.BrightYellow,
            _ => Color.Gray
        };

        return new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(foreground, Color.Black)
        };
    }
}