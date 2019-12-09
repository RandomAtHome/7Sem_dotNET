//variant a
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GuiVariant
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly string SERVER_URL = "https://localhost:44389/recognize/";
        private static CancellationTokenSource cancelTokens = new CancellationTokenSource();

        string DirectoryPath { get; set; } = null;

        public ICollectionView ItemsCollectionView { get; set; }
        public MainWindow()
        {
            InitializeComponent();
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
            }
            else
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
                    clearDatabase.IsEnabled = true;

                    var additionThread = new Thread(AddImagesToCollection)
                    {
                        IsBackground = true
                    };
                    additionThread.Start();
                }
            }
        }

        private void AddImagesToCollection()
        {
            var images = (FindResource("key_ObsImageItems") as ObservableImageItem);
            Dispatcher.BeginInvoke(new Action(() => images.Clear()));
            foreach (var filePath in Directory.GetFiles(DirectoryPath))
            {
                Dispatcher.BeginInvoke(new Action(() => images.Add(new ImageItem()
                {
                    Image = new BitmapImage(new Uri(filePath)),
                    PredictedClass = "TBD",
                    ImagePath = filePath
                })
                ));
            }
        }

        private void startRecognitionBtn_Click(object sender, RoutedEventArgs e)
        {
            var updaterThread = new Thread(collectionUpdater)
            {
                IsBackground = true
            };
            updaterThread.Start();
            startRecognitionBtn.IsEnabled = false;
            stopRecognitionBtn.IsEnabled = true;
            clearDatabase.IsEnabled = false;
        }

        private void collectionUpdater()
        {
            try
            {
                if (httpClient.GetAsync(SERVER_URL).Result.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Recognition failed! Wrong URL", "Info");
                        stopRecognitionBtn_Click(null, null);
                    }));
                    return;
                }
            } catch(AggregateException)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show("Recognition failed! Connection timed out", "Info");
                    stopRecognitionBtn_Click(null, null);
                }));
                return;
            }
            var images = (FindResource("key_ObsImageItems") as ObservableImageItem);
            var classes = (FindResource("key_ObsClassInfo") as ObservableClassInfo);
            var imagesView = (FindResource("key_FilteredView") as CollectionViewSource);
            try
            {
                foreach (var filename in Directory.GetFiles(DirectoryPath))
                {
                    var bi = new FileDescription() { Name = filename, Content = Convert.ToBase64String(File.ReadAllBytes(filename)) };
                    var dataAsString = JsonConvert.SerializeObject(bi);
                    var content = new StringContent(dataAsString);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    var httpResponse = httpClient.PostAsync(SERVER_URL, content, cancelTokens.Token).Result;
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var item = JsonConvert.DeserializeObject<ImageClassified>(httpResponse.Content.ReadAsStringAsync().Result);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var itemToUpdate = images.FirstOrDefault(i => i.ImagePath == item.ImagePath);
                            if (itemToUpdate != null)
                            {
                                itemToUpdate.PredictedClass = item.ClassName;
                                imagesView.View.Refresh();
                            }
                            var classToUpdate = classes.FirstOrDefault(i => i.ClassName == item.ClassName);
                            if (classToUpdate != null)
                            {
                                classToUpdate.Count++;
                                classesDataList.Items.Refresh();
                            }
                            else
                            {
                                classes.Add(new ClassInfo() { ClassName = item.ClassName, Count = 1 });
                            }
                        })).Wait();
                    }
                }
            } catch (AggregateException) {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show("Recognition was interrupted!", "Info");
                })).Wait();
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                imagesView.View.Refresh();
                classesDataList.Items.Refresh();
                startRecognitionBtn.IsEnabled = true;
                clearDatabase.IsEnabled = true;
                directorySelectBtn.IsEnabled = true;
                stopRecognitionBtn.IsEnabled = false;
                MessageBox.Show("Recognition finished!", "Info");
            })).Wait();
        }

        private void stopRecognitionBtn_Click(object sender, RoutedEventArgs e)
        {
            cancelTokens.Cancel(false);
            cancelTokens.Dispose();
            cancelTokens = new CancellationTokenSource();
        }

        private void clearDatabase_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var answer = MessageBox.Show("Do you wish to clear stats?", "Warning", MessageBoxButton.YesNo);
                if (answer is MessageBoxResult.No) return;
                try
                {
                    var response = httpClient.DeleteAsync(SERVER_URL).Result;
                }
                catch (AggregateException)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Clearing stats failed! Server is unresponsive", "Info");
                    }));
                    return;
                }

                MessageBox.Show("Tables successfully truncated!", "Info");
            });
        }

        private void getHitStats_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    var response = httpClient.GetAsync(SERVER_URL).Result;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        hitStatsList.Items.Clear();
                    }));
                    var hits = JsonConvert.DeserializeObject<string[]>(response.Content.ReadAsStringAsync().Result);
                    foreach (var hit in hits)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            hitStatsList.Items.Add(hit);
                        }));
                    }
                    if (hits.Length == 0)
                    {
                        MessageBox.Show("Server returned 0 stats", "Info");
                    }
                }
                catch (AggregateException)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Stats getting failed! Server is unresponsive", "Info");
                    }));
                }
            });
        }
    }

    public class ObservableImageItem : ObservableCollection<ImageItem> { }
    public class ObservableClassInfo : ObservableCollection<ClassInfo> { }

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

    public class FileDescription
    {
        public string Name { get; set; }
        public string Content { get; set; }
    }

    public class ImageClassified
    {
        public string ImagePath { get; set; }
        public string ClassName { get; set; }
        public double Certainty { get; set; }
    }
}
