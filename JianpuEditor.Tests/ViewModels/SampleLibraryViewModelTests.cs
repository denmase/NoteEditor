using JianpuEditor.Core.Messaging;
using JianpuEditor.Services;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public class SampleLibraryViewModelTests
    {
        [Fact]
        public void RefreshSamples_UsesInjectedService()
        {
            var samples = new FakeSampleLibraryService();
            var viewModel = new SampleLibraryViewModel(samples);

            viewModel.RefreshSamples();

            Assert.Single(viewModel.Samples);
            Assert.True(viewModel.HasSamples);
        }

        [Fact]
        public void GetDisplayName_DelegatesToService()
        {
            var samples = new FakeSampleLibraryService();
            var viewModel = new SampleLibraryViewModel(samples);

            Assert.Equal("demo", viewModel.GetDisplayName(@"C:\sample\demo.jianpu"));
        }

        [Fact]
        public void LoadDemoScore_AppliesToSuppliedDocumentAndMessenger()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var document = ViewModelTestHelper.CreateDocument(messenger);
            var samples = new FakeSampleLibraryService();
            var viewModel = new SampleLibraryViewModel(samples);

            var result = viewModel.LoadDemoScore(document, messenger);

            Assert.True(result.Changed);
            Assert.NotNull(document.Score);
        }
    }
}
