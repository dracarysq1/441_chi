﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using YOLOv4MLNet.DataStructures;
using YOLOv4MLNet;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObjectDetectionUI
{
    public class ObjectsData : IEnumerable<string>, INotifyCollectionChanged
    {
        public class Value
        {
            public int Count { get; set; }
            public Dictionary<string, List<float[]>> Dict { get; set; }

            public Value(int count, Dictionary<string, List<float[]>> dict)
            {
                this.Count = count;
                this.Dict = dict;
            }
        }

        // dictionary data structure: <object_name, <count, list<files, list<boundaries>>>>
        public Dictionary<string, Value> DetectedObjects { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObjectsData()
        {
            DetectedObjects = new Dictionary<string, Value>();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
      /*  public new bool Add(string elem)
        {
            var result = base.Add(elem);
            OnCollectionChange();
            return result;
        }

        public override void Clear()
        {
            base.Clear();
            OnCollectionChange();
        }

        private void OnCollectionChange() =>
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));*/
        public void Clear()
        {
            DetectedObjects.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Add(List<YoloV4Result> objectsList, string fileName)
        {
            foreach (YoloV4Result obj in objectsList)
                if (DetectedObjects.ContainsKey(obj.Label)) // 外部字典已经有这个对象
                    if (DetectedObjects[obj.Label].Dict.ContainsKey(fileName)) // 内部字典已经有这样的文件名
                    {
                        DetectedObjects[obj.Label].Count += 1;
                        DetectedObjects[obj.Label].Dict[fileName].Add(obj.BBox);
                    }
                    else
                    {
                        DetectedObjects[obj.Label].Count += 1;
                        DetectedObjects[obj.Label].Dict.Add(fileName, new List<float[]>{ obj.BBox });
                    }

                else // 外部字典没有这个对象
                {
                    DetectedObjects.Add(obj.Label, new Value(1, new Dictionary<string, List<float[]>> { { fileName, new List<float[]>{ obj.BBox } } }));
                }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach(string obj in DetectedObjects.Keys)
            {
                yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<string> ProcessedFiles { get; set; }
        public ObjectsData objectsDict;
        private static readonly object myLocker = new object();

        public MainWindow()
        {
            InitializeComponent();
            ProcessedFiles = new ObservableCollection<string>();
            objectsDict = new ObjectsData();

            Predictor.Notify += PredictorEventHandler;
            ProcessedFilesListBox.SelectionChanged += ProcessedFilesListBox_SelectionChanged;
            ObjectsListBox.SelectionChanged += ObjectsListBox_SelectionChanged;

            ProcessedFilesListBox.ItemsSource = ProcessedFiles;
            ObjectsListBox.ItemsSource = objectsDict;
        }
      //  private void SelectOnlySpecified(object sender, FilterEventArgs args) =>
      //     args.Accepted = mainViewModel.SelectSpecified(args.Item);
        private void PredictorEventHandler(string filePath, List<YoloV4Result> objectsList)
        {
            lock (myLocker)//锁住，防止进程之间的竞争
            {
                Dispatcher.Invoke(new Action(() => { objectsDict.Add(objectsList, filePath); ProcessedFiles.Add(Path.GetFileName(filePath)); }));
            }
        }

        private void ObjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ObjectsListBox.SelectedItem == null)
                return;

            // 使用“选定对象”绘制所有图像
            ImageListBox.Items.Clear();
            foreach (string filename in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict.Keys)
            {
                System.Windows.Controls.Image myLocalImage = new System.Windows.Controls.Image();
                myLocalImage.Height = 200;
                myLocalImage.Margin = new Thickness(5);

                BitmapImage myImageSource = new BitmapImage();
                myImageSource.BeginInit();
                myImageSource.UriSource = new Uri(filename);
                myImageSource.EndInit();
                myLocalImage.Source = myImageSource;

                ImageListBox.Items.Add(myLocalImage);
            }

            // 绘制预测器类已标记为“选定对象”的所有对象
            ObjectsImagesListBox.Items.Clear();
            foreach (string filename in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict.Keys)
            {
                foreach (float[] box in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict[filename])
                {
                    int x1 = (int)box[0];
                    int y1 = (int)box[1];
                    int x2 = (int)box[2];
                    int y2 = (int)box[3];

                    System.Windows.Controls.Image myLocalImage = new System.Windows.Controls.Image();
                    myLocalImage.Height = 200;
                    myLocalImage.Margin = new Thickness(5);

                    BitmapImage myImageSource = new BitmapImage();
                    myImageSource.BeginInit();
                    myImageSource.UriSource = new Uri(filename);
                    myImageSource.EndInit();

                    CroppedBitmap cb = new CroppedBitmap((BitmapSource)myImageSource, new Int32Rect(x1, y1, x2 - x1, y2 - y1));
                    myLocalImage.Source = cb;

                    ObjectsImagesListBox.Items.Add(myLocalImage);
                }
            }
        }
        private void ProcessedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProcessedFilesListBox.SelectedItem == null)
                return;

            try
            {
                SelectedImage.Source = new BitmapImage(new Uri(SelectedFolderListBox.Text + "\\" + ProcessedFilesListBox.SelectedItem.ToString()));
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        public void ClearAll()
        {
            ProcessedFiles.Clear();
            objectsDict.Clear();
            Predictor.cancellationTokenSource = new CancellationTokenSource();
        }
        private void OpenMenu_ItemClicked(object sender, RoutedEventArgs e)
        {
            ClearAll();

            System.Windows.Forms.FolderBrowserDialog openFileDlg = new System.Windows.Forms.FolderBrowserDialog();
            openFileDlg.ShowDialog();
            string folderPath = openFileDlg.SelectedPath;
            SelectedFolderListBox.Text = folderPath;
            if (!string.IsNullOrEmpty(folderPath))
            {
                Task t = Task.Factory.StartNew(() =>
                {
                    try
                    {                      
                        _ = Predictor.MakePredictions(folderPath);                      
                    }
                    catch (Exception exc)
                    {
                        Console.Error.WriteLine(exc.Message);
                    }
                });
            }
        }

        private void SelectFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenMenu_ItemClicked(sender, e);
        }
        private void Abort_ButtonClicked(object sender, RoutedEventArgs e)
        {
            InfoButtonTextBlock.Text = "Stop!";
            
            Predictor.cancellationTokenSource.Cancel();
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
    