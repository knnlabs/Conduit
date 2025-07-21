using System;
using System.Linq;
using Terminal.Gui;
using ConduitLLM.TUI.Utils;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Views
{
    /// <summary>
    /// A scrollable log view that displays log entries from the TUI logger
    /// </summary>
    public class LogView : View
    {
        private readonly LogBuffer _logBuffer;
        private readonly ListView _listView;
        private readonly List<string> _logLines;
        private readonly List<LogEntry> _logEntries;
        private readonly object _lockObject = new();
        private bool _autoScroll = true;
        private LogLevel _filterLevel = LogLevel.Trace;
        private DateTime _lastClickTime = DateTime.MinValue;
        private int _lastClickedItem = -1;

        public LogView(LogBuffer logBuffer)
        {
            _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
            _logLines = new List<string>();
            _logEntries = new List<LogEntry>();

            // Create the list view
            _listView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            
            // Add mouse click handler
            _listView.MouseClick += OnMouseClick;

            // Configure list view colors
            _listView.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black),
                Focus = Application.Driver.MakeAttribute(Color.White, Color.DarkGray),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.DarkGray)
            };

            Add(_listView);

            // Subscribe to log events
            _logBuffer.LogAdded += OnLogAdded;

            // Load existing logs
            RefreshLogs();
        }

        private void OnLogAdded(object? sender, LogEntry entry)
        {
            if (entry.Level < _filterLevel)
                return;

            Application.MainLoop?.Invoke(() =>
            {
                lock (_lockObject)
                {
                    AddLogEntry(entry);
                    UpdateListView();
                    
                    if (_autoScroll)
                    {
                        ScrollToBottom();
                    }
                }
            });
        }

        private void AddLogEntry(LogEntry entry)
        {
            var color = GetColorForLevel(entry.Level);
            var formattedMessage = entry.GetShortMessage();
            
            // Store the formatted message and the full entry
            _logLines.Add(formattedMessage);
            _logEntries.Add(entry);
            
            // Limit the number of lines
            while (_logLines.Count > 1000)
            {
                _logLines.RemoveAt(0);
                _logEntries.RemoveAt(0);
            }
        }

        private void RefreshLogs()
        {
            lock (_lockObject)
            {
                _logLines.Clear();
                _logEntries.Clear();
                
                foreach (var entry in _logBuffer.GetEntries().Where(e => e.Level >= _filterLevel))
                {
                    AddLogEntry(entry);
                }
                
                UpdateListView();
            }
        }

        private void UpdateListView()
        {
            _listView.SetSource(_logLines);
        }

        private void ScrollToBottom()
        {
            if (_logLines.Count > 0)
            {
                _listView.SelectedItem = _logLines.Count - 1;
                _listView.EnsureSelectedItemVisible();
            }
        }

        private Color GetColorForLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => Color.DarkGray,
                LogLevel.Debug => Color.Gray,
                LogLevel.Information => Color.White,
                LogLevel.Warning => Color.BrightYellow,
                LogLevel.Error => Color.BrightRed,
                LogLevel.Critical => Color.BrightMagenta,
                _ => Color.White
            };
        }

        public void SetFilterLevel(LogLevel level)
        {
            _filterLevel = level;
            RefreshLogs();
        }

        public void ToggleAutoScroll()
        {
            _autoScroll = !_autoScroll;
        }

        public new void Clear()
        {
            lock (_lockObject)
            {
                _logLines.Clear();
                _logEntries.Clear();
                UpdateListView();
            }
        }
        
        private new void OnMouseClick(MouseEventArgs args)
        {
            if (args.MouseEvent.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                var clickedItem = _listView.SelectedItem;
                var currentTime = DateTime.Now;
                
                // Check for double-click (within 500ms)
                if (clickedItem == _lastClickedItem && 
                    (currentTime - _lastClickTime).TotalMilliseconds < 500)
                {
                    // Double-click detected - copy to clipboard
                    CopySelectedLineToClipboard();
                    _lastClickedItem = -1; // Reset to prevent triple-click
                }
                else
                {
                    _lastClickedItem = clickedItem;
                    _lastClickTime = currentTime;
                }
            }
        }
        
        private void CopySelectedLineToClipboard()
        {
            lock (_lockObject)
            {
                var selectedIndex = _listView.SelectedItem;
                if (selectedIndex >= 0 && selectedIndex < _logEntries.Count)
                {
                    var entry = _logEntries[selectedIndex];
                    var fullMessage = entry.GetFormattedMessage();
                    
                    try
                    {
                        // Terminal.Gui provides clipboard access through Clipboard class
                        Clipboard.Contents = fullMessage;
                        
                        // Show a brief notification in the frame title
                        var parent = SuperView as FrameView;
                        if (parent != null)
                        {
                            var originalTitle = parent.Title;
                            parent.Title = "Logs (Copied to clipboard!)";
                            
                            // Reset title after a short delay
                            Application.MainLoop?.AddTimeout(TimeSpan.FromSeconds(2), (_) =>
                            {
                                parent.Title = originalTitle;
                                return false;
                            });
                        }
                    }
                    catch
                    {
                        // Clipboard access might fail in some environments
                        // Silently ignore
                    }
                }
            }
        }

        public override bool ProcessKey(KeyEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case Key.Home:
                    _listView.SelectedItem = 0;
                    _listView.EnsureSelectedItemVisible();
                    return true;
                    
                case Key.End:
                    ScrollToBottom();
                    return true;
                    
                case Key.PageUp:
                    var pageUpItem = Math.Max(0, _listView.SelectedItem - (_listView.Frame.Height - 1));
                    _listView.SelectedItem = pageUpItem;
                    _listView.EnsureSelectedItemVisible();
                    return true;
                    
                case Key.PageDown:
                    var pageDownItem = Math.Min(_logLines.Count - 1, _listView.SelectedItem + (_listView.Frame.Height - 1));
                    _listView.SelectedItem = pageDownItem;
                    _listView.EnsureSelectedItemVisible();
                    return true;
                    
                case Key.Enter:
                    CopySelectedLineToClipboard();
                    return true;
                    
                case Key.A | Key.CtrlMask:
                    ToggleAutoScroll();
                    return true;
                    
                case Key.C | Key.CtrlMask:
                    Clear();
                    return true;
            }

            return base.ProcessKey(keyEvent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _logBuffer.LogAdded -= OnLogAdded;
            }
            base.Dispose(disposing);
        }
    }
}