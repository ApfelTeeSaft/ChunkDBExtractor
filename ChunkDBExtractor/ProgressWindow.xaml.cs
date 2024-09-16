using System;
using System.Windows;
using System.Windows.Threading;

namespace ChunkDBExtractor
{
    public partial class ProgressWindow : Window
    {
        private DispatcherTimer animationTimer;
        private int dotCount = 0;
        private string baseText = "Processing";

        public ProgressWindow()
        {
            InitializeComponent();

            // Initialize the animation timer
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(500); // Update every 500ms
            animationTimer.Tick += UpdateAnimation;
        }

        private void UpdateAnimation(object sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % 4; // Cycle between 0, 1, 2, 3
            string dots = new string('.', dotCount);
            FileNameTextBlock.Text = $"{baseText}{dots}";
        }

        public void StartAnimation()
        {
            animationTimer.Start();
        }

        public void StopAnimation()
        {
            Dispatcher.Invoke(() =>
            {
                animationTimer.Stop();
                FileNameTextBlock.Text = "Extraction Complete";
            });
        }

        public void UpdateProgress(string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                baseText = $"Processing: {fileName}"; // Update the base text with the current filename
            });
        }
    }
}