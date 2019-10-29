﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

namespace GuiVariant
{
    public partial class MainWindow : Window
    {
        private string directoryPath = null;
        string DirectoryPath { get => directoryPath; set => directoryPath = value; }

        public ICollectionView ItemsCollectionView { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            if (TryFindResource("key_ObsClassInfo") is ObservableClassInfo tmp)
            {
                tmp.Add("Test");
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
                e.Accepted = classesDataList.SelectedItem.ToString() == (e.Item as ImageItem).PredictedClass;
            } else
            {
                e.Accepted = false;
            }
        }

        private void classesDataList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            (FindResource("key_FilteredView") as CollectionViewSource).View.Refresh();
        }
    }

    public class ObservableImageItem : ObservableCollection<ImageItem> {}
    public class ObservableClassInfo : ObservableCollection<string> {}

    public class ImageItem
    {
        public BitmapImage Image { get; set; }
        public string ImagePath { get; set; }
        public string PredictedClass { get; set; }
    }
}
