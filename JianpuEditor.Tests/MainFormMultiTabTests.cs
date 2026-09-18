using System;
using System.Reflection;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Glue;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Services;
using JianpuEditor.Tests.ViewModels;
using JianpuEditor.ViewModels;
using JianpuEditor.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JianpuEditor.Tests
{
    /// <summary>
    /// End-to-end tests against a real, running <see cref="MainForm"/> (not just its constituent
    /// viewmodels/glue in isolation) covering the multi-tab support added in
    /// AppBootstrapper/DocumentTab/MainForm. Uses reflection to reach private members because
    /// this exercises internal wiring (ActiveTab, CreateTab, CloseTab) that's deliberately not
    /// public API.
    /// </summary>
    public sealed class MainFormMultiTabTests
    {
        [Fact]
        public void StartsWithOneTab_ShowingTheDemoScore()
        {
            using var provider = BuildServiceProvider();
            using var form = CreateForm(provider);
            var tabControl = GetTabControl(form);

            Assert.Single(tabControl.TabPages);
            var tab = GetActiveTab(form);
            Assert.NotNull(tab);
            Assert.Equal("Ode to Joy", tab.ViewModel.Document.Score.Title);
        }

        [Fact]
        public void NewScore_OpensSecondTab_WithIndependentDocument()
        {
            using var provider = BuildServiceProvider();
            using var form = CreateForm(provider);
            var tabControl = GetTabControl(form);
            var firstTab = GetActiveTab(form);

            InvokePrivate(form, "OnNewScore", null, EventArgs.Empty);

            Assert.Equal(2, tabControl.TabPages.Count);
            var secondTab = GetActiveTab(form);
            Assert.NotSame(firstTab, secondTab);
            Assert.NotSame(firstTab.ViewModel, secondTab.ViewModel);
            Assert.NotSame(firstTab.ViewModel.Document, secondTab.ViewModel.Document);

            // Proves per-tab isolation through the whole MainForm wiring (not just DocumentTab in
            // isolation): editing the new tab must not affect the first tab's document.
            secondTab.ViewModel.Document.ApplyHeaderFieldEdit(ScoreHeaderField.Title, "Second Tab Title");
            Assert.Equal("Second Tab Title", secondTab.ViewModel.Document.Title);
            Assert.Equal("Ode to Joy", firstTab.ViewModel.Document.Title);
        }

        [Fact]
        public void CloseTab_RemovesPage_KeepsOtherTabIntact()
        {
            using var provider = BuildServiceProvider();
            using var form = CreateForm(provider);
            var tabControl = GetTabControl(form);
            var firstTab = GetActiveTab(form);

            InvokePrivate(form, "OnNewScore", null, EventArgs.Empty);
            var secondTab = GetActiveTab(form);
            Assert.Equal(2, tabControl.TabPages.Count);

            InvokePrivate(form, "CloseTab", secondTab);

            Assert.Single(tabControl.TabPages);
            var remaining = GetActiveTab(form);
            Assert.Same(firstTab, remaining);
            Assert.Equal("Ode to Joy", remaining.ViewModel.Document.Title);
        }

        [Fact]
        public void CloseLastTab_OpensFreshBlankTabInstead()
        {
            using var provider = BuildServiceProvider();
            using var form = CreateForm(provider);
            var tabControl = GetTabControl(form);
            var onlyTab = GetActiveTab(form);

            InvokePrivate(form, "CloseTab", onlyTab);

            Assert.Single(tabControl.TabPages);
            var freshTab = GetActiveTab(form);
            Assert.NotSame(onlyTab, freshTab);
        }

        [Fact]
        public void CtrlW_ClosesTheActiveTab()
        {
            using var provider = BuildServiceProvider();
            using var form = CreateForm(provider);
            var tabControl = GetTabControl(form);

            InvokePrivate(form, "OnNewScore", null, EventArgs.Empty);
            Assert.Equal(2, tabControl.TabPages.Count);

            var processCmdKey = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance);
            var msg = new Message();
            var handled = (bool)processCmdKey.Invoke(form, new object[] { msg, Keys.Control | Keys.W });

            Assert.True(handled);
            Assert.Single(tabControl.TabPages);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

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

            services.AddSingleton<IScoreFileService, ScoreFileServiceAdapter>();
            services.AddSingleton<IMidiOutput, FakeMidiOutput>();
            services.AddSingleton<IPdfExportService, FakePdfExportService>();
            services.AddSingleton<IMidiExportService, FakeMidiExportService>();
            services.AddSingleton<IMidiImportService, FakeMidiImportService>();
            services.AddSingleton<IAudioImportService, FakeAudioImportService>();
            services.AddSingleton<ISampleLibraryService, SampleLibraryServiceAdapter>();
            services.AddSingleton<IChordTransposeService, ChordTransposeServiceAdapter>();
            services.AddSingleton<IHarmonySuggestionService, HarmonySuggestionServiceAdapter>();
            services.AddSingleton<SampleLibraryViewModel>();
            services.AddSingleton<ILayoutService, WinFormsLayoutService>();
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }

        private static MainForm CreateForm(ServiceProvider provider)
        {
            var form = provider.GetRequiredService<MainForm>();
            form.CreateControl();

            // Load only fires from Show()/Application.Run() in a real app; invoke it directly so
            // this test doesn't depend on a visible top-level window or a running message loop.
            var onFormLoad = typeof(MainForm).GetMethod("OnFormLoad", BindingFlags.NonPublic | BindingFlags.Instance);
            onFormLoad.Invoke(form, new object[] { form, EventArgs.Empty });
            return form;
        }

        private static TabControl GetTabControl(MainForm form)
        {
            return (TabControl)typeof(MainForm).GetField("_tabControl", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        }

        private static DocumentTab GetActiveTab(MainForm form)
        {
            return GetTabControl(form).SelectedTab?.Tag as DocumentTab;
        }

        private static object InvokePrivate(MainForm form, string methodName, params object[] args)
        {
            var method = typeof(MainForm).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                return method.Invoke(form, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }
    }
}
