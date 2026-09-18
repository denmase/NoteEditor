using System;
using JianpuEditor.Controls;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JianpuEditor.Glue
{
    /// <summary>
    /// One open document: its own DI scope (so its <see cref="MainViewModel"/>, undo history,
    /// playback transport, and messenger are all independent of every other open document), a
    /// dedicated <see cref="ScoreCanvas"/>, and the glue wiring them together.
    /// </summary>
    /// <remarks>
    /// Deliberately does *not* own a <see cref="MainFormViewBinder"/>: that binds a document's
    /// viewmodel to the single, shared chrome controls (menu items, status bar, chord box, etc.)
    /// that live once on <see cref="MainForm"/>, not per tab -- MainForm creates and disposes one
    /// live binder, pointed at whichever tab is currently active, rather than each tab owning its
    /// own binder that would all fight over the same shared controls.
    /// </remarks>
    internal sealed class DocumentTab : IDisposable
    {
        public DocumentTab(IServiceScopeFactory scopeFactory)
        {
            if (scopeFactory == null)
            {
                throw new ArgumentNullException(nameof(scopeFactory));
            }

            Scope = scopeFactory.CreateScope();
            ViewModel = Scope.ServiceProvider.GetRequiredService<MainViewModel>();
            Messenger = Scope.ServiceProvider.GetRequiredService<IAppMessenger>();
            Canvas = new ScoreCanvas();
            Glue = new ScoreCanvasGlue(ViewModel, Canvas, Messenger);
        }

        public IServiceScope Scope { get; }

        public MainViewModel ViewModel { get; }

        public IAppMessenger Messenger { get; }

        public ScoreCanvas Canvas { get; }

        public ScoreCanvasGlue Glue { get; }

        public void Dispose()
        {
            Glue.Dispose();
            Canvas.Dispose();

            // Cascades to every scoped-registered IDisposable this tab's MainViewModel pulled in
            // (MainViewModel itself, IScorePlaybackService, ...) -- see AppBootstrapper.
            Scope.Dispose();
        }
    }
}
