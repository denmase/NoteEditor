using System.Drawing;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public static class DynamicMarkingLayout
    {
        public static Rectangle GetRowBounds(JianpuRenderer.MeasureLayout layout)
        {
            var top = JianpuRenderer.GetDynamicsRowTop(layout);
            return new Rectangle(layout.X, top, layout.Width, JianpuRenderer.DynamicsRowHeight);
        }
    }
}
