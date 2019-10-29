using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ParallelRecognition;

namespace GuiVariant
{
    public partial class MainWindow : Window
    {
        private ParallelRecognition.ParallelRecognition parallelRecognition = null;
        private string directoryPath = null;
        string DirectoryPath { get => directoryPath; set => directoryPath = value; }

        public ICollectionView ItemsCollectionView { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            if (TryFindResource("key_ObsClassInfo") is ObservableClassInfo tmp)
            {
                tmp.Add(new ClassInfo() { ClassName = "Test", Count = 3});
            };
            if (TryFindResource("key_ObsImageItems") is ObservableImageItem tmp2)
            {
                tmp2.Add(new ImageItem());
            };
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item != null && classesDataList != null)
            {
                if (classesDataList.SelectedItem is null)
                {
                    e.Accepted = true;
                    return;
                }
                e.Accepted = (classesDataList.SelectedItem as ClassInfo).ClassName == (e.Item as ImageItem).PredictedClass;
            } else
            {
                e.Accepted = false;
            }
        }

        private void classesDataList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            (FindResource("key_FilteredView") as CollectionViewSource).View.Refresh();
        }

        private void directorySelectBtn_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
                {
                    DirectoryPath = dialog.SelectedPath;
                    startRecognitionBtn.IsEnabled = true;
                    directorySelectBtn.IsEnabled = false;

                    parallelRecognition = new ParallelRecognition.ParallelRecognition(DirectoryPath);
                    var images = (FindResource("key_ObsImageItems") as ObservableImageItem);
                    images.Clear();
                    foreach (var filePath in Directory.GetFiles(DirectoryPath))
                    {
                        images.Add(new ImageItem
                        {
                            Image = new BitmapImage(new Uri(filePath)),
                            PredictedClass = "TBD",
                            ImagePath = filePath
                        });
                    }
                }
            }
        }

        private void startRecognitionBtn_Click(object sender, RoutedEventArgs e)
        {
            parallelRecognition.Run();
            Thread updaterThread = new Thread(collectionUpdater);
            updaterThread.Start();
            stopRecognitionBtn.IsEnabled = true;
            startRecognitionBtn.IsEnabled = false;
        }

        void collectionUpdater(object data)
        {
            while (!parallelRecognition.HasFinished && parallelRecognition.CreationTimes.Count != 0)
            {
                while (parallelRecognition.CreationTimes.TryDequeue(out string item))
                {
                    ;
                }
                Thread.Sleep(500);
            }
        }

        private void stopRecognitionBtn_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class ObservableImageItem : ObservableCollection<ImageItem> {}
    public class ObservableClassInfo : ObservableCollection<ClassInfo> {}

    public class ImageItem
    {
        public BitmapImage Image { get; set; }
        public string ImagePath { get; set; }
        public string PredictedClass { get; set; }
    }

    public class ClassInfo
    {
        public string ClassName { get; set; }
        public int Count { get; set; }
        public override string ToString()
        {
            return ClassName + " | " + Count.ToString();
        }
    }
}
