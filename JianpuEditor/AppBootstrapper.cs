using System;
using System.IO;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Services;
using JianpuEditor.ViewModels;
using JianpuEditor.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JianpuEditor
{
    internal static class AppBootstrapper
    {
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAppMessenger, AppMessenger>();
            services.AddSingleton<IScoreFileService, ScoreFileServiceAdapter>();
            services.AddSingleton<IScoreUndoService, ScoreUndoService>();
            services.AddSingleton<IEditCommandHistory, EditCommandHistory>();
            services.AddSingleton<IMidiOutput>(_ => CreateMidiOutput());
            services.AddSingleton<IScorePlaybackService, ScorePlaybackService>();
            services.AddSingleton<IPdfExportService, PdfExportServiceAdapter>();
            services.AddSingleton<IMidiExportService, MidiExportServiceAdapter>();
            services.AddSingleton<IMidiImportService, MidiImportServiceAdapter>();
            services.AddSingleton<ISampleLibraryService, SampleLibraryServiceAdapter>();
            services.AddSingleton<IChordTransposeService, ChordTransposeServiceAdapter>();
            services.AddSingleton<IHarmonySuggestionService, HarmonySuggestionServiceAdapter>();
            services.AddSingleton<ScoreDocumentViewModel>();
            services.AddSingleton<ScoreSelectionViewModel>();
            services.AddSingleton<NoteEditorViewModel>();
            services.AddSingleton<TieEditorViewModel>();
            services.AddSingleton<MeasureNavigationViewModel>();
            services.AddSingleton<MeasureContentViewModel>();
            services.AddSingleton<ChordEditorViewModel>();
            services.AddSingleton<OrnamentEditorViewModel>();
            services.AddSingleton<ScoreEditorViewModel>();
            services.AddSingleton<PlaybackViewModel>();
            services.AddSingleton<SampleLibraryViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ILayoutService, WinFormsLayoutService>();
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }

        private static IMidiOutput CreateMidiOutput()
        {
            var soundFontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Soundfonts", "GeneralUser-GS.sf2");
            try
            {
                return new BassMidiSynthesizer(soundFontPath);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to initialize BASSMIDI SoundFont synthesizer, falling back to system MIDI device", ex);
                return new WindowsMidiSynthesizer();
            }
        }
    }
}
