using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ResourceHackerAITranslator
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string configPath = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translator.config.json");
            Application.Run(new MainForm(AppConfig.Load(configPath), configPath));
        }
    }

    internal sealed class MainForm : Form
    {
        private AppConfig config;
        private readonly string configPath;
        private readonly NotifyIcon icon;
        private readonly HotKeyWindow translateAllHotkey;
        private readonly HotKeyWindow translateSelectionHotkey;
        private readonly FloatingToolbar floatingToolbar;
        private readonly System.Windows.Forms.Timer followTimer;
        private readonly ComboBox providerBox;
        private readonly ComboBox targetLanguageBox;
        private readonly TextBox endpointBox;
        private readonly ComboBox modelBox;
        private readonly TextBox apiKeyBox;
        private readonly TextBox appKeyBox;
        private readonly TextBox appSecretBox;
        private readonly TextBox regionBox;
        private readonly ComboBox sourceBox;
        private readonly ComboBox targetBox;
        private readonly TextBox sourceLineNumberBox;
        private readonly TextBox resultLineNumberBox;
        private readonly TextBox sourcePreviewBox;
        private readonly TextBox resultPreviewBox;
        private readonly TextBox logBox;
        private readonly Button translateAllButton;
        private readonly Button translateSelectionButton;
        private readonly Button saveButton;
        private readonly Button testButton;
        private readonly Button pasteResultButton;
        private readonly Button undoPasteButton;
        private readonly Button toggleFloatButton;
        private IntPtr resourceHackerWindow = IntPtr.Zero;
        private bool floatingEnabled = true;
        private bool lastCopyWasFullEditor = true;
        private string lastBeforePasteText;
        private bool lastUndoUsesEditorUndo;
        private bool busy;

        private sealed class MainWindowSnapshot
        {
            public bool Visible;
            public FormWindowState WindowState;
            public System.Drawing.Rectangle Bounds;
        }

        public MainForm(AppConfig config, string configPath)
        {
            this.config = config;
            this.configPath = configPath;

            Text = "Resource Hacker AI \u7ffb\u8bd1\u5668";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 820;
            Height = 760;
            MinimumSize = new System.Drawing.Size(760, 680);

            translateAllHotkey = new HotKeyWindow(1001, HotKeyWindow.MOD_CONTROL | HotKeyWindow.MOD_ALT, (uint)Keys.T);
            translateSelectionHotkey = new HotKeyWindow(1002, HotKeyWindow.MOD_CONTROL | HotKeyWindow.MOD_ALT | HotKeyWindow.MOD_SHIFT, (uint)Keys.T);
            translateAllHotkey.HotKeyPressed += delegate { TranslateFocusedEditor(false); };
            translateSelectionHotkey.HotKeyPressed += delegate { TranslateFocusedEditor(true); };

            icon = new NotifyIcon();
            icon.Text = "Resource Hacker AI \u7ffb\u8bd1\u5668";
            icon.Icon = System.Drawing.SystemIcons.Application;
            icon.Visible = true;
            icon.DoubleClick += delegate { ShowMainWindow(); };
            icon.ContextMenuStrip = BuildMenu();

            floatingToolbar = new FloatingToolbar(this);
            followTimer = new System.Windows.Forms.Timer();
            followTimer.Interval = 500;
            followTimer.Tick += delegate { UpdateFloatingToolbar(); };
            followTimer.Start();

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 264));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new Label();
            header.Dock = DockStyle.Fill;
            header.Text = "\u9009\u62e9\u7ffb\u8bd1\u63a5\u53e3\uff0c\u7136\u540e\u4f7f\u7528\u6309\u94ae\u6216\u70ed\u952e\u7ffb\u8bd1 Resource Hacker \u5f53\u524d\u7f16\u8f91\u533a\u5185\u5bb9\u3002";
            header.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            header.Font = new System.Drawing.Font(Font.FontFamily, 10, System.Drawing.FontStyle.Bold);
            root.Controls.Add(header, 0, 0);

            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 4;
            grid.RowCount = 6;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.Controls.Add(grid, 0, 1);

            providerBox = new ComboBox();
            providerBox.Dock = DockStyle.Fill;
            providerBox.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string name in config.providers.Keys) providerBox.Items.Add(name);
            providerBox.SelectedIndexChanged += delegate { LoadProviderFields(); };
            targetLanguageBox = NewEditableComboBox(new string[] {
                "Simplified Chinese",
                "Traditional Chinese",
                "English",
                "Japanese",
                "Korean",
                "French",
                "German",
                "Spanish",
                "Russian"
            });
            endpointBox = NewTextBox();
            modelBox = NewEditableComboBox(new string[] {
                "deepseek-chat",
                "deepseek-reasoner",
                "gpt-4o-mini",
                "gpt-4o",
                "gpt-4.1-mini",
                "gpt-4.1",
                "qwen-plus",
                "qwen-turbo",
                "qwen-max",
                "doubao-seed-1-6",
                "doubao-lite-32k",
                "ernie-4.0-turbo-8k",
                "glm-4-flash",
                "gemini-1.5-flash",
                "gemini-1.5-pro"
            });
            apiKeyBox = NewTextBox();
            apiKeyBox.UseSystemPasswordChar = true;
            appKeyBox = NewTextBox();
            appKeyBox.UseSystemPasswordChar = true;
            appSecretBox = NewTextBox();
            regionBox = NewTextBox();
            sourceBox = NewEditableComboBox(new string[] {
                "auto",
                "en",
                "zh",
                "zh-Hans",
                "zh-Hant",
                "ja",
                "ko",
                "fr",
                "de",
                "es",
                "ru"
            });
            targetBox = NewEditableComboBox(new string[] {
                "zh-Hans",
                "zh",
                "zh-Hant",
                "en",
                "ja",
                "ko",
                "fr",
                "de",
                "es",
                "ru"
            });

            AddField(grid, 0, "\u63a5\u53e3", providerBox);
            AddField(grid, 1, "\u76ee\u6807\u8bed\u8a00", targetLanguageBox);
            AddField(grid, 2, "Endpoint", endpointBox);
            AddField(grid, 3, "Model", modelBox);
            AddSecretField(grid, 4, "API Key", apiKeyBox);
            AddField(grid, 5, "Region", regionBox);
            AddSecretField(grid, 6, "App Key", appKeyBox);
            AddField(grid, 7, "App Secret", appSecretBox);
            AddField(grid, 8, "\u6e90\u8bed\u8a00", sourceBox);
            AddField(grid, 9, "\u76ee\u6807\u4ee3\u7801", targetBox);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = false;
            root.Controls.Add(buttons, 0, 2);

            translateAllButton = NewButton("\u7ffb\u8bd1\u5168\u90e8");
            translateAllButton.Click += delegate { TranslateAllFromFloating(); };
            translateSelectionButton = NewButton("\u7ffb\u8bd1\u9009\u4e2d");
            translateSelectionButton.Click += delegate { TranslateFocusedEditor(true); };
            saveButton = NewButton("\u4fdd\u5b58\u914d\u7f6e");
            saveButton.Click += delegate { SaveFields(); };
            testButton = NewButton("\u6d4b\u8bd5\u63a5\u53e3");
            testButton.Click += delegate { TestProvider(); };
            pasteResultButton = NewButton("\u56de\u4f20\u8bd1\u6587");
            pasteResultButton.Click += delegate { PasteResultToFocusedEditor(); };
            undoPasteButton = NewButton("\u64a4\u9500\u56de\u4f20");
            undoPasteButton.Click += delegate { UndoLastPaste(); };
            toggleFloatButton = NewButton("\u60ac\u6d6e\u680f");
            toggleFloatButton.Click += delegate { ToggleFloatingToolbar(); };
            var openConfigButton = NewButton("\u6253\u5f00\u914d\u7f6e");
            openConfigButton.Click += delegate { System.Diagnostics.Process.Start("notepad.exe", configPath); };

            buttons.Controls.Add(translateAllButton);
            buttons.Controls.Add(translateSelectionButton);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(testButton);
            buttons.Controls.Add(pasteResultButton);
            buttons.Controls.Add(undoPasteButton);
            buttons.Controls.Add(toggleFloatButton);
            buttons.Controls.Add(openConfigButton);

            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            root.Controls.Add(tabs, 0, 3);

            var resultPage = new TabPage("\u7ffb\u8bd1\u7ed3\u679c");
            var resultGrid = new TableLayoutPanel();
            resultGrid.Dock = DockStyle.Fill;
            resultGrid.Padding = new Padding(8);
            resultGrid.RowCount = 2;
            resultGrid.ColumnCount = 4;
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            resultGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            resultGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultGrid.Controls.Add(NewHeaderLabel("\u884c"), 0, 0);
            resultGrid.Controls.Add(NewHeaderLabel("\u539f\u6587"), 1, 0);
            resultGrid.Controls.Add(NewHeaderLabel("\u884c"), 2, 0);
            resultGrid.Controls.Add(NewHeaderLabel("\u8bd1\u6587"), 3, 0);
            sourceLineNumberBox = NewLineNumberBox();
            resultLineNumberBox = NewLineNumberBox();
            sourcePreviewBox = NewPreviewBox(true);
            resultPreviewBox = NewPreviewBox(false);
            resultGrid.Controls.Add(sourceLineNumberBox, 0, 1);
            resultGrid.Controls.Add(sourcePreviewBox, 1, 1);
            resultGrid.Controls.Add(resultLineNumberBox, 2, 1);
            resultGrid.Controls.Add(resultPreviewBox, 3, 1);
            resultPage.Controls.Add(resultGrid);
            tabs.TabPages.Add(resultPage);

            var logPage = new TabPage("\u65e5\u5fd7");
            logBox = NewPreviewBox(true);
            logPage.Controls.Add(logBox);
            tabs.TabPages.Add(logPage);

            targetLanguageBox.Text = config.targetLanguage ?? "Simplified Chinese";
            providerBox.SelectedItem = config.provider;
            if (providerBox.SelectedIndex < 0 && providerBox.Items.Count > 0) providerBox.SelectedIndex = 0;
            Log("Ready. Buttons and hotkeys are active.");
            ShowBalloon("Ready", "Use buttons in the window, or Ctrl+Alt+T / Ctrl+Alt+Shift+T.");
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("\u663e\u793a\u7a97\u53e3", null, delegate { ShowMainWindow(); });
            menu.Items.Add("\u7ffb\u8bd1\u5f53\u524d\u7f16\u8f91\u533a", null, delegate { TranslateFocusedEditor(false); });
            menu.Items.Add("\u7ffb\u8bd1\u9009\u4e2d\u5185\u5bb9", null, delegate { TranslateFocusedEditor(true); });
            menu.Items.Add("\u663e\u793a/\u9690\u85cf\u60ac\u6d6e\u680f", null, delegate { ToggleFloatingToolbar(); });
            menu.Items.Add("\u6253\u5f00\u914d\u7f6e", null, delegate { System.Diagnostics.Process.Start("notepad.exe", configPath); });
            menu.Items.Add("\u9000\u51fa", null, delegate { Close(); });
            return menu;
        }

        private static TextBox NewTextBox()
        {
            var box = new TextBox();
            box.Dock = DockStyle.Fill;
            return box;
        }

        private static ComboBox NewEditableComboBox(string[] items)
        {
            var box = new ComboBox();
            box.Dock = DockStyle.Fill;
            box.DropDownStyle = ComboBoxStyle.DropDown;
            box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            box.AutoCompleteSource = AutoCompleteSource.ListItems;
            if (items != null) box.Items.AddRange(items);
            return box;
        }

        private static TextBox NewPreviewBox(bool readOnly)
        {
            var box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.ReadOnly = readOnly;
            return box;
        }

        private static TextBox NewLineNumberBox()
        {
            var box = NewPreviewBox(true);
            box.ScrollBars = ScrollBars.None;
            box.TextAlign = HorizontalAlignment.Right;
            box.BackColor = System.Drawing.SystemColors.Control;
            box.ForeColor = System.Drawing.SystemColors.GrayText;
            box.TabStop = false;
            return box;
        }

        private static Label NewHeaderLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label.Font = new System.Drawing.Font(label.Font, System.Drawing.FontStyle.Bold);
            return label;
        }

        private static Button NewButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.Width = 120;
            button.Height = 34;
            button.Margin = new Padding(0, 8, 8, 8);
            return button;
        }

        private static void AddField(TableLayoutPanel grid, int fieldIndex, string labelText, Control editor)
        {
            int row = fieldIndex / 2;
            int col = (fieldIndex % 2) * 2;
            var label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            editor.Margin = new Padding(0, 8, 12, 6);
            grid.Controls.Add(label, col, row);
            grid.Controls.Add(editor, col + 1, row);
        }

        private static void AddSecretField(TableLayoutPanel grid, int fieldIndex, string labelText, TextBox editor)
        {
            int row = fieldIndex / 2;
            int col = (fieldIndex % 2) * 2;
            var label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            panel.Margin = new Padding(0, 8, 12, 6);

            editor.Margin = new Padding(0, 0, 4, 0);
            editor.Dock = DockStyle.Fill;

            var button = new Button();
            button.Text = "\u663e\u793a";
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0);
            button.Click += delegate
            {
                editor.UseSystemPasswordChar = !editor.UseSystemPasswordChar;
                button.Text = editor.UseSystemPasswordChar ? "\u663e\u793a" : "\u9690\u85cf";
            };

            panel.Controls.Add(editor, 0, 0);
            panel.Controls.Add(button, 1, 0);
            grid.Controls.Add(label, col, row);
            grid.Controls.Add(panel, col + 1, row);
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void LoadProviderFields()
        {
            string name = Convert.ToString(providerBox.SelectedItem);
            if (String.IsNullOrWhiteSpace(name) || !config.providers.ContainsKey(name)) return;
            ProviderConfig p = config.providers[name];
            endpointBox.Text = p.endpoint ?? "";
            modelBox.Text = p.model ?? "";
            apiKeyBox.Text = p.apiKey ?? "";
            appKeyBox.Text = p.appKey ?? "";
            appSecretBox.Text = p.appSecret ?? "";
            regionBox.Text = p.region ?? "";
            sourceBox.Text = p.source ?? "auto";
            targetBox.Text = p.target ?? "zh-Hans";
            Log("宸插姞杞芥帴鍙? " + name);
        }

        private void SaveFields()
        {
            string name = Convert.ToString(providerBox.SelectedItem);
            if (String.IsNullOrWhiteSpace(name) || !config.providers.ContainsKey(name)) return;
            ProviderConfig p = config.providers[name];
            config.provider = name;
            config.targetLanguage = targetLanguageBox.Text.Trim();
            p.endpoint = endpointBox.Text.Trim();
            p.model = modelBox.Text.Trim();
            p.apiKey = apiKeyBox.Text.Trim();
            p.appKey = appKeyBox.Text.Trim();
            p.appSecret = appSecretBox.Text.Trim();
            p.region = regionBox.Text.Trim();
            p.source = sourceBox.Text.Trim();
            p.target = targetBox.Text.Trim();
            config.Save(configPath);
            Log("Saved config: " + configPath);
            ShowBalloon("Config saved", "Provider is now " + name + ".");
        }

        private void TestProvider()
        {
            SaveFields();
            RunBusy("姝ｅ湪娴嬭瘯鎺ュ彛...", delegate
            {
                string source = "File\nEdit\nCancel";
                string translated = Translator.Translate(config, source);
                SetResultPreview(source, translated);
                Log("娴嬭瘯缁撴灉: " + translated.Replace("\r", "").Replace("\n", " | "));
                ShowBalloon("鎺ュ彛娴嬭瘯瀹屾垚", translated);
            });
        }

        public void TranslateAllFromFloating()
        {
            TranslateFocusedEditor(false);
        }

        public void SelectAllFromFloating()
        {
            SelectAllInFocusedEditor();
        }

        public void CopyFromFloating()
        {
            CopyFocusedEditor(false);
        }

        public void TranslateSelectionFromFloating()
        {
            TranslateFocusedEditor(true);
        }

        public void PasteResultFromFloating()
        {
            PasteResultToFocusedEditor();
        }

        public void CopyTranslationFromFloating()
        {
            CopyTranslationToClipboard();
        }

        public void PasteTranslationFromFloating()
        {
            PasteTranslationToFocusedEditor(false);
        }

        public void UndoPasteFromFloating()
        {
            UndoLastPaste();
        }

        public void ShowMainFromFloating()
        {
            ShowMainWindow();
        }

        private void CopyTranslationToClipboard()
        {
            string text = resultPreviewBox.Text;
            if (String.IsNullOrWhiteSpace(text))
            {
                ShowBalloon("No result", "Translation result is empty.");
                return;
            }

            SetClipboardText(text);
            Log("Copied translation result to clipboard: " + text.Length + " characters.");
        }

        private void PasteTranslationToFocusedEditor(bool replacePreviousFullCopy)
        {
            if (busy)
            {
                ShowBalloon("Busy", "A task is already running.");
                return;
            }

            string text = resultPreviewBox.Text;
            if (String.IsNullOrWhiteSpace(text))
            {
                ShowBalloon("No result", "Translation result is empty.");
                return;
            }

            RunBusy("Pasting translation result...", delegate
            {
                ActivateResourceHackerIfAvailable();
                CaptureCurrentEditorForUndo();
                if (replacePreviousFullCopy && lastCopyWasFullEditor) Thread.Sleep(80);
                SetClipboardText(text);
                Thread.Sleep(120);
                SendKeys.SendWait("^v");
                Log("Pasted translation result: " + text.Length + " characters.");
            });
        }

        private void CaptureCurrentEditorForUndo()
        {
            string oldClipboard = TryGetClipboardText();
            SendKeys.SendWait("^a");
            Thread.Sleep(120);
            SendKeys.SendWait("^c");
            Thread.Sleep(250);
            string before = GetClipboardText();
            if (!String.IsNullOrEmpty(before))
            {
                lastBeforePasteText = before;
                lastUndoUsesEditorUndo = false;
                Log("Undo snapshot saved: " + before.Length + " characters.");
            }
            if (oldClipboard != null) SetClipboardText(oldClipboard);
            SendKeys.SendWait("^a");
            Thread.Sleep(120);
        }

        private void UndoLastPaste()
        {
            if (busy)
            {
                ShowBalloon("Busy", "A task is already running.");
                return;
            }

            if (String.IsNullOrEmpty(lastBeforePasteText))
            {
                ShowBalloon("No undo snapshot", "No previous editor content was saved before paste.");
                return;
            }

            RunBusy("Undoing last paste...", delegate
            {
                ActivateResourceHackerIfAvailable();
                if (lastUndoUsesEditorUndo)
                {
                    SendKeys.SendWait("^z");
                    Log("Undid previous selection paste with editor undo. Snapshot length: " + lastBeforePasteText.Length + " characters.");
                    ShowBalloon("Undo complete", "Previous selection translation was undone.");
                    return;
                }

                SendKeys.SendWait("^a");
                Thread.Sleep(120);
                SetClipboardText(lastBeforePasteText);
                Thread.Sleep(120);
                SendKeys.SendWait("^v");
                Log("Restored previous editor content: " + lastBeforePasteText.Length + " characters.");
                ShowBalloon("Undo complete", "Previous Resource Hacker editor content was restored.");
            });
        }

        private void SelectAllInFocusedEditor()
        {
            if (busy)
            {
                ShowBalloon("Busy", "A translation task is already running.");
                return;
            }

            RunBusy("Select all in Resource Hacker editor...", delegate
            {
                ActivateResourceHackerIfAvailable();
                SendKeys.SendWait("^a");
                lastCopyWasFullEditor = true;
                Log("Selected all text in the focused editor.");
            });
        }

        private void CopyFocusedEditor(bool onlySelection)
        {
            if (busy)
            {
                ShowBalloon("Busy", "A translation task is already running.");
                return;
            }

            RunBusy(onlySelection ? "Copy selected text..." : "Select all and copy...", delegate
            {
                ActivateResourceHackerIfAvailable();
                if (!onlySelection)
                {
                    SendKeys.SendWait("^a");
                    Thread.Sleep(120);
                }
                SendKeys.SendWait("^c");
                Thread.Sleep(250);
                string source = GetClipboardText();
                if (String.IsNullOrWhiteSpace(source))
                    throw new InvalidOperationException("No text was copied from Resource Hacker.");

                lastCopyWasFullEditor = !onlySelection;
                SetResultPreview(source, resultPreviewBox.Text);
                Log("Copied " + source.Length + " characters from Resource Hacker.");
            });
        }
        private void ToggleFloatingToolbar()
        {
            floatingEnabled = !floatingEnabled;
            if (!floatingEnabled)
            {
                floatingToolbar.Hide();
            }
            else
            {
            }
            Log(floatingEnabled ? "Floating bar enabled." : "Floating bar hidden.");
        }

        private void UpdateFloatingToolbar()
        {
            if (!floatingEnabled || IsDisposed) return;
            IntPtr hwnd = NativeMethods.FindResourceHackerWindow();
            resourceHackerWindow = hwnd;
            if (hwnd == IntPtr.Zero)
            {
                floatingToolbar.Hide();
                return;
            }

            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect))
            {
                floatingToolbar.Hide();
                return;
            }

            floatingToolbar.SetAutoLocation(GetDefaultFloatingLocation(rect));
            if (!floatingToolbar.Visible) floatingToolbar.Show(this);
        }

        private System.Drawing.Point GetDefaultFloatingLocation(NativeMethods.RECT rect)
        {
            System.Drawing.Rectangle screen = System.Windows.Forms.Screen.FromRectangle(
                System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            ).WorkingArea;

            int preferredX = rect.Left + 700;
            int preferredY = rect.Top + 12;
            int x = Math.Max(screen.Left, Math.Min(preferredX, screen.Right - floatingToolbar.Width));
            int y = Math.Max(screen.Top, Math.Min(preferredY, screen.Bottom - floatingToolbar.Height));
            return new System.Drawing.Point(x, y);
        }

        private void ActivateResourceHackerIfAvailable()
        {
            IntPtr hwnd = resourceHackerWindow;
            if (hwnd == IntPtr.Zero) hwnd = NativeMethods.FindResourceHackerWindow();
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(hwnd);
                Thread.Sleep(180);
            }
        }

        private void PasteResultToFocusedEditor()
        {
            if (busy)
            {
                ShowBalloon("Busy", "A task is already running.");
                return;
            }

            string text = resultPreviewBox.Text;
            if (String.IsNullOrWhiteSpace(text))
            {
                ShowBalloon("No result", "Translation result is empty.");
                return;
            }

            PasteTranslationToFocusedEditor(false);
        }

        private void TranslateFocusedEditor(bool onlySelection)
        {
            if (busy)
            {
                ShowBalloon("Busy", "A task is already running.");
                return;
            }

            SaveFields();
            RunBusy(onlySelection ? "Translating selection..." : "Translating focused editor...", delegate
            {
                ActivateResourceHackerIfAvailable();
                string oldClipboard = TryGetClipboardText();
                if (!onlySelection)
                {
                    SendKeys.SendWait("^a");
                    Thread.Sleep(120);
                }
                SendKeys.SendWait("^c");
                Thread.Sleep(250);

                string source = GetClipboardText();
                lastCopyWasFullEditor = !onlySelection;
                if (String.IsNullOrWhiteSpace(source))
                    throw new InvalidOperationException("No text was copied. Focus Resource Hacker editor and try again.");

                Log("Copied " + source.Length + " characters.");
                string translated = Translator.Translate(config, source);
                if (String.IsNullOrWhiteSpace(translated))
                    throw new InvalidOperationException("Provider returned empty translation.");

                SetResultPreview(source, translated);
                WarnIfLineShapeChanged(source, translated);
                if (!onlySelection)
                {
                    lastBeforePasteText = source;
                    lastUndoUsesEditorUndo = false;
                    Log("Undo snapshot saved: " + source.Length + " characters.");
                }
                else
                {
                    lastBeforePasteText = source;
                    lastUndoUsesEditorUndo = true;
                    Log("Selection undo snapshot saved: " + source.Length + " characters.");
                }
                SetClipboardText(translated);
                Thread.Sleep(120);
                SendKeys.SendWait("^v");
                Thread.Sleep(150);
                if (oldClipboard != null) SetClipboardText(oldClipboard);
                Log("Translated and pasted " + translated.Length + " characters.");
                ShowBalloon("Translated", "Text was pasted back into the focused editor.");
            });
        }

        private void RunBusy(string message, Action action)
        {
            MainWindowSnapshot mainWindowSnapshot = CaptureMainWindowSnapshot();
            busy = true;
            SetUiBusy(true);
            Log(message);
            var worker = new Thread(delegate()
            {
                try { action(); }
                catch (Exception ex)
                {
                    Log("閿欒: " + ex.Message);
                    ShowBalloon("鎿嶄綔澶辫触", ex.Message);
                }
                finally
                {
                    busy = false;
                    SetUiBusy(false);
                    RestoreMainWindowSnapshot(mainWindowSnapshot);
                }
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.IsBackground = true;
            worker.Start();
        }

        private MainWindowSnapshot CaptureMainWindowSnapshot()
        {
            if (InvokeRequired)
                return (MainWindowSnapshot)Invoke(new Func<MainWindowSnapshot>(CaptureMainWindowSnapshot));

            return new MainWindowSnapshot
            {
                Visible = Visible,
                WindowState = WindowState,
                Bounds = Bounds
            };
        }

        private void RestoreMainWindowSnapshot(MainWindowSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Visible || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<MainWindowSnapshot>(RestoreMainWindowSnapshot), snapshot);
                return;
            }

            Show();
            if (snapshot.WindowState == FormWindowState.Normal)
            {
                WindowState = FormWindowState.Normal;
                Bounds = snapshot.Bounds;
            }
            else if (snapshot.WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                WindowState = FormWindowState.Normal;
                Bounds = snapshot.Bounds;
            }
            Activate();
        }

        private void SetUiBusy(bool isBusy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetUiBusy), isBusy);
                return;
            }
            translateAllButton.Enabled = !isBusy;
            translateSelectionButton.Enabled = !isBusy;
            saveButton.Enabled = !isBusy;
            testButton.Enabled = !isBusy;
            pasteResultButton.Enabled = !isBusy;
            undoPasteButton.Enabled = !isBusy;
            toggleFloatButton.Enabled = !isBusy;
        }

        private string TryGetClipboardText()
        {
            try { return GetClipboardText(); } catch { return null; }
        }

        private static string GetClipboardText()
        {
            for (int i = 0; i < 20; i++)
            {
                try { return Clipboard.GetText(); }
                catch { Thread.Sleep(100); }
            }
            throw new InvalidOperationException("Could not read clipboard.");
        }

        private static void SetClipboardText(string text)
        {
            for (int i = 0; i < 20; i++)
            {
                try { Clipboard.SetText(text); return; }
                catch { Thread.Sleep(100); }
            }
            throw new InvalidOperationException("Could not write clipboard.");
        }

        private void ShowBalloon(string title, string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(ShowBalloon), title, text);
                return;
            }
            icon.BalloonTipTitle = title;
            icon.BalloonTipText = text.Length > 240 ? text.Substring(0, 240) : text;
            icon.ShowBalloonTip(4000);
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private void SetResultPreview(string source, string translated)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(SetResultPreview), source, translated);
                return;
            }
            sourcePreviewBox.Text = source ?? "";
            resultPreviewBox.Text = translated ?? "";
            sourceLineNumberBox.Text = BuildLineNumbers(source);
            resultLineNumberBox.Text = BuildLineNumbers(translated);
        }

        private static string BuildLineNumbers(string text)
        {
            int lines = TextShape.CountLines(text);
            if (lines <= 0) return "";
            var sb = new StringBuilder();
            for (int i = 1; i <= lines; i++) sb.AppendLine(i.ToString());
            return sb.ToString();
        }

        private void WarnIfLineShapeChanged(string source, string translated)
        {
            int sourceLines = TextShape.CountLines(source);
            int translatedLines = TextShape.CountLines(translated);
            if (sourceLines != translatedLines)
            {
                Log("WARNING: line count changed. Source lines=" + sourceLines + ", translation lines=" + translatedLines + ". Review before compiling.");
                ShowBalloon("Line structure changed", "Review Translation Result before pasting or compiling.");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            translateAllHotkey.Dispose();
            translateSelectionHotkey.Dispose();
            followTimer.Stop();
            followTimer.Dispose();
            floatingToolbar.Close();
            icon.Visible = false;
            icon.Dispose();
            base.OnFormClosing(e);
        }
    }

    internal static class TextShape
    {
        public static int CountLines(string text)
        {
            if (String.IsNullOrEmpty(text)) return 0;
            int count = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') count++;
            return count;
        }
    }

    internal sealed class FloatingToolbar : Form
    {
        private readonly MainForm owner;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public FloatingToolbar(MainForm owner)
        {
            this.owner = owner;
            Text = "AI \u7ffb\u8bd1";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 520;
            Height = 64;
            StartPosition = FormStartPosition.Manual;

            var panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = false;
            panel.Padding = new Padding(4);
            Controls.Add(panel);

            panel.Controls.Add(MakeButton("\u5168\u9009", delegate { owner.SelectAllFromFloating(); }));
            panel.Controls.Add(MakeButton("\u590d\u5236", delegate { owner.CopyFromFloating(); }));
            panel.Controls.Add(MakeButton("\u5168\u8bd1", delegate { owner.TranslateAllFromFloating(); }));
            panel.Controls.Add(MakeButton("\u9009\u8bd1", delegate { owner.TranslateSelectionFromFloating(); }));
            panel.Controls.Add(MakeButton("\u56de\u4f20\u8bd1\u6587", delegate { owner.PasteResultFromFloating(); }));
            panel.Controls.Add(MakeButton("\u64a4\u9500\u56de\u4f20", delegate { owner.UndoPasteFromFloating(); }));
            panel.Controls.Add(MakeButton("\u4e3b\u7a97", delegate { owner.ShowMainFromFloating(); }));
            panel.Controls.Add(MakeButton("\u590d\u5236\u8bd1\u6587", delegate { owner.CopyTranslationFromFloating(); }));
            panel.Controls.Add(MakeButton("\u7c98\u8d34\u8bd1\u6587", delegate { owner.PasteTranslationFromFloating(); }));
            FitToButtons(panel);
        }

        private void FitToButtons(FlowLayoutPanel panel)
        {
            int width = 28;
            foreach (Control child in panel.Controls)
            {
                width += child.Width + child.Margin.Left + child.Margin.Right;
            }
            Width = Math.Min(Math.Max(width, 420), 720);
        }
        private static Button MakeButton(string text, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new System.Drawing.Size(58, 34);
            button.Height = 34;
            button.Margin = new Padding(2, 4, 2, 4);
            button.Click += handler;
            return button;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public void SetAutoLocation(System.Drawing.Point point)
        {
            Location = point;
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    internal static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static IntPtr FindResourceHackerWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;
                var title = new StringBuilder(512);
                GetWindowText(hWnd, title, title.Capacity);
                string value = title.ToString();
                if (value.IndexOf("Resource Hacker", StringComparison.OrdinalIgnoreCase) >= 0
                    && value.IndexOf("AI Translator", StringComparison.OrdinalIgnoreCase) < 0
                    && value.IndexOf("AI \u7ffb\u8bd1", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }

    internal sealed class HotKeyWindow : NativeWindow, IDisposable
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;
        private readonly int id;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event EventHandler HotKeyPressed;

        public HotKeyWindow(int id, uint modifiers, uint key)
        {
            this.id = id;
            CreateHandle(new CreateParams());
            RegisterHotKey(Handle, id, modifiers, key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && HotKeyPressed != null) HotKeyPressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterHotKey(Handle, id);
            DestroyHandle();
        }
    }

    internal sealed class AppConfig
    {
        public string provider = "openai";
        public string targetLanguage = "Simplified Chinese";
        public Dictionary<string, ProviderConfig> providers = new Dictionary<string, ProviderConfig>();

        public static AppConfig Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Config file not found", path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            return new JavaScriptSerializer().Deserialize<AppConfig>(json);
        }

        public void Save(string path)
        {
            string json = new JavaScriptSerializer().Serialize(this);
            File.WriteAllText(path, PrettyJson(json), Encoding.UTF8);
        }

        private static string PrettyJson(string json)
        {
            var sb = new StringBuilder();
            int indent = 0;
            bool quoted = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"' && (i == 0 || json[i - 1] != '\\')) quoted = !quoted;
                if (!quoted && (ch == '{' || ch == '['))
                {
                    sb.Append(ch).AppendLine();
                    indent++;
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && (ch == '}' || ch == ']'))
                {
                    sb.AppendLine();
                    indent--;
                    sb.Append(new string(' ', indent * 2)).Append(ch);
                }
                else if (!quoted && ch == ',')
                {
                    sb.Append(ch).AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && ch == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }

    internal sealed class ProviderConfig
    {
        public string type;
        public string endpoint;
        public string apiKey;
        public string appKey;
        public string appSecret;
        public string region;
        public string model;
        public string source = "auto";
        public string target = "zh-Hans";
        public string prompt;
    }

    internal static class Translator
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static string Translate(AppConfig config, string text)
        {
            if (!config.providers.ContainsKey(config.provider))
                throw new InvalidOperationException("Provider not found in config: " + config.provider);

            ProviderConfig provider = config.providers[config.provider];
            string type = (provider.type ?? config.provider ?? "").ToLowerInvariant();
            if (type == "openai-compatible") return ChatCompletions(provider, config.targetLanguage, text);
            if (type == "microsoft") return Microsoft(provider, text);
            if (type == "google-v2") return GoogleV2(provider, text);
            if (type == "youdao") return Youdao(provider, text);
            throw new NotSupportedException("Unsupported provider type: " + provider.type);
        }

        private static string ChatCompletions(ProviderConfig p, string targetLanguage, string text)
        {
            Require(p.endpoint, "endpoint");
            Require(p.apiKey, "apiKey");
            Require(p.model, "model");
            string systemPrompt = String.IsNullOrWhiteSpace(p.prompt)
                ? "You are a strict Windows resource-script localization engine. Translate ONLY user-visible plain text to " + targetLanguage + ". The output must be a direct replacement that can compile in Resource Hacker. Keep the exact same number of lines as the input. For each input line, output exactly one corresponding line. Preserve indentation, leading spaces, tabs, blank lines, wrapping, and line order. Preserve every control ID, numeric position, size, style flag, extended style, class name, resource name, language marker, code page, comment, brace, comma, quote style, escape sequence, placeholder, format token, and line order. Preserve accelerator hotkeys and shortcut markers exactly, including &, \\t, Ctrl+, Alt+, F1-F12, and menu shortcut columns. Only translate quoted visible captions or menu text. Do not translate identifiers, constants, filenames, registry paths, URLs, variables, placeholders, or non-visible script syntax. Do not merge lines. Do not split lines. Do not add explanations, markdown, comments, or extra text. Return only the replacement text."
                : p.prompt;
            int lineCount = TextShape.CountLines(text);
            string userContent = "Input line count: " + lineCount + "\nOutput requirement: return exactly " + lineCount + " lines, preserving one output line for each input line.\n\n" + text;

            var body = new Dictionary<string, object>();
            body["model"] = p.model;
            body["temperature"] = 0;
            body["messages"] = new object[] {
                new Dictionary<string, object> { {"role", "system"}, {"content", systemPrompt} },
                new Dictionary<string, object> { {"role", "user"}, {"content", userContent} }
            };

            string response = PostJson(p.endpoint.TrimEnd('/') + "/chat/completions", Json.Serialize(body), new Dictionary<string, string> {
                {"Authorization", "Bearer " + p.apiKey}
            });
            var parsed = Json.DeserializeObject(response) as Dictionary<string, object>;
            var choices = parsed["choices"] as object[];
            var choice = choices[0] as Dictionary<string, object>;
            var message = choice["message"] as Dictionary<string, object>;
            return Convert.ToString(message["content"]);
        }

        private static string Microsoft(ProviderConfig p, string text)
        {
            Require(p.endpoint, "endpoint");
            Require(p.apiKey, "apiKey");
            Require(p.region, "region");
            string url = p.endpoint.TrimEnd('/') + "/translate?api-version=3.0&to=" + Uri.EscapeDataString(p.target ?? "zh-Hans");
            if (!String.IsNullOrWhiteSpace(p.source) && p.source != "auto")
                url += "&from=" + Uri.EscapeDataString(p.source);

            string body = Json.Serialize(new object[] { new Dictionary<string, object> { { "Text", text } } });
            string response = PostJson(url, body, new Dictionary<string, string> {
                {"Ocp-Apim-Subscription-Key", p.apiKey},
                {"Ocp-Apim-Subscription-Region", p.region}
            });
            var root = Json.DeserializeObject(response) as object[];
            var item = root[0] as Dictionary<string, object>;
            var translations = item["translations"] as object[];
            var first = translations[0] as Dictionary<string, object>;
            return Convert.ToString(first["text"]);
        }

        private static string GoogleV2(ProviderConfig p, string text)
        {
            Require(p.endpoint, "endpoint");
            Require(p.apiKey, "apiKey");
            string target = p.target ?? "zh-CN";
            string url = p.endpoint.TrimEnd('/') + "?key=" + Uri.EscapeDataString(p.apiKey);
            var body = new Dictionary<string, object> {
                {"q", text},
                {"target", target},
                {"format", "text"}
            };
            if (!String.IsNullOrWhiteSpace(p.source) && p.source != "auto") body["source"] = p.source;
            string response = PostJson(url, Json.Serialize(body), null);
            var root = Json.DeserializeObject(response) as Dictionary<string, object>;
            var data = root["data"] as Dictionary<string, object>;
            var translations = data["translations"] as object[];
            var first = translations[0] as Dictionary<string, object>;
            return Convert.ToString(first["translatedText"]);
        }

        private static string Youdao(ProviderConfig p, string text)
        {
            Require(p.endpoint, "endpoint");
            Require(p.appKey, "appKey");
            Require(p.appSecret, "appSecret");
            string salt = Guid.NewGuid().ToString("N");
            string curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string sign = Sha256Hex(p.appKey + Truncate(text) + salt + curtime + p.appSecret);
            var fields = new Dictionary<string, string> {
                {"q", text},
                {"from", String.IsNullOrWhiteSpace(p.source) ? "auto" : p.source},
                {"to", String.IsNullOrWhiteSpace(p.target) ? "zh-CHS" : p.target},
                {"appKey", p.appKey},
                {"salt", salt},
                {"sign", sign},
                {"signType", "v3"},
                {"curtime", curtime}
            };
            string response = PostForm(p.endpoint, fields);
            var root = Json.DeserializeObject(response) as Dictionary<string, object>;
            if (root.ContainsKey("translation"))
            {
                var arr = root["translation"] as object[];
                return Convert.ToString(arr[0]);
            }
            throw new InvalidOperationException("Youdao response did not contain translation: " + response);
        }

        private static string Truncate(string q)
        {
            if (q == null) return "";
            if (q.Length <= 20) return q;
            return q.Substring(0, 10) + q.Length.ToString() + q.Substring(q.Length - 10);
        }

        private static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void Require(string value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing provider setting: " + name);
        }

        private static string PostJson(string url, string body, Dictionary<string, string> headers)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "application/json";
            request.ContentLength = bytes.Length;
            if (headers != null)
            {
                foreach (var h in headers) request.Headers[h.Key] = h.Value;
            }
            using (Stream s = request.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            return ReadResponse(request);
        }

        private static string PostForm(string url, Dictionary<string, string> fields)
        {
            var parts = new List<string>();
            foreach (var kv in fields)
                parts.Add(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value ?? ""));
            byte[] bytes = Encoding.UTF8.GetBytes(String.Join("&", parts.ToArray()));
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/json";
            request.ContentLength = bytes.Length;
            using (Stream s = request.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            return ReadResponse(request);
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null) throw;
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    throw new InvalidOperationException(reader.ReadToEnd());
            }
        }
    }
}


