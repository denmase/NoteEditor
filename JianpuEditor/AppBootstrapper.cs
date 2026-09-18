using System;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Services.AudioToMidi;
using JianpuEditor.ViewModels;
using JianpuEditor.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JianpuEditor
{
    internal static class AppBootstrapper
    {
        // Returns the concrete ServiceProvider (not just IServiceProvider) so Program.cs can
        // dispose it in a using block -- that's what actually disposes IMidiOutput and other
        // singletons exactly once, at real application shutdown, now that ScorePlaybackService
        // (scoped, one per tab) no longer disposes the shared synthesizer itself.
        public static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Scoped: one instance per open document tab (see Glue/DocumentTab.cs). Each scope
            // gets its own IAppMessenger backed by a fresh WeakReferenceMessenger rather than the
            // shared WeakReferenceMessenger.Default -- otherwise an edit in one tab would still
            // broadcast to every tab's viewmodels app-wide (marking every tab dirty, clearing
            // every tab's undo history, stopping every tab's playback), defeating the point of
            // scoping the rest of this list.
            services.AddScoped<IAppMessenger>(_ => new AppMessenger(new WeakReferenceMessenger()));
            services.AddScoped<IEditCommandHistory, EditCommandHistory>();
            services.AddScoped<IScorePlaybackService, ScorePlaybackService>();
            services.AddScoped<ScoreDocumentViewModel>();
            services.AddScoped<ScoreSelectionViewModel>();
            services.AddScoped<NoteEditorViewModel>();
            services.AddScoped<TieEditorViewModel>();
            services.AddScoped<MeasureNavigationViewModel>();
            services.AddScoped<MeasureContentViewModel>();
            services.AddScoped<ChordEditorViewModel>();
            services.AddScoped<OrnamentEditorViewModel>();
            services.AddScoped<ScoreEditorViewModel>();
            services.AddScoped<PlaybackViewModel>();
            services.AddScoped<MainViewModel>();

            // Singleton: stateless, or a genuinely shared resource (IMidiOutput is the real
            // BASS/VST audio hardware handle and must not be duplicated per tab).
            services.AddSingleton<IScoreFileService, ScoreFileServiceAdapter>();
            services.AddSingleton<IMidiOutput>(_ => CreateMidiOutput());
            services.AddSingleton<IPdfExportService, PdfExportServiceAdapter>();
            services.AddSingleton<IMidiExportService, MidiExportServiceAdapter>();
            services.AddSingleton<IMidiImportService, MidiImportServiceAdapter>();
            services.AddSingleton<IAudioImportService, AudioImportService>();
            services.AddSingleton<ISampleLibraryService, SampleLibraryServiceAdapter>();
            services.AddSingleton<IChordTransposeService, ChordTransposeServiceAdapter>();
            services.AddSingleton<IHarmonySuggestionService, HarmonySuggestionServiceAdapter>();
            services.AddSingleton<SampleLibraryViewModel>();
            services.AddSingleton<ILayoutService, WinFormsLayoutService>();
            services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }

        private static IMidiOutput CreateMidiOutput()
        {
            var vstPluginPath = AppTheme.VstPluginPath;
            if (!string.IsNullOrWhiteSpace(vstPluginPath))
            {
                try
                {
                    return new BassVstSynthesizer(vstPluginPath);
                }
                catch (Exception ex)
                {
                    AppLog.Exception("Failed to initialize BASSVST plugin '" + vstPluginPath + "', falling back to bundled SoundFont", ex);
                }
            }

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
