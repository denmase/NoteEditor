using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using JianpuEditor.Controls;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Glue;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Services.AudioToMidi;
using JianpuEditor.Services.EditCommands;
using JianpuEditor.ViewModels;
using JianpuEditor.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JianpuEditor
{
    public sealed partial class MainForm : Form, IView
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILayoutService _layoutService;
        private readonly IMidiOutput _midiOutput;
        private readonly SampleLibraryViewModel _sampleLibrary;
        private readonly ISessionService _sessionService;
        private readonly BasicPitchSettings _lastBasicPitchSettings = BasicPitchSettings.CreateDefault();
        private readonly GameSettings _lastGameSettings = GameSettings.CreateDefault();
        private JianpuScore _mutationBeforeSnapshot;
        private int _mutationBeforeMeasureIndex = -1;
        private bool _suppressCanvasMutationTracking;
        private MainFormViewBinder _binder;
        private MainFormLayoutContext _layoutContext;
        private TableLayoutPanel _mainLayout;
        private TableLayoutPanel _chromeLayout;
        private TabControl _tabControl;
        private DocumentTab _previousActiveTab;

        // These were fixed fields before multi-tab support; they're now computed from whichever
        // tab is active so the ~200 existing call sites across this file didn't all need
        // individual rewriting to read through an ActiveTab indirection.
        private DocumentTab ActiveTab => _tabControl?.SelectedTab?.Tag as DocumentTab;
        private MainViewModel _viewModel => ActiveTab.ViewModel;
        private ScoreCanvas _canvas => ActiveTab.Canvas;
        private IAppMessenger _messenger => ActiveTab.Messenger;
        private IEditCommandHistory _commandHistory => ActiveTab.CommandHistory;
        private ScoreCanvasGlue _glue => ActiveTab.Glue;

        private readonly TextBox _chordBox = new TextBox();
        private readonly NumericUpDown _measureSelector = new NumericUpDown();
        private readonly NumericUpDown _measureRangeFrom = new NumericUpDown();
        private readonly NumericUpDown _measureRangeTo = new NumericUpDown();
        private readonly ScoreStatusBar _statusBar = new ScoreStatusBar();
        private RibbonButton _tieButton;
        private RibbonButton _playButton;
        private RibbonButton _stopButton;
        private MenuStrip _menuStrip;
        private ContextMenuStrip _sampleLibraryMenu;
        private ToolStripMenuItem _darkModeMenuItem;
        private ToolStripMenuItem _fillPlaceholdersMenuItem;
        private ToolStripMenuItem _notationStyleChineseMenuItem;
        private ToolStripMenuItem _notationStyleIndonesianMenuItem;
        private ToolStripMenuItem _undoMenuItem;
        private ToolStripMenuItem _redoMenuItem;
        private readonly ToolTip _toolTip = new ToolTip();
        private bool _isExecutingHistoryChange;

        private const string NoteButtonToolTip =
            "Click: insert or modify a note at the selected position\r\nCtrl+Click: append to the end of the current measure\r\nCtrl+Shift+Click: append and copy the previous note's duration/octave";

        public MainForm(
            IServiceScopeFactory scopeFactory,
            ILayoutService layoutService,
            IMidiOutput midiOutput,
            SampleLibraryViewModel sampleLibrary,
            ISessionService sessionService)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
            _midiOutput = midiOutput ?? throw new ArgumentNullException(nameof(midiOutput));
            _sampleLibrary = sampleLibrary ?? throw new ArgumentNullException(nameof(sampleLibrary));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

            InitializeComponent();
            SetupLayoutStructure();

            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            Load += OnFormLoad;
            Resize += OnFormResize;
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
        }

        /// <summary>
        /// No-op. <see cref="IView"/> declares this as taking an externally-supplied viewmodel,
        /// from back when MainForm bound to one process-wide singleton; now every tab (including
        /// the first one, created in <see cref="SetupLayoutStructure"/>) constructs and owns its
        /// own, so there's nothing left for a caller to hand in. Kept only for interface
        /// compatibility -- MainForm is the sole implementer of <see cref="IView"/>, so nothing
        /// calls this polymorphically.
        /// </summary>
        public void InitializeBindings(object viewModel)
        {
        }

        /// <summary>Wires the MainForm-level event handling a newly created tab needs -- the
        /// mechanical per-canvas-instance and per-viewmodel-instance subscriptions that used to
        /// happen once, in the constructor, back when there was only ever one document.</summary>
        private void AttachTab(DocumentTab tab)
        {
            tab.ViewModel.Document.PropertyChanged += OnDocumentPropertyChanged;
            tab.ViewModel.Document.PropertyChanged += (s, e) => SyncTabTitle(tab);
            tab.CommandHistory.HistoryChanged += (s, e) =>
            {
                if (ReferenceEquals(ActiveTab, tab))
                {
                    UpdateUndoMenuState();
                }
            };

            tab.Canvas.SelectionChanged += OnCanvasSelectionChanged;
            tab.Canvas.MeasureTextEdited += OnCanvasMeasureTextEdited;
            tab.Canvas.HeaderEdited += OnCanvasHeaderEdited;
            tab.Canvas.ChordMarkersChanged += OnCanvasChordMarkersChanged;
            tab.Canvas.ScoreMutationStarting += OnCanvasScoreMutationStarting;
            tab.Canvas.PlaybackSeeked += OnCanvasPlaybackSeeked;
            tab.Canvas.ContextMenuOpening += OnCanvasContextMenuOpening;
            tab.Canvas.ZoomChanged += () =>
            {
                if (ReferenceEquals(ActiveTab, tab))
                {
                    _statusBar.SetZoomPercent((int)Math.Round(tab.Canvas.ZoomScale * 100));
                }
            };

            tab.ViewModel.RequestOpenScore += (s, e) => OnOpenScore(s, e);
            tab.ViewModel.RequestSaveScore += (s, e) => OnSaveScore(s, e);
            tab.ViewModel.RequestSaveAsScore += (s, e) => OnSaveScoreAs(s, e);
            tab.ViewModel.RequestExportPdf += (s, e) => OnExportPdf(s, e);
            tab.ViewModel.RequestExportMidi += (s, e) => OnExportMidi(s, e);
            tab.ViewModel.RequestImportMidi += (s, e) => OnImportMidi(s, e);
            tab.ViewModel.RequestImportAudioInstrument += (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Instrument);
            tab.ViewModel.RequestImportAudioVocal += (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Vocal);
            tab.ViewModel.RequestTransposeDialog += (s, e) => ShowTransposeDialog();
        }

        /// <summary>Creates a new tab, wires it, adds it to the TabControl, and selects it (which
        /// activates it -- see <see cref="OnActiveTabChanged"/>). Used for New/Open/Import
        /// MIDI/Import Audio/Sample Library, all of which always open a new tab rather than
        /// replacing the active one.</summary>
        private DocumentTab CreateTab()
        {
            var tab = new DocumentTab(_scopeFactory);
            AttachTab(tab);

            var page = new TabPage { Tag = tab };
            tab.Canvas.Dock = DockStyle.Fill;
            tab.Canvas.MinimumSize = new Size(200, 200);
            page.Controls.Add(tab.Canvas);

            _tabControl.TabPages.Add(page);
            _tabControl.SelectedTab = page;

            // TabControl.SelectedIndexChanged is not a reliable way to detect this activation --
            // confirmed unreliable for the very first tab even on real Windows (see the removed
            // comment this replaced in SetupLayoutStructure), and observed here to also not fire
            // for later tabs at least under Mono/headless. Driving OnActiveTabChanged explicitly,
            // every time a tab is created, guarantees the shared chrome (and per-tab-switch
            // behavior like auto-stopping a playing tab you're leaving) stays correct regardless
            // of whether the event happens to fire too -- it's harmless to also run it again if
            // SelectedIndexChanged does fire for the same activation, since ActiveTab is already
            // this tab by then and every guard below keys off that.
            OnActiveTabChanged(this, EventArgs.Empty);
            SyncTabTitle(tab);
            return tab;
        }

        private void SyncTabTitle(DocumentTab tab)
        {
            var page = FindTabPage(tab);
            if (page != null)
            {
                page.Text = tab.TabTitle;
            }
        }

        private TabPage FindTabPage(DocumentTab tab)
        {
            return _tabControl.TabPages.Cast<TabPage>().FirstOrDefault(p => ReferenceEquals(p.Tag, tab));
        }

        /// <summary>Re-points the shared chrome (status bar, toolbar buttons, chord/measure
        /// boxes, window title) at whichever tab is now selected. Called for every tab switch,
        /// including right after a new tab is created.</summary>
        private void OnActiveTabChanged(object sender, EventArgs e)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            // Playback doesn't stop itself just because its tab scrolled out of view -- without
            // this, switching away from a still-playing tab would leave its audio running
            // invisibly in the background (RemoveTab covers the tab-closed case; this covers
            // "switched away but left it open"). A tab RemoveTab already closed has already had
            // its own Playback.Stop() called, so this is a no-op for it here.
            if (_previousActiveTab != null
                && !ReferenceEquals(_previousActiveTab, tab)
                && _previousActiveTab.ViewModel.Playback.IsPlaying)
            {
                _previousActiveTab.ViewModel.Playback.Stop();
            }

            _previousActiveTab = tab;

            _binder?.Dispose();
            _binder = new MainFormViewBinder(
                tab.ViewModel,
                this,
                _chordBox,
                _measureSelector,
                _measureRangeFrom,
                _measureRangeTo,
                _statusBar,
                _tieButton,
                _playButton,
                _stopButton);

            _layoutContext.ScoreCanvas = tab.Canvas;
            UpdateUndoMenuState();
            _statusBar.SetZoomPercent((int)Math.Round(tab.Canvas.ZoomScale * 100));

            // Deliberately not RestoreLayout(): it resets AutoScrollPosition on the canvas it's
            // pointed at, which here would mean switching back to a tab always jumped its scroll
            // position back to the top. The TabControl itself already handles showing/hiding the
            // right tab's canvas; nothing here needs to touch the chrome sizing RestoreLayout is
            // actually for (that doesn't depend on which tab is active).
        }

        /// <summary>Prompts to save unsaved changes (Yes/No/Cancel), then disposes the tab and
        /// removes its page. A cancelled prompt leaves the tab open. Closing the last remaining
        /// tab opens a fresh blank one instead of leaving the window empty.</summary>
        private void CloseTab(DocumentTab tab)
        {
            if (tab == null)
            {
                return;
            }

            var page = FindTabPage(tab);
            if (page == null)
            {
                return;
            }

            if (!ConfirmDiscardOrSave(tab))
            {
                return;
            }

            RemoveTab(page, tab);
        }

        /// <summary>If <paramref name="tab"/> has unsaved changes, switches to it (so the user can
        /// see what they're being asked about) and prompts Yes/No/Cancel; Yes runs the existing
        /// Save/Save-As flow. Returns false only on Cancel (either button, or the Save As dialog
        /// being dismissed) -- callers must not proceed with closing that tab (or the whole app)
        /// when this returns false. A clean tab always returns true without prompting.</summary>
        private bool ConfirmDiscardOrSave(DocumentTab tab)
        {
            if (!tab.ViewModel.Document.IsDirty)
            {
                return true;
            }

            var page = FindTabPage(tab);
            if (page != null && !ReferenceEquals(_tabControl.SelectedTab, page))
            {
                _tabControl.SelectedTab = page;
                OnActiveTabChanged(this, EventArgs.Empty); // Don't rely solely on SelectedIndexChanged; see CreateTab.
            }

            var choice = MessageBox.Show(
                "Save changes to \"" + tab.ViewModel.Document.Title + "\" before closing?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
            if (choice == DialogResult.Cancel)
            {
                return false;
            }

            if (choice == DialogResult.Yes)
            {
                OnSaveScore(this, EventArgs.Empty);
                if (tab.ViewModel.Document.IsDirty)
                {
                    return false; // Save was itself cancelled (e.g. the Save As dialog was dismissed).
                }
            }

            return true;
        }

        /// <summary>Whole-app close: prompts for every open tab's unsaved changes (not just the
        /// active one), in tab order. Cancelling any one of them aborts the close entirely and
        /// leaves every tab open, including ones already resolved earlier in the loop -- a tab
        /// saved before the cancellation stays saved, matching how closing several documents one
        /// after another normally behaves.</summary>
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (TabPage page in _tabControl.TabPages)
            {
                var tab = page.Tag as DocumentTab;
                if (tab != null && !ConfirmDiscardOrSave(tab))
                {
                    e.Cancel = true;
                    return;
                }
            }

            SaveSession();
        }

        /// <summary>Snapshots every open tab's current state (whatever the prompts above left it
        /// as -- Yes-saved, No-still-dirty, or already clean) so the next launch can restore it.
        /// A dirty tab's in-memory content is captured via a fresh autosave file even if the user
        /// chose not to save it to its own location, the same "recovered document" safety net
        /// familiar from other editors.</summary>
        private void SaveSession()
        {
            var snapshots = new List<SessionTabSnapshot>();
            var activeIndex = 0;
            var selectedPage = _tabControl.SelectedTab;

            for (var i = 0; i < _tabControl.TabPages.Count; i++)
            {
                var page = _tabControl.TabPages[i];
                if (ReferenceEquals(page, selectedPage))
                {
                    activeIndex = snapshots.Count;
                }

                var tab = page.Tag as DocumentTab;
                if (tab == null)
                {
                    continue;
                }

                snapshots.Add(new SessionTabSnapshot
                {
                    FilePath = tab.ViewModel.Document.CurrentFilePath,
                    IsDirty = tab.ViewModel.Document.IsDirty,
                    Score = tab.ViewModel.Document.Score
                });
            }

            _sessionService.Save(snapshots, activeIndex);
        }

        /// <summary>Recreates tabs from a previously saved session, if one exists and at least one
        /// entry is still restorable. Returns false (leaving the constructor's initial blank tab
        /// untouched, for OnFormLoad to fall back to the usual demo score) when there's no session,
        /// or every entry's file(s) are now missing.</summary>
        private bool RestoreSession()
        {
            var session = _sessionService.Load();
            if (session.Tabs.Count == 0)
            {
                return false;
            }

            var originalFirstTab = ActiveTab;
            var restoredTabs = new List<DocumentTab>();
            foreach (var entry in session.Tabs)
            {
                var tab = CreateTab();
                if (RestoreTab(tab, entry))
                {
                    restoredTabs.Add(tab);
                }
                else
                {
                    DiscardTab(tab);
                }
            }

            if (restoredTabs.Count == 0)
            {
                return false;
            }

            DiscardTab(originalFirstTab);

            var activeIndex = Math.Max(0, Math.Min(session.ActiveTabIndex, restoredTabs.Count - 1));
            var page = FindTabPage(restoredTabs[activeIndex]);
            if (page != null)
            {
                _tabControl.SelectedTab = page;
                OnActiveTabChanged(this, EventArgs.Empty); // Don't rely solely on SelectedIndexChanged; see CreateTab.
            }

            return true;
        }

        private static bool RestoreTab(DocumentTab tab, SessionTabState entry)
        {
            try
            {
                if (entry.IsDirty && !string.IsNullOrEmpty(entry.AutosavePath) && File.Exists(entry.AutosavePath))
                {
                    tab.ViewModel.Document.RestoreFromAutosave(entry.AutosavePath, entry.FilePath);
                }
                else if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
                {
                    tab.ViewModel.Document.LoadFromFile(entry.FilePath);
                }
                else
                {
                    AppLog.Info(
                        "Session restore: skipping entry with no readable file (FilePath=" +
                        (entry.FilePath ?? "<none>") + ", AutosavePath=" + (entry.AutosavePath ?? "<none>") + ")");
                    return false;
                }

                tab.Glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                tab.Glue.ResetPlaybackHead();
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Exception("Session restore failed for tab (FilePath=" + entry.FilePath + ")", ex);
                return false;
            }
        }

        /// <summary>Removes a just-created tab whose New/Open/Import/Sample load attempt failed
        /// or was never actually populated, skipping the unsaved-changes prompt entirely -- the
        /// user never asked for this tab to exist, so there's nothing of theirs to lose.</summary>
        private void DiscardTab(DocumentTab tab)
        {
            var page = FindTabPage(tab);
            if (page != null)
            {
                RemoveTab(page, tab);
            }
        }

        private void RemoveTab(TabPage page, DocumentTab tab)
        {
            tab.ViewModel.Playback.Stop();
            _tabControl.TabPages.Remove(page);
            tab.Dispose();

            if (_tabControl.TabPages.Count == 0)
            {
                CreateTab();
            }
        }

        private const int TabCloseButtonSize = 14;

        private void OnDrawTabItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _tabControl.TabPages.Count)
            {
                return;
            }

            var page = _tabControl.TabPages[e.Index];
            var tabRect = _tabControl.GetTabRect(e.Index);
            e.DrawBackground();

            var closeRect = GetCloseButtonRect(tabRect);
            var textRect = new Rectangle(
                tabRect.X + 6,
                tabRect.Y,
                Math.Max(0, closeRect.Left - tabRect.X - 8),
                tabRect.Height);
            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                _tabControl.Font,
                textRect,
                SystemColors.ControlText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using (var pen = new Pen(Color.Gray, 1.5f))
            {
                e.Graphics.DrawLine(pen, closeRect.Left + 3, closeRect.Top + 3, closeRect.Right - 3, closeRect.Bottom - 3);
                e.Graphics.DrawLine(pen, closeRect.Right - 3, closeRect.Top + 3, closeRect.Left + 3, closeRect.Bottom - 3);
            }
        }

        private static Rectangle GetCloseButtonRect(Rectangle tabRect)
        {
            return new Rectangle(
                tabRect.Right - TabCloseButtonSize - 6,
                tabRect.Top + (tabRect.Height - TabCloseButtonSize) / 2,
                TabCloseButtonSize,
                TabCloseButtonSize);
        }

        private void OnTabControlMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            for (var i = 0; i < _tabControl.TabPages.Count; i++)
            {
                if (GetCloseButtonRect(_tabControl.GetTabRect(i)).Contains(e.Location))
                {
                    CloseTab(_tabControl.TabPages[i].Tag as DocumentTab);
                    return;
                }
            }
        }

        public void RestoreLayout()
        {
            _layoutService.RestoreLayout();
        }

        public void ApplyTheme()
        {
            _layoutService.ApplyTheme();
            _binder?.SyncFromViewModels();
        }

        private void SetupLayoutStructure()
        {
            _menuStrip = BuildMenuStrip();
            var toolbarPanel = BuildToolbarPanel();

            _chromeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _chromeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _chromeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, MainFormLayoutContext.MinimumMenuHeight));
            _chromeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, MainFormLayoutContext.DefaultToolbarHeight));

            _menuStrip.Dock = DockStyle.Fill;
            toolbarPanel.Dock = DockStyle.Fill;

            _chromeLayout.Controls.Add(_menuStrip, 0, 0);
            _chromeLayout.Controls.Add(toolbarPanel, 0, 1);

            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _tabControl = new TabControl { MinimumSize = new Size(200, 200), DrawMode = TabDrawMode.OwnerDrawFixed };
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.DrawItem += OnDrawTabItem;
            _tabControl.MouseDown += OnTabControlMouseDown;
            _statusBar.Dock = DockStyle.Fill;
            _statusBar.MinimumSize = new Size(0, MainFormLayoutContext.StatusRowHeight);
            _statusBar.ZoomInClicked += () => _canvas.ZoomIn();
            _statusBar.ZoomOutClicked += () => _canvas.ZoomOut();
            _statusBar.EngineClicked += (s, e) => ShowAudioEngineDialog();
            _statusBar.SetEngine(_midiOutput.EngineName);
            // Zoom percent display and _layoutContext.ScoreCanvas are set once the first tab is
            // created below and OnActiveTabChanged runs -- there's no canvas to read yet here.

            _mainLayout.Controls.Add(_chromeLayout, 0, 0);
            _mainLayout.Controls.Add(_tabControl, 0, 1);
            _mainLayout.Controls.Add(_statusBar, 0, 2);

            Controls.Clear();
            Controls.Add(_mainLayout);
            MainMenuStrip = _menuStrip;

            _layoutContext = new MainFormLayoutContext
            {
                Form = this,
                MainLayout = _mainLayout,
                ChromeLayout = _chromeLayout,
                MenuStrip = _menuStrip,
                ToolbarPanel = toolbarPanel,
                ScoreStatusBar = _statusBar
            };
            _layoutService.Attach(_layoutContext);

            _tabControl.SelectedIndexChanged += OnActiveTabChanged;
            CreateTab(); // Activates itself; see the comment in CreateTab().
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            // The first tab is already created in SetupLayoutStructure (called from the
            // constructor) so _layoutContext.ScoreCanvas is populated before RestoreLayout/
            // ApplyTheme below ever run.
            RestoreLayout();
            ApplyDpiScaling();
            ApplyTheme();

            AppLog.Info("Jianpu Editor started");

            if (!RestoreSession())
            {
                var demoResult = _viewModel.SampleLibrary.LoadDemoScore(_viewModel.Document, _messenger);
                _glue.ApplyEditResult(demoResult);
                _glue.ResetPlaybackHead();
            }

            _binder.SyncHeaderFromDocument();
            _binder.SyncFromViewModels();
            _viewModel.SetStatus("Ready - click the title/key/tempo/BPM/composer to edit directly, click a lyric line to edit its text");
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            RestoreLayout();
        }

        private void ApplyDpiScaling()
        {
            _layoutService.ApplyDpiScaling();
        }

        private MenuStrip BuildMenuStrip()
        {
            var menu = new MenuStrip();
            PopulateMenuStrip(menu);
            return menu;
        }

        private void PopulateMenuStrip(MenuStrip menu)
        {
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(CreateMenuItem("New", Keys.Control | Keys.N, (s, e) => OnNewScore(s, e)));
            fileMenu.DropDownItems.Add(CreateMenuItem("Open...", Keys.Control | Keys.O, OnOpenScore));
            fileMenu.DropDownItems.Add(CreateMenuItem("Save", Keys.Control | Keys.S, OnSaveScore));
            fileMenu.DropDownItems.Add(CreateMenuItem("Save As...", Keys.Control | Keys.Shift | Keys.S, OnSaveScoreAs));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(CreateMenuItem("Export PDF...", Keys.Control | Keys.P, OnExportPdf));
            fileMenu.DropDownItems.Add(CreateMenuItem("Export MIDI...", Keys.None, OnExportMidi));
            fileMenu.DropDownItems.Add(CreateMenuItem("Import MIDI... (Spike)", Keys.None, OnImportMidi));
            fileMenu.DropDownItems.Add(CreateMenuItem("Import from Audio (Instrument)... (Spike)", Keys.None,
                (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Instrument)));
            fileMenu.DropDownItems.Add(CreateMenuItem("Import from Audio (Vocal)... (Spike)", Keys.None,
                (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Vocal)));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            var sampleMenu = new ToolStripMenuItem("Sample Library");
            sampleMenu.DropDownOpening += (s, e) => PopulateSampleLibraryMenu(sampleMenu.DropDownItems);
            PopulateSampleLibraryMenu(sampleMenu.DropDownItems);
            fileMenu.DropDownItems.Add(sampleMenu);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(CreateMenuItem("Close Tab", Keys.Control | Keys.W, (s, e) => CloseTab(ActiveTab)));
            fileMenu.DropDownItems.Add(CreateMenuItem("Exit", Keys.None, (s, e) => Close()));

            var editMenu = new ToolStripMenuItem("Edit");
            _undoMenuItem = CreateMenuItem("Undo", Keys.Control | Keys.Z, (s, e) => ExecuteUndo());
            _undoMenuItem.Enabled = false;
            editMenu.DropDownItems.Add(_undoMenuItem);
            _redoMenuItem = CreateMenuItem("Redo", Keys.Control | Keys.Y, (s, e) => ExecuteRedo());
            _redoMenuItem.Enabled = false;
            editMenu.DropDownItems.Add(_redoMenuItem);
            editMenu.DropDownItems.Add(CreateMenuItem("Cut", Keys.Control | Keys.X, (s, e) => ExecuteCut()));
            editMenu.DropDownItems.Add(CreateMenuItem("Copy", Keys.Control | Keys.C, (s, e) => ExecuteCopy()));
            editMenu.DropDownItems.Add(CreateMenuItem("Paste", Keys.Control | Keys.V, (s, e) => ExecutePaste()));
            editMenu.DropDownItems.Add(CreateMenuItem("Delete", Keys.Delete, (s, e) => ExecuteDelete()));
            editMenu.DropDownItems.Add(CreateMenuItem("Add Measure", Keys.None, (s, e) => ExecuteAddMeasure()));
            editMenu.DropDownItems.Add(CreateMenuItem(
                "Add Measure (with placeholders)",
                Keys.Control | Keys.Shift | Keys.N,
                (s, e) => ExecuteAddMeasureWithPlaceholders()));
            editMenu.DropDownItems.Add(CreateMenuItem("Duplicate Measure(s)", Keys.None, (s, e) => ExecuteDuplicateMeasures()));
            editMenu.DropDownItems.Add(CreateMenuItem("Chord Transpose...", Keys.None, (s, e) => ShowTransposeDialog()));
            editMenu.DropDownItems.Add(CreateMenuItem("Chord Suggestion...", Keys.None, (s, e) => ShowHarmonySuggestionDialog()));
            editMenu.DropDownItems.Add(CreateMenuItem("Bulk Edit Lyrics...", Keys.None, (s, e) => ShowBulkLyricEditDialog()));
            editMenu.DropDownItems.Add(CreateMenuItem("Instruments...", Keys.None, (s, e) => ShowInstrumentDialog()));
            editMenu.DropDownItems.Add(CreateMenuItem("Audio Engine...", Keys.None, (s, e) => ShowAudioEngineDialog()));
            var ornamentMenu = new ToolStripMenuItem("Ornaments");
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Grace Note",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.GraceNote))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Trill",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Trill))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Turn",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Turn))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Mordent",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Mordent))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Fermata",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fermata))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Breath Mark",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.BreathMark))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Staccato",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Staccato))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Accent",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Accent))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Tenuto",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Tenuto))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Glissando",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Glissando))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Segno",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Segno))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Coda",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Coda))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "D.C. (Da Capo)",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DaCapo))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "D.S. (Dal Segno)",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DalSegno))));
            ornamentMenu.DropDownItems.Add(CreateMenuItem(
                "Fine",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fine))));
            editMenu.DropDownItems.Add(ornamentMenu);
            var dynamicsMenu = new ToolStripMenuItem("Dynamics");
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("pp", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("pp"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("p", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("p"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("mp", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mp"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("mf", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mf"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("f", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("f"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem("ff", Keys.None, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("ff"))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem(
                "Crescendo (selected notes)",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(true))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem(
                "Diminuendo (selected notes)",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(false))));
            dynamicsMenu.DropDownItems.Add(CreateMenuItem(
                "Remove Hairpin",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.RemoveHairpin())));
            editMenu.DropDownItems.Add(dynamicsMenu);
            var barLineMenu = new ToolStripMenuItem("Bar Line");
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Single",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.SetBarLineType(BarLineType.Single))));
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Double",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.SetBarLineType(BarLineType.Double))));
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Final",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.SetBarLineType(BarLineType.Final))));
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Repeat End",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.SetBarLineType(BarLineType.RepeatEnd))));
            barLineMenu.DropDownItems.Add(new ToolStripSeparator());
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Toggle Repeat Start",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.ToggleRepeatStart())));
            barLineMenu.DropDownItems.Add(CreateMenuItem(
                "Toggle Line Break After This Measure",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.MeasureContent.ToggleLineBreak())));
            editMenu.DropDownItems.Add(barLineMenu);
            var voltaMenu = new ToolStripMenuItem("Volta Bracket");
            voltaMenu.DropDownItems.Add(CreateMenuItem(
                "1st Ending",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.ScoreEditor.AddVolta("1."))));
            voltaMenu.DropDownItems.Add(CreateMenuItem(
                "2nd Ending",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.ScoreEditor.AddVolta("2."))));
            voltaMenu.DropDownItems.Add(new ToolStripSeparator());
            voltaMenu.DropDownItems.Add(CreateMenuItem(
                "Remove Volta Bracket",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.ScoreEditor.RemoveVolta())));
            editMenu.DropDownItems.Add(voltaMenu);
            var accidentalMenu = new ToolStripMenuItem("Accidental");
            accidentalMenu.DropDownItems.Add(CreateMenuItem(
                "Sharp",
                Keys.None,
                (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetAccidental(AccidentalKind.Sharp))));
            accidentalMenu.DropDownItems.Add(CreateMenuItem(
                "Flat",
                Keys.None,
                (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetAccidental(AccidentalKind.Flat))));
            accidentalMenu.DropDownItems.Add(CreateMenuItem(
                "Natural",
                Keys.None,
                (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetAccidental(AccidentalKind.Natural))));
            accidentalMenu.DropDownItems.Add(new ToolStripSeparator());
            accidentalMenu.DropDownItems.Add(CreateMenuItem(
                "None",
                Keys.None,
                (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetAccidental(AccidentalKind.None))));
            editMenu.DropDownItems.Add(accidentalMenu);
            var voicesMenu = new ToolStripMenuItem("Voices");
            voicesMenu.DropDownItems.Add(CreateMenuItem(
                "Single Voice",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.ScoreEditor.SetVoiceMode(false))));
            voicesMenu.DropDownItems.Add(CreateMenuItem(
                "SATB (Alto / Tenor / Bass)",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.ScoreEditor.SetVoiceMode(true))));
            editMenu.DropDownItems.Add(voicesMenu);
            editMenu.DropDownItems.Add(CreateMenuItem("Clear Score", Keys.None, OnClearScore));

            var viewMenu = new ToolStripMenuItem("View");
            _darkModeMenuItem = new ToolStripMenuItem("Dark Mode")
            {
                CheckOnClick = true,
                Checked = AppTheme.IsDarkMode
            };
            _darkModeMenuItem.CheckedChanged += OnDarkModeToggled;
            viewMenu.DropDownItems.Add(_darkModeMenuItem);
            _fillPlaceholdersMenuItem = new ToolStripMenuItem("Fill placeholders by default when adding measures")
            {
                CheckOnClick = true,
                Checked = AppTheme.FillMeasurePlaceholdersOnAdd
            };
            _fillPlaceholdersMenuItem.CheckedChanged += OnFillPlaceholdersToggled;
            viewMenu.DropDownItems.Add(_fillPlaceholdersMenuItem);
            var notationStyleMenu = new ToolStripMenuItem("Notation Style");
            _notationStyleChineseMenuItem = new ToolStripMenuItem("Chinese / Western (default)")
            {
                Checked = AppTheme.NotationStyle == NotationStyle.Chinese
            };
            _notationStyleChineseMenuItem.Click += (s, e) => SetNotationStyle(NotationStyle.Chinese);
            notationStyleMenu.DropDownItems.Add(_notationStyleChineseMenuItem);
            _notationStyleIndonesianMenuItem = new ToolStripMenuItem("Indonesian (kres/mol accidentals, beams above)")
            {
                Checked = AppTheme.NotationStyle == NotationStyle.Indonesian
            };
            _notationStyleIndonesianMenuItem.Click += (s, e) => SetNotationStyle(NotationStyle.Indonesian);
            notationStyleMenu.DropDownItems.Add(_notationStyleIndonesianMenuItem);
            viewMenu.DropDownItems.Add(notationStyleMenu);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(CreateMenuItem("Zoom In", Keys.Control | Keys.Oemplus, (s, e) => _canvas.ZoomIn()));
            viewMenu.DropDownItems.Add(CreateMenuItem("Zoom Out", Keys.Control | Keys.OemMinus, (s, e) => _canvas.ZoomOut()));
            viewMenu.DropDownItems.Add(CreateMenuItem("Reset Zoom", Keys.Control | Keys.D0, (s, e) => _canvas.ResetZoom()));
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(CreateMenuItem("Reset Layout", Keys.None, (s, e) => RestoreLayout()));

            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(viewMenu);
        }

        private FlowLayoutPanel BuildToolbarPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Padding = new Padding(6, 4, 6, 4),
                WrapContents = true,
                AutoScroll = false
            };

            var playback = new RibbonGroup("Playback");
            _playButton = CreateRibbonButton(RibbonIcon.Play, "Play", OnPlayScore);
            _stopButton = CreateRibbonButton(RibbonIcon.Stop, "Stop", OnStopPlayback);
            _stopButton.Enabled = false;
            var instrumentsButton = CreateRibbonButton(RibbonIcon.Instrument, "Instruments", ShowInstrumentDialog);
            playback.AddRow(_playButton, _stopButton, instrumentsButton);
            panel.Controls.Add(playback);

            var notes = new RibbonGroup("Notes 1-7 . Rest . Hold");
            var noteButtons = new Control[9];
            for (var pitch = 1; pitch <= 7; pitch++)
            {
                noteButtons[pitch - 1] = CreateNoteButton(pitch);
            }

            noteButtons[7] = CreateRestButton();
            noteButtons[8] = CreateContinuationDotButton();
            notes.AddRow(noteButtons);
            panel.Controls.Add(notes);

            var modify = new RibbonGroup("Modify");
            _tieButton = CreateRibbonButton(RibbonIcon.Tie, "Tie", () => _viewModel.TieEditor.ToggleTieModeCommand.Execute(null), compact: true);
            modify.AddRow(
                CreateRibbonButton(RibbonIcon.MeasureAdd, "New measure", ExecuteAddMeasure, compact: true),
                CreateRibbonButton(RibbonIcon.OctaveUp, "High octave", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetOctave(1)), compact: true),
                CreateRibbonButton(RibbonIcon.OctaveDown, "Low octave", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetOctave(-1)), compact: true),
                CreateRibbonButton(RibbonIcon.TransposeUp, "Transpose up", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.TransposePitch(1)), compact: true),
                CreateRibbonButton(RibbonIcon.TransposeDown, "Transpose down", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.TransposePitch(-1)), compact: true),
                _tieButton);
            modify.AddRow(
                CreateRibbonButton(RibbonIcon.Split, "Split", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.SplitSelectedNotes()), compact: true),
                CreateRibbonButton(RibbonIcon.Merge, "Merge", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.MergeSelectedNotes()), compact: true),
                CreateRibbonButton(RibbonIcon.Dotted, "Dotted", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.ToggleDotted()), compact: true),
                CreateRibbonButton(RibbonIcon.Extend, "Extend", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.IncreaseDuration()), compact: true),
                CreateRibbonButton(RibbonIcon.Shorten, "Shorten", () => ExecuteNoteEdit(() => _viewModel.NoteEditor.DecreaseDuration()), compact: true));
            panel.Controls.Add(modify);

            var ornaments = new RibbonGroup("Ornaments");
            ornaments.AddRow(
                CreateRibbonButton(RibbonIcon.Grace, "Grace note", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.GraceNote)), compact: true),
                CreateRibbonButton(RibbonIcon.Trill, "Trill", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Trill)), compact: true),
                CreateRibbonButton(RibbonIcon.Turn, "Turn", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Turn)), compact: true),
                CreateRibbonButton(RibbonIcon.Mordent, "Mordent", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Mordent)), compact: true),
                CreateRibbonButton(RibbonIcon.Fermata, "Fermata", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fermata)), compact: true),
                CreateRibbonButton(RibbonIcon.BreathMark, "Breath mark", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.BreathMark)), compact: true));
            ornaments.AddRow(
                CreateRibbonButton(RibbonIcon.Staccato, "Staccato", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Staccato)), compact: true),
                CreateRibbonButton(RibbonIcon.Accent, "Accent", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Accent)), compact: true),
                CreateRibbonButton(RibbonIcon.Tenuto, "Tenuto", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Tenuto)), compact: true),
                CreateRibbonButton(RibbonIcon.Segno, "Segno", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Segno)), compact: true),
                CreateRibbonButton(RibbonIcon.Coda, "Coda", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Coda)), compact: true),
                CreateRibbonButton(RibbonIcon.Glissando, "Glissando", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Glissando)), compact: true));
            ornaments.AddRow(
                CreateRibbonButton(RibbonIcon.DaCapo, "D.C. (Da Capo)", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DaCapo)), compact: true),
                CreateRibbonButton(RibbonIcon.DalSegno, "D.S. (Dal Segno)", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DalSegno)), compact: true),
                CreateRibbonButton(RibbonIcon.Fine, "Fine", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fine)), compact: true));
            panel.Controls.Add(ornaments);

            var dynamics = new RibbonGroup("Dynamics");
            dynamics.AddRow(
                CreateRibbonButton(RibbonIcon.DynamicPianissimo, "pp", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("pp")), compact: true),
                CreateRibbonButton(RibbonIcon.DynamicPiano, "p", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("p")), compact: true),
                CreateRibbonButton(RibbonIcon.DynamicMezzoPiano, "mp", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mp")), compact: true),
                CreateRibbonButton(RibbonIcon.DynamicMezzoForte, "mf", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mf")), compact: true),
                CreateRibbonButton(RibbonIcon.DynamicForte, "f", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("f")), compact: true),
                CreateRibbonButton(RibbonIcon.DynamicFortissimo, "ff", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("ff")), compact: true));
            dynamics.AddRow(
                CreateRibbonButton(RibbonIcon.Crescendo, "Crescendo", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(true)), compact: true),
                CreateRibbonButton(RibbonIcon.Diminuendo, "Diminuendo", () => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(false)), compact: true));
            panel.Controls.Add(dynamics);

            var measures = new RibbonGroup("Measures");
            _measureSelector.Minimum = 1;
            _measureSelector.Maximum = 1;
            _measureSelector.Width = 48;
            _measureSelector.ValueChanged += OnMeasureSelectorChanged;
            measures.AddRow(CreateInlineLabel("Cur"), _measureSelector, CreateRibbonButton(RibbonIcon.Duplicate, "Duplicate", ExecuteDuplicateMeasures, compact: true));

            _measureRangeFrom.Minimum = 1;
            _measureRangeFrom.Maximum = 1;
            _measureRangeFrom.Width = 48;
            _measureRangeFrom.ValueChanged += OnMeasureRangeChanged;
            _measureRangeTo.Minimum = 1;
            _measureRangeTo.Maximum = 1;
            _measureRangeTo.Width = 48;
            _measureRangeTo.ValueChanged += OnMeasureRangeChanged;
            measures.AddRow(CreateInlineLabel("From"), _measureRangeFrom, CreateInlineLabel("To"), _measureRangeTo);
            panel.Controls.Add(measures);

            var chord = new RibbonGroup("Chord text");
            _chordBox.Width = 100;
            _chordBox.TextChanged += OnChordTextChanged;
            chord.AddRow(_chordBox);
            panel.Controls.Add(chord);

            var other = new RibbonGroup(string.Empty);
            _sampleLibraryMenu = new ContextMenuStrip();
            _sampleLibraryMenu.Opening += (s, e) => PopulateSampleLibraryMenu(_sampleLibraryMenu.Items);
            var sampleButton = CreateRibbonButton(RibbonIcon.Library, "Sample library", () => { }, compact: true);
            sampleButton.Click += (s, e) => _sampleLibraryMenu.Show(sampleButton, new Point(0, sampleButton.Height));
            other.AddRow(
                CreateRibbonButton(RibbonIcon.Delete, "Delete", ExecuteDelete, compact: true),
                sampleButton);
            panel.Controls.Add(other);

            // Groups naturally size to their own row count (Modify/Measures hold two rows,
            // the rest hold one), which left their bottoms -- and the caption row baseline --
            // uneven. Give every group the tallest group's height so they all line up.
            var groups = new[] { playback, notes, modify, ornaments, measures, chord, other };
            var maxGroupHeight = 0;
            foreach (var group in groups)
            {
                maxGroupHeight = Math.Max(maxGroupHeight, group.NaturalHeight);
            }

            foreach (var group in groups)
            {
                group.SetFixedHeight(maxGroupHeight);
            }

            return panel;
        }

        private RibbonButton CreateRibbonButton(RibbonIcon icon, string caption, Action onClick, bool compact = false)
        {
            var button = new RibbonButton(icon, caption, compact);
            button.Click += (s, e) => onClick();
            return button;
        }

        private static Label CreateInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(2, 10, 2, 0),
                Font = new Font("Segoe UI", 7.5f)
            };
        }


        private DigitButton CreateNoteButton(int pitch)
        {
            var button = new DigitButton(pitch.ToString());
            _toolTip.SetToolTip(button, NoteButtonToolTip);
            button.Click += (s, e) => OnNoteButtonClick(pitch);
            return button;
        }

        private DigitButton CreateRestButton()
        {
            var button = new DigitButton("0");
            _toolTip.SetToolTip(button, NoteButtonToolTip);
            button.Click += (s, e) => OnRestButtonClick();
            return button;
        }

        private DigitButton CreateContinuationDotButton()
        {
            var button = new DigitButton(".");
            _toolTip.SetToolTip(button, "Continuation dot: holds the previous note's pitch through this beat "
                + "(jianpu notation), beaming with neighbors like a real note.\r\n" + NoteButtonToolTip);
            button.Click += (s, e) => OnContinuationDotButtonClick();
            return button;
        }

        private void OnNoteButtonClick(int pitch)
        {
            if (IsAppendModifierActive())
            {
                var copyStyle = IsCopyStyleModifierActive();
                ExecuteNoteEdit(() => _viewModel.NoteEditor.AppendNote(pitch, copyStyle));
                return;
            }

            ExecuteNoteEdit(() => _viewModel.NoteEditor.AddNote(pitch));
        }

        private void OnRestButtonClick()
        {
            if (IsAppendModifierActive())
            {
                var copyStyle = IsCopyStyleModifierActive();
                ExecuteNoteEdit(() => _viewModel.NoteEditor.AppendRest(copyStyle));
                return;
            }

            ExecuteNoteEdit(() => _viewModel.NoteEditor.AddRest());
        }

        private void OnContinuationDotButtonClick()
        {
            if (IsAppendModifierActive())
            {
                var copyStyle = IsCopyStyleModifierActive();
                ExecuteNoteEdit(() => _viewModel.NoteEditor.AppendContinuationDot(copyStyle));
                return;
            }

            ExecuteNoteEdit(() => _viewModel.NoteEditor.AddContinuationDot());
        }

        private static bool IsAppendModifierActive()
        {
            return (Control.ModifierKeys & Keys.Control) == Keys.Control;
        }

        private static bool IsCopyStyleModifierActive()
        {
            return IsAppendModifierActive() && (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
        }

        private void OnDarkModeToggled(object sender, EventArgs e)
        {
            AppTheme.SetDarkMode(_darkModeMenuItem.Checked);
            ApplyTheme();
        }

        private void OnFillPlaceholdersToggled(object sender, EventArgs e)
        {
            AppTheme.SetFillMeasurePlaceholdersOnAdd(_fillPlaceholdersMenuItem.Checked);
        }

        private void SetNotationStyle(NotationStyle style)
        {
            AppTheme.SetNotationStyle(style);
            _notationStyleChineseMenuItem.Checked = style == NotationStyle.Chinese;
            _notationStyleIndonesianMenuItem.Checked = style == NotationStyle.Indonesian;
        }

        private static ToolStripMenuItem CreateMenuItem(string text, Keys shortcut, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text, null, handler);
            if (shortcut != Keys.None && IsValidMenuShortcut(shortcut))
            {
                item.ShortcutKeys = shortcut;
                item.ShowShortcutKeys = true;
            }

            return item;
        }

        private static bool IsValidMenuShortcut(Keys shortcut)
        {
            return (shortcut & Keys.Modifiers) != Keys.None;
        }

        private void UpdateUndoMenuState()
        {
            if (_undoMenuItem != null)
            {
                _undoMenuItem.Enabled = _commandHistory.CanUndo;
            }

            if (_redoMenuItem != null)
            {
                _redoMenuItem.Enabled = _commandHistory.CanRedo;
            }
        }

        private void ExecuteNoteEdit(Func<ScoreEditResult> action)
        {
            ExecuteTrackedEdit(action, refreshUndoMenu: false);
        }

        private void ExecuteScoreEdit(Func<ScoreEditResult> action)
        {
            ExecuteTrackedEdit(action, refreshUndoMenu: true);
        }

        private void ExecuteTrackedEdit(Func<ScoreEditResult> action, bool refreshUndoMenu)
        {
            DiscardPendingCanvasMutation();
            _glue?.AttachDocumentScore();
            _suppressCanvasMutationTracking = true;
            try
            {
                var result = action();
                if (!result.Changed)
                {
                    return;
                }

                _glue.ApplyEditResult(result);
                _binder.SyncFromViewModels();
                if (refreshUndoMenu)
                {
                    UpdateUndoMenuState();
                }
            }
            finally
            {
                _suppressCanvasMutationTracking = false;
                _mutationBeforeSnapshot = null;
                _mutationBeforeMeasureIndex = -1;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                if (!ShouldDeferUndoRedoToTextInput())
                {
                    ExecuteUndo();
                    return true;
                }
            }

            if (keyData == (Keys.Control | Keys.Y))
            {
                if (!ShouldDeferUndoRedoToTextInput())
                {
                    ExecuteRedo();
                    return true;
                }
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.N))
            {
                ExecuteAddMeasureWithPlaceholders();
                return true;
            }

            if (keyData == (Keys.Control | Keys.W))
            {
                CloseTab(ActiveTab);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ExecuteAddMeasure()
        {
            ExecuteScoreEdit(() => _viewModel.MeasureNavigation.AddMeasure());
        }

        private void ExecuteAddMeasureWithPlaceholders()
        {
            ExecuteScoreEdit(() => _viewModel.MeasureNavigation.AddMeasureWithPlaceholders());
        }

        private void ExecuteDuplicateMeasures()
        {
            ExecuteScoreEdit(() => _viewModel.MeasureNavigation.DuplicateMeasures());
        }

        private void ExecuteDelete()
        {
            ExecuteScoreEdit(() => _viewModel.ScoreEditor.Delete());
        }

        /// <summary>Copy, then delete -- but only if there was actually something to copy, so an
        /// accidental Cut with nothing selected doesn't fall through to Delete's own "nothing
        /// selected" behavior (deleting the last note in the measure).</summary>
        private void ExecuteCut()
        {
            if (_viewModel.NoteEditor.CopySelectedNotes())
            {
                ExecuteDelete();
            }
        }

        private void ExecuteCopy()
        {
            _viewModel.NoteEditor.CopySelectedNotes();
        }

        private void ExecutePaste()
        {
            ExecuteNoteEdit(() => _viewModel.NoteEditor.PasteNotes());
        }

        /// <summary>Builds the right-click menu's edit-command items, based on what the canvas
        /// just told us is under the cursor (it has already synced the selection to match, before
        /// raising this event -- see <see cref="ScoreCanvas.ApplyHitSelectionForContextMenu"/>).
        /// Every item here reuses an existing command already wired to the ribbon/menu elsewhere,
        /// except Cut/Copy/Paste.</summary>
        private void OnCanvasContextMenuOpening(object sender, ScoreContextMenuEventArgs e)
        {
            var menu = e.Menu;
            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
            }

            switch (e.HitType)
            {
                case ScoreHitType.Note:
                    AddNoteContextMenuItems(menu);
                    break;
                case ScoreHitType.Gap:
                    AddGapContextMenuItems(menu);
                    break;
                case ScoreHitType.Tie:
                    menu.Items.Add("Remove Tie", null, (s, args) => ExecuteDelete());
                    break;
                case ScoreHitType.ChordMarker:
                case ScoreHitType.ChordDelete:
                case ScoreHitType.ChordDragHandle:
                case ScoreHitType.ChordAddSlot:
                case ScoreHitType.ChordRow:
                    AddChordContextMenuItems(menu);
                    break;
                case ScoreHitType.LyricText:
                    menu.Items.Add("Align Lyrics", null, (s, args) => ExecuteScoreEdit(() => _viewModel.MeasureContent.AlignLyricsToNotes()));
                    break;
                default:
                    AddMeasureContextMenuItems(menu);
                    break;
            }
        }

        private void AddNoteContextMenuItems(ContextMenuStrip menu)
        {
            menu.Items.Add("Cut", null, (s, e) => ExecuteCut());
            menu.Items.Add("Copy", null, (s, e) => ExecuteCopy());
            menu.Items.Add("Delete", null, (s, e) => ExecuteDelete());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Shorten", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.DecreaseDuration()));
            menu.Items.Add("Extend", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.IncreaseDuration()));
            menu.Items.Add("Toggle Dotted", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.ToggleDotted()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Octave Up", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetOctave(1)));
            menu.Items.Add("Octave Down", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SetOctave(-1)));
            menu.Items.Add("Transpose Up", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.TransposePitch(1)));
            menu.Items.Add("Transpose Down", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.TransposePitch(-1)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Split", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.SplitSelectedNotes()));
            menu.Items.Add("Merge", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.MergeSelectedNotes()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Tie Here", null, (s, e) => _viewModel.TieEditor.ToggleTieModeCommand.Execute(null));

            var ornamentsMenu = new ToolStripMenuItem("Ornaments");
            ornamentsMenu.DropDownItems.Add("Grace Note", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.GraceNote)));
            ornamentsMenu.DropDownItems.Add("Trill", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Trill)));
            ornamentsMenu.DropDownItems.Add("Turn", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Turn)));
            ornamentsMenu.DropDownItems.Add("Mordent", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Mordent)));
            ornamentsMenu.DropDownItems.Add("Fermata", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fermata)));
            ornamentsMenu.DropDownItems.Add("Breath Mark", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.BreathMark)));
            ornamentsMenu.DropDownItems.Add("Staccato", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Staccato)));
            ornamentsMenu.DropDownItems.Add("Accent", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Accent)));
            ornamentsMenu.DropDownItems.Add("Tenuto", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Tenuto)));
            ornamentsMenu.DropDownItems.Add("Segno", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Segno)));
            ornamentsMenu.DropDownItems.Add("Coda", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Coda)));
            ornamentsMenu.DropDownItems.Add("D.C. (Da Capo)", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DaCapo)));
            ornamentsMenu.DropDownItems.Add("D.S. (Dal Segno)", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.DalSegno)));
            ornamentsMenu.DropDownItems.Add("Fine", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fine)));
            ornamentsMenu.DropDownItems.Add("Glissando", null, (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Glissando)));
            menu.Items.Add(ornamentsMenu);

            var dynamicsMenu = new ToolStripMenuItem("Dynamics");
            dynamicsMenu.DropDownItems.Add("pp", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("pp")));
            dynamicsMenu.DropDownItems.Add("p", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("p")));
            dynamicsMenu.DropDownItems.Add("mp", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mp")));
            dynamicsMenu.DropDownItems.Add("mf", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("mf")));
            dynamicsMenu.DropDownItems.Add("f", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("f")));
            dynamicsMenu.DropDownItems.Add("ff", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.SetDynamic("ff")));
            dynamicsMenu.DropDownItems.Add("Crescendo (selected notes)", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(true)));
            dynamicsMenu.DropDownItems.Add("Diminuendo (selected notes)", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.AddHairpin(false)));
            dynamicsMenu.DropDownItems.Add("Remove Hairpin", null, (s, e) => ExecuteScoreEdit(() => _viewModel.DynamicsEditor.RemoveHairpin()));
            menu.Items.Add(dynamicsMenu);

            AddPasteItemIfAvailable(menu);
        }

        private void AddGapContextMenuItems(ContextMenuStrip menu)
        {
            var insertMenu = new ToolStripMenuItem("Insert Note");
            for (var pitch = 1; pitch <= 7; pitch++)
            {
                var capturedPitch = pitch;
                insertMenu.DropDownItems.Add(pitch.ToString(), null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.AddNote(capturedPitch)));
            }

            menu.Items.Add(insertMenu);
            menu.Items.Add("Insert Rest", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.AddRest()));
            menu.Items.Add("Insert Continuation Dot", null, (s, e) => ExecuteNoteEdit(() => _viewModel.NoteEditor.AddContinuationDot()));
            AddPasteItemIfAvailable(menu);
        }

        private void AddChordContextMenuItems(ContextMenuStrip menu)
        {
            if (_viewModel.Selection.HasChordSelected)
            {
                menu.Items.Add("Delete Chord Marker", null, (s, e) => ExecuteDelete());
            }

            menu.Items.Add("Add Chord Marker Here", null, (s, e) => ExecuteScoreEdit(() => _viewModel.ChordEditor.AddChordMarker()));
        }

        private void AddMeasureContextMenuItems(ContextMenuStrip menu)
        {
            menu.Items.Add("Add Measure", null, (s, e) => ExecuteAddMeasure());
            menu.Items.Add("Duplicate Measure(s)", null, (s, e) => ExecuteDuplicateMeasures());
            AddPasteItemIfAvailable(menu);
        }

        private void AddPasteItemIfAvailable(ContextMenuStrip menu)
        {
            if (!_viewModel.NoteEditor.HasClipboardContent)
            {
                return;
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Paste", null, (s, e) => ExecutePaste());
        }

        private void ExecuteUndo()
        {
            if (_isExecutingHistoryChange || !_commandHistory.CanUndo)
            {
                return;
            }

            ExecuteHistoryChange(() =>
            {
                _commandHistory.Undo();
                _viewModel.SetStatus("Undone");
            });
        }

        private void ExecuteRedo()
        {
            if (_isExecutingHistoryChange || !_commandHistory.CanRedo)
            {
                return;
            }

            ExecuteHistoryChange(() =>
            {
                _commandHistory.Redo();
                _viewModel.SetStatus("Redone");
            });
        }

        private void ExecuteHistoryChange(Action changeAction)
        {
            _isExecutingHistoryChange = true;
            try
            {
                changeAction();
                _glue.SyncAfterHistoryChange(CreateHistoryRefreshResult());
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
                UpdateUndoMenuState();
            }
            catch (Exception ex)
            {
                AppLog.Exception("Undo/redo failed", ex);
                _viewModel.SetStatus("Undo/redo failed: " + ex.Message);
            }
            finally
            {
                _isExecutingHistoryChange = false;
            }
        }

        private bool ShouldDeferUndoRedoToTextInput()
        {
            return ActiveControl is TextBox;
        }

        private ScoreEditResult CreateHistoryRefreshResult()
        {
            var measureIndex = _viewModel.MeasureNavigation.CurrentMeasureIndex;
            return new ScoreEditResult
            {
                Changed = true,
                RequiresScoreRefresh = true,
                SelectMeasureIndex = measureIndex,
                ClearMelodySelection = true,
                ClearTieSelection = true,
                ClearChordSelection = true
            };
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                if (!ShouldDeferUndoRedoToTextInput())
                {
                    ExecuteUndo();
                    e.Handled = true;
                }

                return;
            }

            if (e.Control && e.KeyCode == Keys.Y)
            {
                if (!ShouldDeferUndoRedoToTextInput())
                {
                    ExecuteRedo();
                    e.Handled = true;
                }

                return;
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.N)
            {
                ExecuteAddMeasureWithPlaceholders();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.W)
            {
                CloseTab(ActiveTab);
                e.Handled = true;
                return;
            }

            if (IsTextInputFocused())
            {
                return;
            }

            if (e.KeyCode == Keys.Escape && _viewModel.TieEditor.IsTieModeActive)
            {
                _viewModel.TieEditor.CancelTieModeCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
            {
                ExecuteDelete();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.X)
            {
                ExecuteCut();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                ExecuteCopy();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                ExecutePaste();
                e.Handled = true;
            }
        }

        private bool IsTextInputFocused()
        {
            var active = ActiveControl;
            return active is TextBox || active is NumericUpDown;
        }

        private void OnMeasureSelectorChanged(object sender, EventArgs e)
        {
            if (_binder.SuppressMeasureSelectorSync)
            {
                return;
            }

            var result = _viewModel.MeasureNavigation.SelectMeasure((int)_measureSelector.Value - 1);
            _glue.ApplyEditResult(result);
            _binder.SyncFromViewModels();
        }

        private void OnMeasureRangeChanged(object sender, EventArgs e)
        {
            if (_binder.SuppressMeasureRangeSync)
            {
                return;
            }

            var normalized = _viewModel.MeasureNavigation.NormalizeMeasureRange(
                (int)_measureRangeFrom.Value,
                (int)_measureRangeTo.Value,
                sender == _measureRangeFrom);
            var result = _viewModel.MeasureNavigation.ApplyMeasureRange(normalized.fromIndex, normalized.toIndex);
            _glue.ApplyEditResult(result);
            _binder.SyncFromViewModels();
        }

        private void OnChordTextChanged(object sender, EventArgs e)
        {
            if (_binder.SuppressMeasureTextSync)
            {
                return;
            }

            _viewModel.ChordEditor.SelectedChordText = _chordBox.Text;
            _canvas.UpdateSelectedChordText(_chordBox.Text);
        }

        private void OnCanvasHeaderEdited(object sender, ScoreHeaderEditedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            _viewModel.Document.ApplyHeaderFieldEdit(e.Field, e.Text ?? string.Empty);
            _binder.SyncHeaderFromDocument();
            _binder.SyncFromViewModels();
        }

        private void OnCanvasSelectionChanged(object sender, ScoreSelectionChangedEventArgs e)
        {
            if (e.MeasureIndex < 0)
            {
                return;
            }

            _viewModel.HandleSelectionChanged(ScoreSelectionMapper.FromCanvas(e));
            _binder.SyncFromViewModels();
        }

        private void OnCanvasScoreMutationStarting(object sender, EventArgs e)
        {
            if (_suppressCanvasMutationTracking)
            {
                return;
            }

            _mutationBeforeSnapshot = ScoreCloneService.Clone(_viewModel.Document.Score);
            _mutationBeforeMeasureIndex = _viewModel.MeasureNavigation.CurrentMeasureIndex;
        }

        private void OnCanvasChordMarkersChanged(object sender, EventArgs e)
        {
            if (!_suppressCanvasMutationTracking)
            {
                CommitCanvasMutationCommand("Updated chord marker");
            }

            _viewModel.ChordEditor.SyncFromSelection();
            _binder.SyncMeasureTextBoxes();
        }

        private void OnCanvasMeasureTextEdited(object sender, EventArgs e)
        {
            if (!_suppressCanvasMutationTracking)
            {
                CommitCanvasMutationCommand("Updated measure text");
            }
            if (_canvas.SelectedMeasureIndex >= 0)
            {
                var result = _viewModel.MeasureContent.NotifyInlineLyricEdited(_canvas.SelectedMeasureIndex);
                _glue.ApplyEditResult(result);
                _binder.SyncFromViewModels();
            }
        }

        private void DiscardPendingCanvasMutation()
        {
            _mutationBeforeSnapshot = null;
            _mutationBeforeMeasureIndex = -1;
        }

        private void OnDocumentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScoreDocumentViewModel.Score))
            {
                DiscardPendingCanvasMutation();
            }
        }

        private void CommitCanvasMutationCommand(string description)
        {
            if (_mutationBeforeSnapshot == null)
            {
                _viewModel.NotifyScoreEdited(description, markDirty: true);
                return;
            }

            if (ScoreCloneService.AreEquivalent(_mutationBeforeSnapshot, _viewModel.Document.Score))
            {
                _mutationBeforeSnapshot = null;
                _mutationBeforeMeasureIndex = -1;
                _viewModel.NotifyScoreEdited(description, markDirty: true);
                return;
            }

            var command = new ScoreStateCommand(
                _viewModel.Document,
                _viewModel.MeasureNavigation,
                _messenger,
                _mutationBeforeSnapshot,
                _viewModel.Document.Score,
                _mutationBeforeMeasureIndex,
                _viewModel.MeasureNavigation.CurrentMeasureIndex,
                description);
            _commandHistory.Execute(command);
            _mutationBeforeSnapshot = null;
            _mutationBeforeMeasureIndex = -1;
            UpdateUndoMenuState();
        }

        private void OnClearScore(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear the current score?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _viewModel.Playback.Stop();
            _viewModel.TieEditor.CancelTieMode();
            var result = _viewModel.ScoreEditor.ClearScore();
            if (!result.Changed)
            {
                return;
            }

            _glue.ApplyEditResult(result);
            _glue.ResetPlaybackHead();
            _binder.SyncFromViewModels();
        }

        private void OnNewScore(object sender, EventArgs e)
        {
            var tab = CreateTab();
            tab.ViewModel.NewScoreCommand.Execute(null);
            tab.Glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
            tab.Glue.ResetPlaybackHead();
            _binder.SyncHeaderFromDocument();
            _binder.SyncFromViewModels();
        }

        private void OnImportMidi(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "MIDI Files (*.mid)|*.mid|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                DocumentTab tab = null;
                try
                {
                    // Reads the file's track list without touching any tab's document, so a new
                    // tab is only created once the user has actually committed to an import
                    // (picked a track, or the file has just one) rather than left behind empty
                    // if they cancel the track picker.
                    int? trackIndex = null;
                    var trackInfos = _viewModel.GetMidiTrackInfos(dialog.FileName);
                    if (trackInfos.Count > 1)
                    {
                        using (var picker = new MidiTrackPickerDialog(trackInfos))
                        {
                            if (picker.ShowDialog(this) != DialogResult.OK)
                            {
                                return;
                            }

                            trackIndex = picker.SelectedTrackIndex;
                        }
                    }

                    tab = CreateTab();
                    var result = tab.ViewModel.ImportMidi(dialog.FileName, trackIndex);
                    Text = tab.ViewModel.Document.WindowTitle;
                    tab.Glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                    tab.Glue.ResetPlaybackHead();
                    _binder.SyncHeaderFromDocument();
                    _binder.SyncFromViewModels();
                    tab.ViewModel.SetStatus(result.Message);
                }
                catch (Exception ex)
                {
                    AppLog.Exception("MIDI import failed: " + dialog.FileName, ex);
                    if (tab != null)
                    {
                        DiscardTab(tab);
                    }

                    MessageBox.Show("MIDI import failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void OnImportAudio(object sender, EventArgs e, AudioTranscriptionEngine engine)
        {
            var engineLabel = engine == AudioTranscriptionEngine.Vocal ? "Vocal (GAME)" : "Instrument (basic-pitch)";

            if (!TryShowEngineSettingsDialog(engine))
            {
                return;
            }

            using (var dialog = new OpenFileDialog
            {
                Title = "Import from Audio (" + engineLabel + ")",
                Filter = "Audio Files (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var fileName = dialog.FileName;
                var tab = CreateTab();

                // Transcription runs several seconds to a few minutes depending on the engine and
                // clip length; running it on the UI thread froze the window ("Not Responding") for
                // that whole time with no feedback. Task.Run keeps the UI pumping messages while
                // Progress<string> (captures this thread's SynchronizationContext) marshals status
                // updates back safely. The whole window (not just this tab) is disabled for the
                // duration, so tab.ViewModel below can't drift from whatever's active mid-await.
                using (var progressDialog = new AudioImportProgressDialog("Import from Audio (" + engineLabel + ")"))
                {
                    progressDialog.Show(this);
                    Enabled = false;
                    var progress = new Progress<string>(message =>
                    {
                        progressDialog.SetMessage(message);
                        tab.ViewModel.SetStatus(message);
                    });

                    try
                    {
                        var result = await Task.Run(() => tab.ViewModel.ImportAudio(fileName, engine, _lastBasicPitchSettings, _lastGameSettings, progress));
                        Text = tab.ViewModel.Document.WindowTitle;
                        tab.Glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                        tab.Glue.ResetPlaybackHead();
                        _binder.SyncHeaderFromDocument();
                        _binder.SyncFromViewModels();
                        tab.ViewModel.SetStatus(result.Message);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Exception("Audio import failed: " + fileName, ex);
                        DiscardTab(tab);
                        MessageBox.Show("Audio import failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        Enabled = true;
                        progressDialog.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Shows the engine-specific "Advanced settings" prompt before every audio import (per
        /// user preference -- the values shown default to whatever was used last, but the dialog
        /// itself is never skipped). Returns false if the user cancelled, in which case the caller
        /// should abort the import entirely without opening the file picker.
        /// </summary>
        private bool TryShowEngineSettingsDialog(AudioTranscriptionEngine engine)
        {
            IReadOnlyList<ImportParameter> parameters;
            if (engine == AudioTranscriptionEngine.Vocal)
            {
                parameters = new[]
                {
                    new ImportParameter("Segmentation threshold", (decimal)_lastGameSettings.SegThreshold, 0.05m, 0.95m, 0.05m, 2),
                    new ImportParameter("Segmentation radius (frames)", _lastGameSettings.SegRadiusFrames, 0, 10, 1, 0),
                    new ImportParameter("Note-presence threshold", (decimal)_lastGameSettings.EstThreshold, 0.05m, 0.95m, 0.05m, 2),
                    new ImportParameter("Vibrato smoothing min (seconds)", (decimal)_lastGameSettings.MinVibratoSmoothingSeconds, 0.0m, 0.5m, 0.01m, 2),
                    new ImportParameter("Vibrato smoothing max (seconds, 0=off)", (decimal)_lastGameSettings.MaxVibratoSmoothingSeconds, 0.0m, 0.5m, 0.01m, 2),
                    new ImportParameter("Onset quantize grid (quarter notes, 0=off)", (decimal)_lastGameSettings.OnsetQuantizeGrid, 0.0m, 1.0m, 0.25m, 2)
                };
            }
            else
            {
                parameters = new[]
                {
                    new ImportParameter("Onset threshold", (decimal)_lastBasicPitchSettings.OnsetThreshold, 0.05m, 0.95m, 0.05m, 2),
                    new ImportParameter("Frame threshold", (decimal)_lastBasicPitchSettings.FrameThreshold, 0.05m, 0.95m, 0.05m, 2),
                    new ImportParameter("Minimum note length (frames)", _lastBasicPitchSettings.MinNoteLenFrames, 1, 60, 1, 0),
                    new ImportParameter("Merge gap (seconds)", (decimal)_lastBasicPitchSettings.MergeGapSeconds, 0.0m, 0.5m, 0.01m, 2),
                    new ImportParameter("Minimum amplitude", (decimal)_lastBasicPitchSettings.MinAmplitude, 0.0m, 0.95m, 0.05m, 2),
                    new ImportParameter("Onset quantize grid (quarter notes, 0=off)", (decimal)_lastBasicPitchSettings.OnsetQuantizeGrid, 0.0m, 1.0m, 0.25m, 2)
                };
            }

            var engineLabel = engine == AudioTranscriptionEngine.Vocal ? "Vocal (GAME)" : "Instrument (basic-pitch)";
            using (var dialog = new AudioImportSettingsDialog("Audio Import Settings (" + engineLabel + ")", parameters))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                dialog.ApplyValues();
            }

            if (engine == AudioTranscriptionEngine.Vocal)
            {
                _lastGameSettings.SegThreshold = (float)parameters[0].Value;
                _lastGameSettings.SegRadiusFrames = (long)parameters[1].Value;
                _lastGameSettings.EstThreshold = (float)parameters[2].Value;
                _lastGameSettings.MinVibratoSmoothingSeconds = (double)parameters[3].Value;
                _lastGameSettings.MaxVibratoSmoothingSeconds = (double)parameters[4].Value;
                _lastGameSettings.OnsetQuantizeGrid = (double)parameters[5].Value;
            }
            else
            {
                _lastBasicPitchSettings.OnsetThreshold = (float)parameters[0].Value;
                _lastBasicPitchSettings.FrameThreshold = (float)parameters[1].Value;
                _lastBasicPitchSettings.MinNoteLenFrames = (int)parameters[2].Value;
                _lastBasicPitchSettings.MergeGapSeconds = (double)parameters[3].Value;
                _lastBasicPitchSettings.MinAmplitude = (float)parameters[4].Value;
                _lastBasicPitchSettings.OnsetQuantizeGrid = (double)parameters[5].Value;
            }

            return true;
        }

        private void OnOpenScore(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Jianpu Files (*.jianpu)|*.jianpu|JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var tab = CreateTab();
                tab.ViewModel.Document.LoadFromFile(dialog.FileName);
                tab.Glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                tab.Glue.ResetPlaybackHead();
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
                tab.ViewModel.SetStatus("Opened: " + dialog.FileName);
            }
        }

        private void OnSaveScore(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.Document.CurrentFilePath))
            {
                OnSaveScoreAs(sender, e);
                return;
            }

            _viewModel.Document.SaveToFile(_viewModel.Document.CurrentFilePath);
            _viewModel.SetStatus("Saved: " + _viewModel.Document.CurrentFilePath);
        }

        private void OnSaveScoreAs(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "Jianpu Files (*.jianpu)|*.jianpu|JSON Files (*.json)|*.json",
                FileName = string.IsNullOrWhiteSpace(_viewModel.Document.Title)
                    ? "New Score.jianpu"
                    : _viewModel.Document.Title + ".jianpu"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                _viewModel.Document.SaveToFile(dialog.FileName);
                _binder.SyncHeaderFromDocument();
                _viewModel.SetStatus("Saved: " + dialog.FileName);
            }
        }

        private void OnExportPdf(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = string.IsNullOrWhiteSpace(_viewModel.Document.Title)
                    ? "Score.pdf"
                    : _viewModel.Document.Title + ".pdf"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    _viewModel.ExportPdf(dialog.FileName, Math.Max(Width - 40, 900));
                    MessageBox.Show("PDF exported successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnExportMidi(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "MIDI Files (*.mid)|*.mid",
                FileName = string.IsNullOrWhiteSpace(_viewModel.Document.Title)
                    ? "Score.mid"
                    : _viewModel.Document.Title + ".mid"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    _viewModel.ExportMidi(dialog.FileName);
                    MessageBox.Show("MIDI exported successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PopulateSampleLibraryMenu(ToolStripItemCollection items)
        {
            items.Clear();
            items.Add(CreateMenuItem("Ode to Joy (built-in)", Keys.None, (s, e) => LoadDemoScore()));
            items.Add(new ToolStripSeparator());

            // Uses the directly-injected singleton, not _viewModel.SampleLibrary (same object,
            // but this menu is built once during SetupLayoutStructure, before any tab -- and
            // therefore ActiveTab -- exists yet).
            _sampleLibrary.RefreshSamples();
            if (!_sampleLibrary.HasSamples)
            {
                items.Add(new ToolStripMenuItem("(no files in the sample directory yet)") { Enabled = false });
                return;
            }

            foreach (var sampleFile in _sampleLibrary.Samples)
            {
                var path = sampleFile;
                var label = _sampleLibrary.GetDisplayName(sampleFile);
                items.Add(CreateMenuItem(label, Keys.None, (s, e) => LoadSampleScore(path)));
            }
        }

        private void LoadSampleScore(string path)
        {
            var tab = CreateTab();
            try
            {
                var result = tab.ViewModel.SampleLibrary.LoadSample(tab.ViewModel.Document, tab.Messenger, path);
                Text = tab.ViewModel.SampleLibrary.BuildWindowTitle(tab.ViewModel.Document, path);
                tab.Glue.ApplyEditResult(result);
                tab.Glue.ResetPlaybackHead();
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to load sample score: " + path, ex);
                DiscardTab(tab);
                MessageBox.Show("Failed to load sample score: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDemoScore()
        {
            var tab = CreateTab();
            var result = tab.ViewModel.SampleLibrary.LoadDemoScore(tab.ViewModel.Document, tab.Messenger);
            tab.Glue.ApplyEditResult(result);
            tab.Glue.ResetPlaybackHead();
            _binder.SyncHeaderFromDocument();
            _binder.SyncFromViewModels();
        }

        private void OnPlayScore()
        {
            try
            {
                var startQuarter = _canvas.PlaybackPositionQuarter;
                _viewModel.Playback.Play(startQuarter);
                _canvas.SetPlaybackPosition(startQuarter, showHead: true, ensureVisible: true);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Play button click failed", ex);
                ShowPlaybackError("Playback failed", ex);
            }
        }

        private void OnStopPlayback()
        {
            _viewModel.Playback.StopCommand.Execute(null);
        }

        private void OnCanvasPlaybackSeeked(double quarterBeat)
        {
            try
            {
                _viewModel.Playback.Seek(quarterBeat);
                if (!_viewModel.Playback.IsPlaying)
                {
                    _canvas.SetPlaybackPosition(quarterBeat, showHead: true, ensureVisible: true);
                }
            }
            catch (Exception ex)
            {
                AppLog.Exception("Dragging playback position failed", ex);
                ShowPlaybackError("Playback seek failed", ex);
            }
        }

        private static void ShowPlaybackError(string title, Exception ex)
        {
            var message = ex == null
                ? title
                : title + Environment.NewLine + Environment.NewLine +
                  ex.Message + Environment.NewLine + Environment.NewLine +
                  "Detailed log:" + Environment.NewLine + AppLog.LogFilePath;
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            AppLog.Info("Jianpu Editor exiting");
            _binder?.Dispose();
            foreach (TabPage page in _tabControl.TabPages)
            {
                (page.Tag as DocumentTab)?.Dispose();
            }
        }

        private void ShowHarmonySuggestionDialog()
        {
            _viewModel.Document.EnsureMeasures();
            if (_viewModel.Selection.TryGetContiguousMeasureRange(out var fromIndex, out var toIndex))
            {
                ShowHarmonyProgressionSuggestionDialog(fromIndex, toIndex);
                return;
            }

            var measureIndex = _viewModel.Selection.HasChordSelected
                ? _viewModel.Selection.ChordMeasureIndex
                : Math.Max(0, _viewModel.Selection.MeasureIndex);
            if (measureIndex < 0)
            {
                measureIndex = 0;
            }

            var beatPosition = _viewModel.ChordEditor.ResolveSuggestionBeat(measureIndex);
            var suggestions = _viewModel.ChordEditor.GetHarmonySuggestions(measureIndex, beatPosition);
            if (suggestions == null || suggestions.Count == 0)
            {
                MessageBox.Show("No chord suggestions can be generated at the current position.", "Chord Suggestion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new Views.HarmonySuggestionDialog(
                measureIndex + 1,
                beatPosition,
                _viewModel.Document.KeySignature,
                suggestions))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedSuggestion == null)
                {
                    return;
                }

                ExecuteScoreEdit(() => _viewModel.ChordEditor.ApplyHarmonySuggestion(
                    measureIndex,
                    beatPosition,
                    dialog.SelectedSuggestion.ChordSymbol));
            }
        }

        private void ShowHarmonyProgressionSuggestionDialog(int fromIndex, int toIndex)
        {
            var suggestions = _viewModel.ChordEditor.GetHarmonyProgressionSuggestions(fromIndex, toIndex);
            if (suggestions == null || suggestions.Count == 0)
            {
                MessageBox.Show("No chord progression suggestions can be generated for the current selection.", "Chord Suggestion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new Views.HarmonyProgressionSuggestionDialog(
                fromIndex + 1,
                toIndex + 1,
                _viewModel.Document.KeySignature,
                suggestions))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedSuggestion == null)
                {
                    return;
                }

                ExecuteScoreEdit(() => _viewModel.ChordEditor.ApplyHarmonyProgressionSuggestion(
                    dialog.SelectedSuggestion,
                    fromIndex));
            }
        }

        private void ShowBulkLyricEditDialog()
        {
            _viewModel.Document.EnsureMeasures();
            var measureCount = Math.Max(1, _viewModel.Document.Score.Measures.Count);
            var range = _viewModel.Selection.GetMeasureRangeIndices();
            var fromMeasure = Math.Max(1, Math.Min(measureCount, range.fromIndex + 1));
            var toMeasure = Math.Max(1, Math.Min(measureCount, range.toIndex + 1));

            using (var dialog = new BulkLyricEditDialog(
                measureCount,
                fromMeasure,
                toMeasure,
                (fromIndex, toIndex) => _viewModel.MeasureContent.GetLyricTextsForRange(fromIndex, toIndex)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ExecuteScoreEdit(() => _viewModel.MeasureContent.ApplyBulkLyrics(
                    dialog.FromMeasure - 1,
                    dialog.ToMeasure - 1,
                    dialog.LyricLines,
                    dialog.Realign));
            }
        }

        private void ShowTransposeDialog()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Chord Transpose";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(380, 156);
                dialog.Font = Font;

                var sourceLabel = new Label { Text = "Current Key:", Location = new Point(16, 18), AutoSize = true };
                var sourceValue = new Label
                {
                    Text = _viewModel.Document.KeySignature ?? "1=C",
                    Location = new Point(108, 18),
                    AutoSize = true
                };
                var targetLabel = new Label { Text = "Target Key:", Location = new Point(16, 54), AutoSize = true };
                var targetBox = new TextBox
                {
                    Location = new Point(108, 50),
                    Width = 240,
                    Text = _viewModel.Document.KeySignature ?? "1=C"
                };
                var hintLabel = new Label
                {
                    Text = "Supported formats: G, 1=G, F#, Bb, D major. Only transposes chord markers in the secondary melody.",
                    Location = new Point(16, 84),
                    Size = new Size(348, 32),
                    ForeColor = Color.DimGray
                };
                var okButton = new Button { Text = "Convert", DialogResult = DialogResult.OK, Location = new Point(192, 118), Width = 76 };
                var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(276, 118), Width = 76 };

                dialog.Controls.Add(sourceLabel);
                dialog.Controls.Add(sourceValue);
                dialog.Controls.Add(targetLabel);
                dialog.Controls.Add(targetBox);
                dialog.Controls.Add(hintLabel);
                dialog.Controls.Add(okButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var targetKey = targetBox.Text?.Trim();
                if (string.IsNullOrEmpty(targetKey))
                {
                    MessageBox.Show("Please enter a target key.", "Transpose", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = _viewModel.ChordEditor.TransposeChords(targetKey);
                if (!result.Changed)
                {
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        MessageBox.Show(result.Message, "Transpose Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    return;
                }

                _glue.ApplyEditResult(result);
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
            }
        }

        private void ShowInstrumentDialog()
        {
            using (var dialog = new InstrumentDialog(_viewModel.Document.MelodyInstrument, _viewModel.Document.ChordInstrument))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _viewModel.Document.ApplyInstrumentEdit(false, dialog.SelectedMelodyInstrument);
                _viewModel.Document.ApplyInstrumentEdit(true, dialog.SelectedChordInstrument);
            }
        }

        private void ShowAudioEngineDialog()
        {
            using (var dialog = new AudioEngineDialog(AppTheme.CustomSoundFontPath, _midiOutput.EngineName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var selectedSoundFontPath = dialog.SelectedSoundFontPath;
                if (!string.IsNullOrWhiteSpace(selectedSoundFontPath) && !File.Exists(selectedSoundFontPath))
                {
                    MessageBox.Show("Instrument file not found: " + selectedSoundFontPath, "Audio Engine", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (AppTheme.CustomSoundFontPath == selectedSoundFontPath)
                {
                    return;
                }

                AppTheme.SetCustomSoundFontPath(selectedSoundFontPath);
                MessageBox.Show(
                    "Audio engine setting saved. Restart Jianpu Editor for this to take effect.",
                    "Audio Engine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
