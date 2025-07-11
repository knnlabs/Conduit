using Terminal.Gui;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// A reusable tabbed view component that supports keyboard navigation and tab switching.
/// </summary>
public class TabbedView : View
{
    private readonly List<TabInfo> _tabs = new();
    private int _selectedTabIndex = 0;
    private View _tabHeaderView = null!;
    private View _tabContentView = null!;
    
    /// <summary>
    /// Event raised when the active tab changes.
    /// </summary>
    public event Action<int, TabInfo>? TabChanged;

    /// <summary>
    /// Gets the currently selected tab index.
    /// </summary>
    public int SelectedTabIndex => _selectedTabIndex;

    /// <summary>
    /// Gets the currently selected tab.
    /// </summary>
    public TabInfo? SelectedTab => _selectedTabIndex < _tabs.Count ? _tabs[_selectedTabIndex] : null;

    /// <summary>
    /// Gets all tabs.
    /// </summary>
    public IReadOnlyList<TabInfo> Tabs => _tabs.AsReadOnly();

    public TabbedView()
    {
        InitializeUI();
        CanFocus = true;
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Tab header area (top row)
        _tabHeaderView = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1
        };

        // Tab content area (rest of the space)
        _tabContentView = new View
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1
        };

        Add(_tabHeaderView, _tabContentView);
    }

    /// <summary>
    /// Add a new tab.
    /// </summary>
    public void AddTab(string title, View content, string? shortcut = null)
    {
        var tab = new TabInfo(title, content, shortcut);
        _tabs.Add(tab);
        
        if (_tabs.Count == 1)
        {
            // First tab - activate it
            SetActiveTab(0);
        }
        
        UpdateTabHeaders();
    }

    /// <summary>
    /// Remove a tab by index.
    /// </summary>
    public bool RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return false;

        var tab = _tabs[index];
        _tabs.RemoveAt(index);

        // Remove content from content view
        if (_tabContentView.Subviews.Contains(tab.Content))
        {
            _tabContentView.Remove(tab.Content);
        }

        // Adjust selected tab index if necessary
        if (_selectedTabIndex >= _tabs.Count)
        {
            _selectedTabIndex = Math.Max(0, _tabs.Count - 1);
        }

        if (_tabs.Count > 0)
        {
            SetActiveTab(_selectedTabIndex);
        }

        UpdateTabHeaders();
        return true;
    }

    /// <summary>
    /// Set the active tab by index.
    /// </summary>
    public bool SetActiveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _selectedTabIndex)
            return false;

        // Hide current tab content
        if (_selectedTabIndex < _tabs.Count)
        {
            var currentTab = _tabs[_selectedTabIndex];
            currentTab.Content.Visible = false;
        }

        // Show new tab content
        _selectedTabIndex = index;
        var newTab = _tabs[_selectedTabIndex];
        
        // Add content to content view if not already added
        if (!_tabContentView.Subviews.Contains(newTab.Content))
        {
            _tabContentView.Add(newTab.Content);
        }
        
        newTab.Content.Visible = true;
        newTab.Content.SetFocus();

        UpdateTabHeaders();
        TabChanged?.Invoke(_selectedTabIndex, newTab);
        return true;
    }

    /// <summary>
    /// Set the active tab by title.
    /// </summary>
    public bool SetActiveTab(string title)
    {
        var index = _tabs.FindIndex(t => t.Title == title);
        return index >= 0 && SetActiveTab(index);
    }

    /// <summary>
    /// Move to the next tab.
    /// </summary>
    public void NextTab()
    {
        if (_tabs.Count > 1)
        {
            var nextIndex = (_selectedTabIndex + 1) % _tabs.Count;
            SetActiveTab(nextIndex);
        }
    }

    /// <summary>
    /// Move to the previous tab.
    /// </summary>
    public void PreviousTab()
    {
        if (_tabs.Count > 1)
        {
            var prevIndex = (_selectedTabIndex - 1 + _tabs.Count) % _tabs.Count;
            SetActiveTab(prevIndex);
        }
    }

    /// <summary>
    /// Update the visual representation of tab headers.
    /// </summary>
    private void UpdateTabHeaders()
    {
        _tabHeaderView.RemoveAll();

        if (_tabs.Count == 0)
            return;

        int x = 0;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var isSelected = i == _selectedTabIndex;
            
            // Build tab header text with optional shortcut
            var headerText = tab.Title;
            if (!string.IsNullOrEmpty(tab.Shortcut))
            {
                headerText = $"[{tab.Shortcut}] {headerText}";
            }

            var tabHeader = new Label(headerText)
            {
                X = x,
                Y = 0,
                ColorScheme = new ColorScheme
                {
                    Normal = isSelected 
                        ? Application.Driver.MakeAttribute(Color.Black, Color.White)
                        : Application.Driver.MakeAttribute(Color.White, Color.Black),
                    Focus = isSelected
                        ? Application.Driver.MakeAttribute(Color.Black, Color.White)
                        : Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black)
                }
            };

            // Create clickable button for tab switching
            var tabIndex = i; // Capture for closure
            var tabButton = new Button(headerText)
            {
                X = x,
                Y = 0,
                AutoSize = false,
                Width = headerText.Length + 2,
                Height = 1
            };
            tabButton.Clicked += () => SetActiveTab(tabIndex);

            _tabHeaderView.Add(tabButton);
            x += headerText.Length + 3; // Add spacing between tabs
        }

        // Add help text at the right
        var helpText = "Tab/Shift+Tab: Navigate | F1: Help";
        var helpLabel = new Label(helpText)
        {
            X = Pos.Right(_tabHeaderView) - helpText.Length,
            Y = 0,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };
        _tabHeaderView.Add(helpLabel);
    }

    /// <summary>
    /// Handle keyboard input for tab navigation.
    /// </summary>
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        switch (keyEvent.Key)
        {
            case Key.Tab:
                if (keyEvent.IsShift)
                    PreviousTab();
                else
                    NextTab();
                return true;

            case Key.F1:
                ShowHelp();
                return true;

            // Handle numeric keys (1-9) for direct tab selection
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
            case Key.D6:
            case Key.D7:
            case Key.D8:
            case Key.D9:
                var tabNumber = (int)keyEvent.Key - (int)Key.D1;
                if (tabNumber < _tabs.Count)
                {
                    SetActiveTab(tabNumber);
                    return true;
                }
                break;
        }

        return base.ProcessKey(keyEvent);
    }

    /// <summary>
    /// Show help dialog for tab navigation.
    /// </summary>
    private void ShowHelp()
    {
        var help = new Dialog("Tab Navigation Help", 60, 15);
        
        var helpText = @"Keyboard Shortcuts:
• Tab / Shift+Tab - Navigate between tabs
• 1-9 - Jump to specific tab by number
• F1 - Show this help
• Esc - Close current dialog

Mouse:
• Click on tab headers to switch tabs

Available Tabs:";

        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            helpText += $"\n• [{i + 1}] {tab.Title}";
            if (!string.IsNullOrEmpty(tab.Shortcut))
                helpText += $" ({tab.Shortcut})";
        }

        var textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = helpText,
            ReadOnly = true,
            WordWrap = true
        };

        var okButton = new Button("OK")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(help) - 3
        };
        okButton.Clicked += () => help.Running = false;

        help.Add(textView, okButton);
        Application.Run(help);
    }
}

/// <summary>
/// Information about a tab in the tabbed view.
/// </summary>
public class TabInfo
{
    /// <summary>
    /// Gets the tab title displayed in the header.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the content view for this tab.
    /// </summary>
    public View Content { get; }

    /// <summary>
    /// Gets the optional keyboard shortcut for this tab.
    /// </summary>
    public string? Shortcut { get; }

    public TabInfo(string title, View content, string? shortcut = null)
    {
        Title = title;
        Content = content;
        Shortcut = shortcut;
    }
}