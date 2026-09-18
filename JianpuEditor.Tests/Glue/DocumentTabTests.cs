using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Glue;
using JianpuEditor.Services;
using JianpuEditor.Tests.ViewModels;
using JianpuEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JianpuEditor.Tests.Glue
{
    public sealed class DocumentTabTests
    {
        [Fact]
        public void TwoTabs_GetIndependentViewModelsAndDocuments()
        {
            using var provider = BuildServiceProvider();
            using var tabA = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());
            using var tabB = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());

            Assert.NotSame(tabA.ViewModel, tabB.ViewModel);
            Assert.NotSame(tabA.ViewModel.Document, tabB.ViewModel.Document);
            Assert.NotSame(tabA.Messenger, tabB.Messenger);
            Assert.NotSame(tabA.Canvas, tabB.Canvas);
        }

        [Fact]
        public void TwoTabs_MessengersDoNotCrossTalk()
        {
            // Regression guard for the exact bug scoping IAppMessenger fixes: without a fresh
            // WeakReferenceMessenger per tab, editing one document would broadcast to every
            // other open tab's recipients too (marking them dirty, clearing their undo history,
            // stopping their playback).
            using var provider = BuildServiceProvider();
            using var tabA = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());
            using var tabB = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());

            var receivedByA = new List<string>();
            var receivedByB = new List<string>();
            tabA.Messenger.Register<object, StatusChangedMessage>(this, (r, m) => receivedByA.Add(m.Message));
            tabB.Messenger.Register<object, StatusChangedMessage>(this, (r, m) => receivedByB.Add(m.Message));

            tabA.Messenger.Send(new StatusChangedMessage("only for A"));

            Assert.Equal(new[] { "only for A" }, receivedByA);
            Assert.Empty(receivedByB);
        }

        [Fact]
        public void Dispose_DoesNotAffectOtherOpenTabs()
        {
            using var provider = BuildServiceProvider();
            var tabA = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());
            using var tabB = new DocumentTab(provider.GetRequiredService<IServiceScopeFactory>());

            tabA.Dispose();

            // tabB's viewmodel/messenger/canvas must still be perfectly usable after a sibling
            // tab's scope (and everything it resolved) has been torn down.
            tabB.Messenger.Send(new StatusChangedMessage("still alive"));
            Assert.False(tabB.Canvas.IsDisposed);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddScoped<IAppMessenger>(_ => new AppMessenger(new WeakReferenceMessenger()));
            services.AddScoped<IEditCommandHistory, EditCommandHistory>();
            services.AddScoped<IScorePlaybackService, FakePlaybackService>();
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

            services.AddSingleton<IScoreFileService, ScoreFileServiceAdapter>();
            services.AddSingleton<IPdfExportService, FakePdfExportService>();
            services.AddSingleton<IMidiExportService, FakeMidiExportService>();
            services.AddSingleton<IMidiImportService, FakeMidiImportService>();
            services.AddSingleton<IAudioImportService, FakeAudioImportService>();
            services.AddSingleton<ISampleLibraryService, SampleLibraryServiceAdapter>();
            services.AddSingleton<IChordTransposeService, ChordTransposeServiceAdapter>();
            services.AddSingleton<IHarmonySuggestionService, HarmonySuggestionServiceAdapter>();
            services.AddSingleton<SampleLibraryViewModel>();
            services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();

            return services.BuildServiceProvider();
        }
    }
}
