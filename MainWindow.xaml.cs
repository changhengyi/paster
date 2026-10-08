using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Paster;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1001;
    private const int WmHotkey = 0x0312;
    private const uint WmChar = 0x0102;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;
    private const int VkMenu = 0x12;
    private const int VkInsert = 0x2D;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;
    private const int ClassNameBufferLength = 256;

    private ClipStorage _storage;
    private readonly Forms.NotifyIcon _trayIcon = new();

    private bool _isExiting;
    private bool _suppressHotkeyOnce;
    private IntPtr _lastForegroundWindow = IntPtr.Zero;
    private IntPtr _lastFocusedControl = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private int _selectedPageIndex;
    private ClipItem? _dragItem;
    private System.Windows.Point _dragStartPoint;
    private System.Windows.Controls.ListViewItem? _dropTargetItem;
    private bool _suppressCopyOnce;

    public ObservableCollection<ClipPage> Pages { get; private set; } = new();

    public ObservableCollection<ClipItem> ClipItems { get; private set; } = new();

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set
        {
            if (_selectedPageIndex == value)
            {
                return;
            }

            _selectedPageIndex = value;
            UpdateCurrentPageItems();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        _storage = new ClipStorage(AppSettingsStore.ResolveClipsJsonPath());
        Pages = _storage.Load();
        SelectedPageIndex = Pages.Count > 0 ? 0 : -1;
        DataContext = this;
        RefreshPageTabs();

        SetupTrayIcon();
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        if (Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            Loaded += (_, _) => Hide();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        var registered = RegisterHotKey(handle, HotkeyId, ModControl | ModShift, VkV);
        if (!registered)
        {
            System.Windows.MessageBox.Show("全局快捷键 Ctrl+Shift+V 注册失败，请检查是否被占用。", "提示");
        }
    }

    private void SetupTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());

        _trayIcon.Text = "Paster";
        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        _trayIcon.Visible = true;
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_storage.ClipsJsonPath)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrEmpty(dialog.AcceptedClipsJsonPath))
        {
            return;
        }

        _storage = new ClipStorage(dialog.AcceptedClipsJsonPath);
        ReloadClipItemsFromStorage();
        e.Handled = true;
    }

    private void ReloadClipItemsFromStorage()
    {
        var previousIndex = _selectedPageIndex;
        Pages = _storage.Load();
        SelectedPageIndex = Pages.Count == 0
            ? -1
            : Math.Min(Math.Max(previousIndex, 0), Pages.Count - 1);
        RefreshPageTabs();
    }

    private void UpdateCurrentPageItems()
    {
        ClipItems = _selectedPageIndex >= 0 && _selectedPageIndex < Pages.Count
            ? Pages[_selectedPageIndex].Items
            : new ObservableCollection<ClipItem>();
        ClipListView.ItemsSource = ClipItems;

        if (PageTabsControl.SelectedIndex != _selectedPageIndex)
        {
            PageTabsControl.SelectedIndex = _selectedPageIndex;
        }
    }

    private ClipPage EnsureCurrentPage()
    {
        if (_selectedPageIndex >= 0 && _selectedPageIndex < Pages.Count)
        {
            return Pages[_selectedPageIndex];
        }

        var page = new ClipPage { Name = "常用" };
        Pages.Add(page);
        SelectedPageIndex = Pages.Count - 1;
        RefreshPageTabs();
        return page;
    }

    private void RefreshPageTabs()
    {
        PageTabsControl.ItemsSource = Pages;
        PageTabsControl.SelectedIndex = _selectedPageIndex;
    }

    private void PageTabsControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PageTabsControl.SelectedIndex == _selectedPageIndex)
        {
            return;
        }

        SelectedPageIndex = PageTabsControl.SelectedIndex;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            if (_suppressHotkeyOnce)
            {
                _suppressHotkeyOnce = false;
                handled = true;
                return IntPtr.Zero;
            }

            _lastForegroundWindow = GetForegroundWindow();
            _lastFocusedControl = GetFocusedControlHandle(_lastForegroundWindow);
            ShowMainWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ClipEditWindow
        {
            Owner = this,
            DialogTitle = "新增内容",
            TitleText = string.Empty,
            ContentText = string.Empty
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(dialog.TitleText)
            ? (dialog.ContentText.Length > 12 ? dialog.ContentText[..12] + "..." : dialog.ContentText)
            : dialog.TitleText.Trim();

        EnsureCurrentPage().Items.Add(new ClipItem
        {
            Title = title,
            Content = dialog.ContentText.Trim()
        });

        Persist();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ClipItem item)
        {
            return;
        }

        ClipItems.Remove(item);
        Persist();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ClipItem item)
        {
            return;
        }

        var dialog = new ClipEditWindow
        {
            Owner = this,
            DialogTitle = "编辑内容",
            TitleText = item.Title,
            ContentText = item.Content
        };
        if (dialog.ShowDialog() != true)
        {
            e.Handled = true;
            return;
        }

        var title = string.IsNullOrWhiteSpace(dialog.TitleText)
            ? (dialog.ContentText.Length > 12 ? dialog.ContentText[..12] + "..." : dialog.ContentText)
            : dialog.TitleText.Trim();

        item.Title = title;
        item.Content = dialog.ContentText.Trim();
        ClipListView.Items.Refresh();
        Persist();

        e.Handled = true;
    }

    private void ClipItemBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressCopyOnce)
        {
            _suppressCopyOnce = false;
            return;
        }

        if (IsClickInsideButton(e.OriginalSource as DependencyObject)
            || IsClickInsideDragHandle(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not ClipItem item)
        {
            return;
        }

        System.Windows.Clipboard.SetText(item.Content);
        Hide();
    }

    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ClipItem item)
        {
            return;
        }

        _dragItem = item;
        _dragStartPoint = e.GetPosition(null);
        e.Handled = true;
    }

    private void ClipListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        var delta = _dragStartPoint - position;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragItem;
        _suppressCopyOnce = true;
        System.Windows.DragDrop.DoDragDrop(ClipListView, item, System.Windows.DragDropEffects.Move);
        ClearDropIndicator();
        _dragItem = null;
    }

    private void ClipListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragItem = null;
    }

    private void ClipListView_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ClipItem)))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        UpdateDropIndicator(e.GetPosition(ClipListView));
        e.Handled = true;
    }

    private void ClipListView_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ClipItem)) is not ClipItem item)
        {
            ClearDropIndicator();
            return;
        }

        var from = ClipItems.IndexOf(item);
        if (from < 0)
        {
            ClearDropIndicator();
            return;
        }

        var insertIndex = GetInsertIndex(e.GetPosition(ClipListView));
        if (from < insertIndex)
        {
            insertIndex--;
        }

        if (from != insertIndex)
        {
            ClipItems.Move(from, insertIndex);
            Persist();
        }

        ClearDropIndicator();
        _dragItem = null;
        e.Handled = true;
    }

    private void ClipListView_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        var position = e.GetPosition(ClipListView);
        if (position.X < 0
            || position.Y < 0
            || position.X > ClipListView.ActualWidth
            || position.Y > ClipListView.ActualHeight)
        {
            ClearDropIndicator();
        }
    }

    private int GetInsertIndex(System.Windows.Point positionInList)
    {
        var hit = System.Windows.Media.VisualTreeHelper.HitTest(ClipListView, positionInList);
        var listViewItem = FindAncestor<System.Windows.Controls.ListViewItem>(hit?.VisualHit);
        if (listViewItem is null)
        {
            return ClipItems.Count;
        }

        var itemIndex = ClipListView.ItemContainerGenerator.IndexFromContainer(listViewItem);
        if (itemIndex < 0)
        {
            return ClipItems.Count;
        }

        var relativeY = ClipListView.TranslatePoint(positionInList, listViewItem).Y;
        return relativeY > listViewItem.ActualHeight / 2 ? itemIndex + 1 : itemIndex;
    }

    private void UpdateDropIndicator(System.Windows.Point positionInList)
    {
        var hit = System.Windows.Media.VisualTreeHelper.HitTest(ClipListView, positionInList);
        var listViewItem = FindAncestor<System.Windows.Controls.ListViewItem>(hit?.VisualHit);
        if (listViewItem is null)
        {
            ClearDropIndicator();
            return;
        }

        var insertAfter = ClipListView.TranslatePoint(positionInList, listViewItem).Y
            > listViewItem.ActualHeight / 2;
        var desiredThickness = insertAfter
            ? new Thickness(0, 0, 0, 2)
            : new Thickness(0, 2, 0, 0);

        if (ReferenceEquals(_dropTargetItem, listViewItem)
            && _dropTargetItem.BorderThickness == desiredThickness)
        {
            return;
        }

        ClearDropIndicator();
        _dropTargetItem = listViewItem;
        _dropTargetItem.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        _dropTargetItem.BorderThickness = desiredThickness;
    }

    private void ClearDropIndicator()
    {
        if (_dropTargetItem is null)
        {
            return;
        }

        _dropTargetItem.BorderBrush = System.Windows.Media.Brushes.Transparent;
        _dropTargetItem.BorderThickness = new Thickness(0);
        _dropTargetItem = null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static bool IsClickInsideButton(DependencyObject? source)
    {
        return FindAncestor<System.Windows.Controls.Button>(source) is not null;
    }

    private static bool IsClickInsideDragHandle(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Name: "DragHandle" })
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void Persist()
    {
        _storage.Save(Pages);
    }

    private static bool SendTextByInput(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        var inputs = new List<Input>(content.Length * 2);
        foreach (var ch in content)
        {
            inputs.Add(Input.UnicodeKeyDown(ch));
            inputs.Add(Input.UnicodeKeyUp(ch));
        }

        var sentCount = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        return sentCount == inputs.Count;
    }

    private static void SendTextByPostMessage(IntPtr targetControl, string content)
    {
        if (targetControl == IntPtr.Zero || string.IsNullOrEmpty(content))
        {
            return;
        }

        foreach (var ch in content)
        {
            PostMessage(targetControl, WmChar, (IntPtr)ch, IntPtr.Zero);
        }
    }

    private static IntPtr GetFocusedControlHandle(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var targetThreadId = GetWindowThreadProcessId(targetWindow, IntPtr.Zero);
        if (targetThreadId == 0)
        {
            return IntPtr.Zero;
        }

        var threadInfo = new GuiThreadInfo
        {
            cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>()
        };

        if (GetGUIThreadInfo(targetThreadId, ref threadInfo))
        {
            return threadInfo.hwndFocus;
        }

        return IntPtr.Zero;
    }

    private static bool IsTerminalWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return false;
        }

        var className = GetWindowClassName(targetWindow);
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        return className.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase)
            || className.Contains("CASCADIA", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PasteToTerminalCompatAsync(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        System.Windows.IDataObject? backupClipboard = null;
        try
        {
            if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Text)
                || System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.UnicodeText))
            {
                backupClipboard = System.Windows.Clipboard.GetDataObject();
            }
        }
        catch
        {
            backupClipboard = null;
        }

        try
        {
            System.Windows.Clipboard.SetText(content);
            await Task.Delay(120);

            // Prefer native terminal paste shortcut. Suppress our own global hotkey once.
            _suppressHotkeyOnce = true;
            SendTerminalNativePaste();

            // Fallback path: some terminals only accept Shift+Insert.
            await Task.Delay(50);
            SendShiftInsertPaste();
        }
        finally
        {
            await Task.Delay(160);
            try
            {
                if (backupClipboard is not null)
                {
                    System.Windows.Clipboard.SetDataObject((object)backupClipboard, true);
                }
            }
            catch
            {
                // Ignore clipboard restore failure to keep paste flow uninterrupted.
            }
        }
    }

    private static void SendTerminalNativePaste()
    {
        var inputs = new[]
        {
            Input.VirtualKeyDown((ushort)VkControl),
            Input.VirtualKeyDown((ushort)VkShift),
            Input.VirtualKeyDown((ushort)VkV),
            Input.VirtualKeyUp((ushort)VkV),
            Input.VirtualKeyUp((ushort)VkShift),
            Input.VirtualKeyUp((ushort)VkControl)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendShiftInsertPaste()
    {
        var inputs = new[]
        {
            Input.VirtualKeyDown((ushort)VkShift),
            Input.VirtualKeyDown((ushort)VkInsert),
            Input.VirtualKeyUp((ushort)VkInsert),
            Input.VirtualKeyUp((ushort)VkShift)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static string GetWindowClassName(IntPtr windowHandle)
    {
        var buffer = new StringBuilder(ClassNameBufferLength);
        var len = GetClassName(windowHandle, buffer, buffer.Capacity);
        return len > 0 ? buffer.ToString() : string.Empty;
    }

    private static async Task WaitForModifierKeysReleasedAsync()
    {
        for (var i = 0; i < 25; i++)
        {
            if (!IsModifierKeyDown(VkControl) && !IsModifierKeyDown(VkShift) && !IsModifierKeyDown(VkMenu))
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private static bool IsModifierKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private static void ReleaseModifierKeys()
    {
        var inputs = new[]
        {
            Input.VirtualKeyUp((ushort)VkControl),
            Input.VirtualKeyUp((ushort)VkShift),
            Input.VirtualKeyUp((ushort)VkMenu)
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo lpgui);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    private static void FocusTargetWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return;
        }

        var foreground = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(targetWindow, IntPtr.Zero);
        var foregroundThreadId = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, IntPtr.Zero);

        var attachedToForeground = false;
        var attachedToTarget = false;

        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            // Do not restore or resize target window to avoid breaking fullscreen mode.
            SetForegroundWindow(targetWindow);
        }
        finally
        {
            if (attachedToForeground)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (attachedToTarget)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion U;

        public static Input UnicodeKeyDown(char ch)
        {
            return new Input
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KeyBdInput
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KeyeventfUnicode
                    }
                }
            };
        }

        public static Input UnicodeKeyUp(char ch)
        {
            return new Input
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KeyBdInput
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KeyeventfUnicode | KeyeventfKeyup
                    }
                }
            };
        }

        public static Input VirtualKeyUp(ushort keyCode)
        {
            return new Input
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KeyBdInput
                    {
                        wVk = keyCode,
                        wScan = 0,
                        dwFlags = KeyeventfKeyup
                    }
                }
            };
        }

        public static Input VirtualKeyDown(ushort keyCode)
        {
            return new Input
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KeyBdInput
                    {
                        wVk = keyCode,
                        wScan = 0,
                        dwFlags = 0
                    }
                }
            };
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyBdInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyBdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public Rect rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
