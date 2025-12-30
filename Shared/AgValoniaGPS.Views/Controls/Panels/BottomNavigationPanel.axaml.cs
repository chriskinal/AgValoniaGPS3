using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class BottomNavigationPanel : DraggableRotatablePanel
{
    private Border? _mainPanel;
    private Border? _abLineFlyoutPanel;
    private Border? _flagsFlyoutPanel;
    private Button? _abLineMenuButton;
    private Button? _flagMenuButton;
    private bool _isABLineFlyoutOpen;
    private bool _isFlagsFlyoutOpen;
    private AgValoniaGPS.ViewModels.MainViewModel? _subscribedViewModel;

    public BottomNavigationPanel()
    {
        InitializeComponent();

        // Find controls
        _mainPanel = this.FindControl<Border>("MainPanel");
        _abLineMenuButton = this.FindControl<Button>("ABLineMenuButton");
        _flagMenuButton = this.FindControl<Button>("FlagMenuButton");
        _abLineFlyoutPanel = this.FindControl<Border>("ABLineFlyoutPanel");
        _flagsFlyoutPanel = this.FindControl<Border>("FlagsFlyoutPanel");

        // Initialize drag and rotate behavior from base class
        InitializeDragRotate();

        // Wire up menu buttons to toggle flyouts
        if (_abLineMenuButton != null)
        {
            _abLineMenuButton.Click += ABLineMenuButton_Click;
        }

        if (_flagMenuButton != null)
        {
            _flagMenuButton.Click += FlagMenuButton_Click;
        }

        // Close flyouts when clicking outside
        this.PointerPressed += OnPanelPointerPressed;

        // Subscribe to dialog changes to close flyouts when dialogs close
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old viewmodel to avoid multiple subscriptions
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.State.UI.DialogChanged -= OnDialogChanged;
            _subscribedViewModel = null;
        }

        // Subscribe to dialog state changes
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.State.UI.DialogChanged += OnDialogChanged;
            _subscribedViewModel = vm;
        }
    }

    private void OnDialogChanged(object? sender, DialogChangedEventArgs e)
    {
        // Close flyouts when any dialog closes (especially after opening from flyout)
        if (e.Current == DialogType.None)
        {
            CloseAllFlyouts();
        }
    }

    /// <summary>
    /// Override to rotate the ButtonStack orientation.
    /// </summary>
    protected override void RotatePanel()
    {
        var buttonStack = FindButtonStack();
        if (buttonStack != null)
        {
            buttonStack.Orientation = buttonStack.Orientation == Orientation.Vertical
                ? Orientation.Horizontal
                : Orientation.Vertical;
        }
    }

    private void UpdateFlyoutPositions()
    {
        if (_mainPanel == null) return;

        var panelLeft = Canvas.GetLeft(_mainPanel);
        var panelTop = Canvas.GetTop(_mainPanel);

        if (double.IsNaN(panelLeft)) panelLeft = 0;
        if (double.IsNaN(panelTop)) panelTop = -78; // Default from XAML

        // Get main panel width, use estimate if not yet measured
        var mainPanelWidth = _mainPanel.Bounds.Width > 0 ? _mainPanel.Bounds.Width : 700;

        // Position flyouts above the main panel
        // Use estimated height if bounds not yet calculated (first show)
        if (_abLineFlyoutPanel != null)
        {
            // AB flyout has ~8 buttons @ 64px + spacing + padding = ~580px estimated
            var abFlyoutHeight = _abLineFlyoutPanel.Bounds.Height > 0
                ? _abLineFlyoutPanel.Bounds.Height
                : 580;
            Canvas.SetLeft(_abLineFlyoutPanel, panelLeft + mainPanelWidth - 70);
            Canvas.SetTop(_abLineFlyoutPanel, panelTop - abFlyoutHeight - 10);
        }

        if (_flagsFlyoutPanel != null)
        {
            // Flags flyout has 4 buttons @ 64px + spacing + padding = ~320px estimated
            var flagsFlyoutHeight = _flagsFlyoutPanel.Bounds.Height > 0
                ? _flagsFlyoutPanel.Bounds.Height
                : 320;
            Canvas.SetLeft(_flagsFlyoutPanel, panelLeft + mainPanelWidth - 130);
            Canvas.SetTop(_flagsFlyoutPanel, panelTop - flagsFlyoutHeight - 10);
        }
    }

    private void ABLineMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        // Close flags flyout if open
        CloseFlagsFlyout();
        ToggleABLineFlyout();
        e.Handled = true;
    }

    private void FlagMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        // Close AB line flyout if open
        CloseABLineFlyout();
        ToggleFlagsFlyout();
        e.Handled = true;
    }

    private void ToggleABLineFlyout()
    {
        _isABLineFlyoutOpen = !_isABLineFlyoutOpen;
        if (_abLineFlyoutPanel != null)
        {
            if (_isABLineFlyoutOpen)
            {
                // Height depends on HasActiveTrack - check DataContext
                // 3 buttons (64px each) + spacing (6px * 2) + padding (16px) = ~220px without active track
                // 7 items + 2 separators + nudge rows when active = ~580px with active track
                double estimatedHeight = 230; // Default: no active track (3 buttons)

                if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm && vm.HasActiveTrack)
                {
                    estimatedHeight = 580; // Full menu with active track
                }

                PositionFlyoutAboveButton(_abLineFlyoutPanel, _abLineMenuButton, estimatedHeight);
            }
            _abLineFlyoutPanel.IsVisible = _isABLineFlyoutOpen;
        }
    }

    private void ToggleFlagsFlyout()
    {
        _isFlagsFlyoutOpen = !_isFlagsFlyoutOpen;
        if (_flagsFlyoutPanel != null)
        {
            if (_isFlagsFlyoutOpen)
            {
                // Position relative to the button
                PositionFlyoutAboveButton(_flagsFlyoutPanel, _flagMenuButton, 320);
            }
            _flagsFlyoutPanel.IsVisible = _isFlagsFlyoutOpen;
        }
    }

    private void PositionFlyoutAboveButton(Border flyout, Button? button, double estimatedHeight)
    {
        if (button == null || _mainPanel == null) return;

        // Get the button's position relative to the main panel
        var buttonBounds = button.Bounds;
        var buttonPosition = button.TranslatePoint(new Point(0, 0), _mainPanel);

        if (buttonPosition.HasValue)
        {
            // Get the main panel's canvas position
            var panelLeft = Canvas.GetLeft(_mainPanel);
            var panelTop = Canvas.GetTop(_mainPanel);
            if (double.IsNaN(panelLeft)) panelLeft = 0;
            if (double.IsNaN(panelTop)) panelTop = -78;

            // Always use estimated height for consistent first-open positioning
            // The panel renders the same every time, so this is reliable
            var flyoutHeight = estimatedHeight;

            // Calculate X position - align flyout's right edge roughly with button
            var flyoutWidth = 80.0; // Single column of 64px buttons + padding
            var flyoutLeft = panelLeft + buttonPosition.Value.X + buttonBounds.Width - flyoutWidth;

            // Position flyout above the main panel
            Canvas.SetLeft(flyout, flyoutLeft);
            Canvas.SetTop(flyout, panelTop - flyoutHeight - 10);
        }
    }

    private void CloseABLineFlyout()
    {
        _isABLineFlyoutOpen = false;
        if (_abLineFlyoutPanel != null)
        {
            _abLineFlyoutPanel.IsVisible = false;
        }
    }

    private void CloseFlagsFlyout()
    {
        _isFlagsFlyoutOpen = false;
        if (_flagsFlyoutPanel != null)
        {
            _flagsFlyoutPanel.IsVisible = false;
        }
    }

    private void CloseAllFlyouts()
    {
        CloseABLineFlyout();
        CloseFlagsFlyout();
    }

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // If any flyout is open and user clicked outside of it, close it
        if (_isABLineFlyoutOpen && _abLineFlyoutPanel != null)
        {
            var position = e.GetPosition(_abLineFlyoutPanel);
            var bounds = _abLineFlyoutPanel.Bounds;

            // Check if click is outside the flyout panel bounds
            if (position.X < 0 || position.Y < 0 ||
                position.X > bounds.Width || position.Y > bounds.Height)
            {
                // But don't close if they clicked the menu button
                if (_abLineMenuButton != null)
                {
                    var menuPos = e.GetPosition(_abLineMenuButton);
                    var menuBounds = _abLineMenuButton.Bounds;
                    if (menuPos.X >= 0 && menuPos.Y >= 0 &&
                        menuPos.X <= menuBounds.Width && menuPos.Y <= menuBounds.Height)
                    {
                        // Clicked menu button, let the Click handler deal with it
                        return;
                    }
                }

                CloseABLineFlyout();
            }
        }

        if (_isFlagsFlyoutOpen && _flagsFlyoutPanel != null)
        {
            var position = e.GetPosition(_flagsFlyoutPanel);
            var bounds = _flagsFlyoutPanel.Bounds;

            // Check if click is outside the flyout panel bounds
            if (position.X < 0 || position.Y < 0 ||
                position.X > bounds.Width || position.Y > bounds.Height)
            {
                // But don't close if they clicked the menu button
                if (_flagMenuButton != null)
                {
                    var menuPos = e.GetPosition(_flagMenuButton);
                    var menuBounds = _flagMenuButton.Bounds;
                    if (menuPos.X >= 0 && menuPos.Y >= 0 &&
                        menuPos.X <= menuBounds.Width && menuPos.Y <= menuBounds.Height)
                    {
                        // Clicked menu button, let the Click handler deal with it
                        return;
                    }
                }

                CloseFlagsFlyout();
            }
        }
    }

    /// <summary>
    /// Close all flyouts when any action button is clicked.
    /// Call this from flyout button click handlers if needed.
    /// </summary>
    public void CloseFlyoutOnAction()
    {
        CloseAllFlyouts();
    }
}
