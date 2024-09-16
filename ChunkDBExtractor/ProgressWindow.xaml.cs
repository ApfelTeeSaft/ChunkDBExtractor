using System.Windows;

namespace ChunkDBExtractor
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(double value, string fileName)
        {
            FileNameTextBlock.Text = $"Processing: {fileName}";
            ProgressBar.Value = value;
        }
    }
}