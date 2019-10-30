﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

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

                    parallelRecognition = new ParallelRecognition.ParallelRecognition(DirectoryPath);
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
            if (parallelRecognition.Run())
            {
                var updaterThread = new Thread(collectionUpdater)
                {
                    IsBackground = true
                };
                updaterThread.Start();
                stopRecognitionBtn.IsEnabled = true;
                startRecognitionBtn.IsEnabled = false;
            }
        }

        private void collectionUpdater()
        {
            var images = (FindResource("key_ObsImageItems") as ObservableImageItem);
            var classes = (FindResource("key_ObsClassInfo") as ObservableClassInfo);
            var imagesView = (FindResource("key_FilteredView") as CollectionViewSource);
            while (!parallelRecognition.HasFinished)
            {
                while (parallelRecognition.CreationTimes.TryDequeue(out ParallelRecognition.ImageClassified item))
                {
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
                    }));
                }
                Thread.Sleep(500);
            }
        }

        private void stopRecognitionBtn_Click(object sender, RoutedEventArgs e)
        {
            parallelRecognition.Stop();
            directorySelectBtn.IsEnabled = true;
            startRecognitionBtn.IsEnabled = false;
            stopRecognitionBtn.IsEnabled = false;
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
}