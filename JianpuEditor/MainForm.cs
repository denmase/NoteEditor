using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

namespace JianpuEditor
{
    public sealed partial class MainForm : Form, IView
    {
        private readonly MainViewModel _viewModel;
        private readonly IAppMessenger _messenger;
        private readonly ILayoutService _layoutService;
        private readonly IEditCommandHistory _commandHistory;
        private readonly IMidiOutput _midiOutput;
        private readonly BasicPitchSettings _lastBasicPitchSettings = BasicPitchSettings.CreateDefault();
        private readonly GameSettings _lastGameSettings = GameSettings.CreateDefault();
        private JianpuScore _mutationBeforeSnapshot;
        private int _mutationBeforeMeasureIndex = -1;
        private bool _suppressCanvasMutationTracking;
        private ScoreCanvasGlue _glue;
        private MainFormViewBinder _binder;
        private MainFormLayoutContext _layoutContext;
        private TableLayoutPanel _mainLayout;
        private TableLayoutPanel _chromeLayout;
        private readonly ScoreCanvas _canvas = new ScoreCanvas();
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
        private ToolStripMenuItem _underlinesAboveMenuItem;
        private ToolStripMenuItem _undoMenuItem;
        private ToolStripMenuItem _redoMenuItem;
        private readonly ToolTip _toolTip = new ToolTip();
        private bool _isExecutingHistoryChange;

        private const string NoteButtonToolTip =
            "Click: insert or modify a note at the selected position\r\nCtrl+Click: append to the end of the current measure\r\nCtrl+Shift+Click: append and copy the previous note's duration/octave";

        public MainForm(
            MainViewModel viewModel,
            IAppMessenger messenger,
            ILayoutService layoutService,
            IEditCommandHistory commandHistory,
            IMidiOutput midiOutput)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
            _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            _midiOutput = midiOutput ?? throw new ArgumentNullException(nameof(midiOutput));
            _commandHistory.HistoryChanged += (s, e) => UpdateUndoMenuState();

            InitializeComponent();
            SetupLayoutStructure();

            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            Load += OnFormLoad;
            Resize += OnFormResize;
            FormClosed += OnFormClosed;
        }

        public void InitializeBindings(object viewModel)
        {
            if (viewModel is not MainViewModel mainViewModel)
            {
                throw new ArgumentException("MainForm requires MainViewModel.", nameof(viewModel));
            }

            _binder = new MainFormViewBinder(
                mainViewModel,
                this,
                _chordBox,
                _measureSelector,
                _measureRangeFrom,
                _measureRangeTo,
                _statusBar,
                _tieButton,
                _playButton,
                _stopButton);

            _glue = new ScoreCanvasGlue(mainViewModel, _canvas, _messenger);
            mainViewModel.Document.PropertyChanged += OnDocumentPropertyChanged;

            _canvas.SelectionChanged += OnCanvasSelectionChanged;
            _canvas.MeasureTextEdited += OnCanvasMeasureTextEdited;
            _canvas.HeaderEdited += OnCanvasHeaderEdited;
            _canvas.ChordMarkersChanged += OnCanvasChordMarkersChanged;
            _canvas.ScoreMutationStarting += OnCanvasScoreMutationStarting;
            _canvas.PlaybackSeeked += OnCanvasPlaybackSeeked;

            mainViewModel.RequestOpenScore += (s, e) => OnOpenScore(s, e);
            mainViewModel.RequestSaveScore += (s, e) => OnSaveScore(s, e);
            mainViewModel.RequestSaveAsScore += (s, e) => OnSaveScoreAs(s, e);
            mainViewModel.RequestExportPdf += (s, e) => OnExportPdf(s, e);
            mainViewModel.RequestExportMidi += (s, e) => OnExportMidi(s, e);
            mainViewModel.RequestImportMidi += (s, e) => OnImportMidi(s, e);
            mainViewModel.RequestImportAudioInstrument += (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Instrument);
            mainViewModel.RequestImportAudioVocal += (s, e) => OnImportAudio(s, e, AudioTranscriptionEngine.Vocal);
            mainViewModel.RequestTransposeDialog += (s, e) => ShowTransposeDialog();
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

            _canvas.Dock = DockStyle.Fill;
            _canvas.MinimumSize = new Size(200, 200);
            _statusBar.Dock = DockStyle.Fill;
            _statusBar.MinimumSize = new Size(0, MainFormLayoutContext.StatusRowHeight);
            _statusBar.ZoomInClicked += () => _canvas.ZoomIn();
            _statusBar.ZoomOutClicked += () => _canvas.ZoomOut();
            _statusBar.EngineClicked += (s, e) => ShowAudioEngineDialog();
            _statusBar.SetEngine(_midiOutput.EngineName);
            _statusBar.SetZoomPercent((int)System.Math.Round(_canvas.ZoomScale * 100));
            _canvas.ZoomChanged += () => _statusBar.SetZoomPercent((int)System.Math.Round(_canvas.ZoomScale * 100));

            _mainLayout.Controls.Add(_chromeLayout, 0, 0);
            _mainLayout.Controls.Add(_canvas, 0, 1);
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
                ScoreCanvas = _canvas,
                ScoreStatusBar = _statusBar
            };
            _layoutService.Attach(_layoutContext);
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            InitializeBindings(_viewModel);
            RestoreLayout();
            ApplyDpiScaling();
            ApplyTheme();

            AppLog.Info("Jianpu Editor started");

            var demoResult = _viewModel.SampleLibrary.LoadDemoScore();
            _glue.ApplyEditResult(demoResult);
            _glue.ResetPlaybackHead();
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
            fileMenu.DropDownItems.Add(CreateMenuItem("Exit", Keys.None, (s, e) => Close()));

            var editMenu = new ToolStripMenuItem("Edit");
            _undoMenuItem = CreateMenuItem("Undo", Keys.Control | Keys.Z, (s, e) => ExecuteUndo());
            _undoMenuItem.Enabled = false;
            editMenu.DropDownItems.Add(_undoMenuItem);
            _redoMenuItem = CreateMenuItem("Redo", Keys.Control | Keys.Y, (s, e) => ExecuteRedo());
            _redoMenuItem.Enabled = false;
            editMenu.DropDownItems.Add(_redoMenuItem);
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
                "Fermata",
                Keys.None,
                (s, e) => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fermata))));
            editMenu.DropDownItems.Add(ornamentMenu);
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
            _underlinesAboveMenuItem = new ToolStripMenuItem("Beams Above Notes (Indonesian Jianpu style)")
            {
                CheckOnClick = true,
                Checked = AppTheme.UnderlinesAbove
            };
            _underlinesAboveMenuItem.CheckedChanged += OnUnderlinesAboveToggled;
            viewMenu.DropDownItems.Add(_underlinesAboveMenuItem);
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

            var notes = new RibbonGroup("Notes 1-7 . Rest");
            var noteButtons = new Control[8];
            for (var pitch = 1; pitch <= 7; pitch++)
            {
                noteButtons[pitch - 1] = CreateNoteButton(pitch);
            }

            noteButtons[7] = CreateRestButton();
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
                CreateRibbonButton(RibbonIcon.Fermata, "Fermata", () => ExecuteScoreEdit(() => _viewModel.OrnamentEditor.AddOrnament(OrnamentType.Fermata)), compact: true));
            panel.Controls.Add(ornaments);

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

        private void OnUnderlinesAboveToggled(object sender, EventArgs e)
        {
            AppTheme.SetUnderlinesAbove(_underlinesAboveMenuItem.Checked);
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
            _viewModel.NewScoreCommand.Execute(null);
            _glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
            _glue.ResetPlaybackHead();
            _binder.SyncHeaderFromDocument();
            _binder.SyncFromViewModels();
        }

        private void OnImportMidi(object sender, EventArgs e)
        {
            _viewModel.Playback.Stop();
            _viewModel.TieEditor.CancelTieMode();
            using (var dialog = new OpenFileDialog
            {
                Filter = "MIDI Files (*.mid)|*.mid|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
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

                    var result = _viewModel.ImportMidi(dialog.FileName, trackIndex);
                    Text = _viewModel.Document.WindowTitle;
                    _glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                    _glue.ResetPlaybackHead();
                    _binder.SyncHeaderFromDocument();
                    _binder.SyncFromViewModels();
                    _viewModel.SetStatus(result.Message);
                }
                catch (Exception ex)
                {
                    AppLog.Exception("MIDI import failed: " + dialog.FileName, ex);
                    MessageBox.Show("MIDI import failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void OnImportAudio(object sender, EventArgs e, AudioTranscriptionEngine engine)
        {
            _viewModel.Playback.Stop();
            _viewModel.TieEditor.CancelTieMode();
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

                // Transcription runs several seconds to a few minutes depending on the engine and
                // clip length; running it on the UI thread froze the window ("Not Responding") for
                // that whole time with no feedback. Task.Run keeps the UI pumping messages while
                // Progress<string> (captures this thread's SynchronizationContext) marshals status
                // updates back safely.
                using (var progressDialog = new AudioImportProgressDialog("Import from Audio (" + engineLabel + ")"))
                {
                    progressDialog.Show(this);
                    Enabled = false;
                    var progress = new Progress<string>(message =>
                    {
                        progressDialog.SetMessage(message);
                        _viewModel.SetStatus(message);
                    });

                    try
                    {
                        var result = await Task.Run(() => _viewModel.ImportAudio(fileName, engine, _lastBasicPitchSettings, _lastGameSettings, progress));
                        Text = _viewModel.Document.WindowTitle;
                        _glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                        _glue.ResetPlaybackHead();
                        _binder.SyncHeaderFromDocument();
                        _binder.SyncFromViewModels();
                        _viewModel.SetStatus(result.Message);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Exception("Audio import failed: " + fileName, ex);
                        _viewModel.SetStatus("Audio import failed");
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
            _viewModel.Playback.Stop();
            _viewModel.TieEditor.CancelTieMode();
            using (var dialog = new OpenFileDialog
            {
                Filter = "Jianpu Files (*.jianpu)|*.jianpu|JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                _viewModel.Document.LoadFromFile(dialog.FileName);
                _glue.ApplyEditResult(new ScoreEditResult { Changed = true, SelectMeasureIndex = 0 });
                _glue.ResetPlaybackHead();
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
                _viewModel.SetStatus("Opened: " + dialog.FileName);
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

            _viewModel.SampleLibrary.RefreshSamples();
            if (!_viewModel.SampleLibrary.HasSamples)
            {
                items.Add(new ToolStripMenuItem("(no files in the sample directory yet)") { Enabled = false });
                return;
            }

            foreach (var sampleFile in _viewModel.SampleLibrary.Samples)
            {
                var path = sampleFile;
                var label = _viewModel.SampleLibrary.GetDisplayName(sampleFile);
                items.Add(CreateMenuItem(label, Keys.None, (s, e) => LoadSampleScore(path)));
            }
        }

        private void LoadSampleScore(string path)
        {
            try
            {
                _viewModel.Playback.Stop();
                _viewModel.TieEditor.CancelTieMode();
                var result = _viewModel.SampleLibrary.LoadSample(path);
                Text = _viewModel.SampleLibrary.BuildWindowTitle(path);
                _glue.ApplyEditResult(result);
                _glue.ResetPlaybackHead();
                _binder.SyncHeaderFromDocument();
                _binder.SyncFromViewModels();
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to load sample score: " + path, ex);
                MessageBox.Show("Failed to load sample score: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDemoScore()
        {
            _viewModel.Playback.Stop();
            _viewModel.TieEditor.CancelTieMode();
            var result = _viewModel.SampleLibrary.LoadDemoScore();
            _glue.ApplyEditResult(result);
            _glue.ResetPlaybackHead();
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
            _glue?.Dispose();
            _binder?.Dispose();
            _viewModel.Dispose();
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
            using (var dialog = new AudioEngineDialog(AppTheme.VstPluginPath, _midiOutput.EngineName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var selectedPath = dialog.SelectedVstPluginPath;
                if (!string.IsNullOrWhiteSpace(selectedPath) && !File.Exists(selectedPath))
                {
                    MessageBox.Show("VST plugin file not found: " + selectedPath, "Audio Engine", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (AppTheme.VstPluginPath == selectedPath)
                {
                    return;
                }

                AppTheme.SetVstPluginPath(selectedPath);
                MessageBox.Show(
                    "Audio engine setting saved. Restart Jianpu Editor for this to take effect.",
                    "Audio Engine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
