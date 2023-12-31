﻿using Microsoft.Win32;
using MediaPlayer.Keys;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WMPLib;
using System.Collections.ObjectModel;
using Path = System.IO.Path;
using System.Net;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Media;
using System.Threading.Tasks;

namespace MediaPlayer
{
    public partial class MainWindow : Window
    {
        private Media? currentMedia;
        private bool isPlayingMedia = false;
        private bool isShuffling = false;
        private DispatcherTimer timerVideoTime;
        private TimeSpan totalTime;
        private ObservableCollection<Media> mediaList = new ObservableCollection<Media>();
        private ObservableCollection<Media> recentMediaList = new ObservableCollection<Media>();

        string thumbnail_audio = "Images/musical-note-64x64.png";
        string thumbnail_video = "Images/film-64x64.png";

        bool isDelete = false;

        public class MediaPlaybackInfo
        {
            public Media Media { get; set; }
            public TimeSpan Position { get; set; }
        }
        private List<MediaPlaybackInfo> mediaPlaybackInfos = new List<MediaPlaybackInfo>();

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            HotkeysManager.SetupSystemHook();
            // Save hotkey
            HotkeysManager.AddHotkey(new GlobalHotkey(ModifierKeys.Control, Key.S, savePlaylist));

            // Play & Pause hotkey
            HotkeysManager.AddHotkey(new GlobalHotkey(ModifierKeys.Control, Key.P, playOrPauseMedia));

            // Skip to next media hotkey
            HotkeysManager.AddHotkey(new GlobalHotkey(ModifierKeys.Control, Key.F, playNextMedia));

            // Skip to previous media hotkey
            HotkeysManager.AddHotkey(new GlobalHotkey(ModifierKeys.Control, Key.B, playPreviousMedia));

            // Shuffle on/off hotkey
            HotkeysManager.AddHotkey(new GlobalHotkey(ModifierKeys.Control, Key.H, toggleShuffle));
            canvasPreviewImage = this.canvasPreviewImage;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            if (mediaList.Count == 0)
            {
                saveButton.IsEnabled = false;
            }
            plListView.ItemsSource = mediaList;
            recentListView.ItemsSource = recentMediaList;

            slider.Width = ActualWidth - 300;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            slider.Width = ActualWidth - 300;
            drawPlaybackTimeline();
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Title = "Choose media files";
            openFileDialog.Filter = "Media files|*.mp3;*.mp4;*.wav;*.flac;*.ogg;*.avi;*.mkv|All files|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == true)
            {
                string[] selectedFilePaths = openFileDialog.FileNames;

                foreach (string selectedFilePath in selectedFilePaths)
                {
                    var player = new WindowsMediaPlayer();
                    var clip = player.newMedia(selectedFilePath);

                    string extension = Path.GetExtension(selectedFilePath).ToLower();

                    bool fileExists = mediaList.Any(media => media.FilePath == selectedFilePath);

                    if (fileExists)
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(selectedFilePath);
                        string newFileName = fileNameWithoutExtension;
                        int index = 1;
                        while (mediaList.Any(media => media.Name == newFileName))
                        {
                            newFileName = $"{fileNameWithoutExtension} ({index})";
                            index++;
                        }

                        Media newMedia;

                        if (extension == ".mp3" || extension == ".flac" || extension == ".ogg" || extension == ".wav")
                        {
                            newMedia = new Media(selectedFilePath, newFileName, clip.duration, thumbnail_audio);
                            mediaList.Add(newMedia);

                            mediaPlaybackInfos.Add(new MediaPlaybackInfo
                            {
                                Media = newMedia,
                                Position = TimeSpan.Zero
                            });
                        }
                        else if (extension == ".mp4" || extension == ".avi" || extension == ".mkv")
                        {
                            newMedia = new Media(selectedFilePath, newFileName, clip.duration, thumbnail_video);
                            mediaList.Add(newMedia);

                            mediaPlaybackInfos.Add(new MediaPlaybackInfo
                            {
                                Media = newMedia,
                                Position = TimeSpan.Zero
                            });
                        }
                        else
                        {
                            throw new ArgumentException("Invalid file", nameof(extension));
                        }
                    } else
                    {
                        Media newMedia;

                        if (extension == ".mp3" || extension == ".flac" || extension == ".ogg" || extension == ".wav")
                        {
                            newMedia = new Media(selectedFilePath, clip.duration, thumbnail_audio);
                            mediaList.Add(newMedia);

                            mediaPlaybackInfos.Add(new MediaPlaybackInfo
                            {
                                 Media = newMedia,
                                 Position = TimeSpan.Zero
                            });
                        }
                        else if (extension == ".mp4" || extension == ".avi" || extension == ".mkv")
                        {
                            newMedia = new Media(selectedFilePath, clip.duration, thumbnail_video);
                            mediaList.Add(newMedia);

                            mediaPlaybackInfos.Add(new MediaPlaybackInfo
                             {
                                 Media = newMedia,
                                 Position = TimeSpan.Zero
                             });
                        }
                        else
                        {
                            throw new ArgumentException("Invalid file", nameof(extension));
                        }
                    }
                }
            }
            saveButtonCheck();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            savePlaylist();
        }

        private async void savePlaylist()
        {
            await Task.Delay(100);
            var folderDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select a Folder"
            };

            if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selectedFolderPath = folderDialog.FileName;
                string newFolderName = "";
                string newFolderPath = "";
                int i = 0;
                do
                {
                    if (i == 0)
                    {
                        newFolderName = "MyMediaFolder";
                    }
                    else
                    {
                        newFolderName = $"MyMediaFolder ({i})";
                    }
                    newFolderPath = Path.Combine(selectedFolderPath, newFolderName);
                    i++;
                } while (Directory.Exists(newFolderPath));
                Directory.CreateDirectory(newFolderPath);

                foreach (var media in mediaList)
                {
                    string extension = Path.GetExtension(media?.FilePath);
                    string fileNameWithoutExtension = media?.FileName;
                    if (media?.FileName != media?.Name)
                    {
                        fileNameWithoutExtension = media?.Name;
                    }
                    string fileName = fileNameWithoutExtension + extension;
                    string filePath = Path.Combine(newFolderPath, fileName);
                    WebClient webClient = new WebClient();
                    try
                    {
                        webClient.DownloadFile(media.Source.AbsoluteUri, filePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error downloading file: {ex.Message}");
                    }
                }
            }
        }


        private void addListPathFile(string[] selectedFilePaths)
        {
                foreach (string selectedFilePath in selectedFilePaths)
                {
                    var player = new WindowsMediaPlayer();
                    var clip = player.newMedia(selectedFilePath);

                    string extension = Path.GetExtension(selectedFilePath).ToLower();

                    bool fileExists = mediaList.Any(media => media.FilePath == selectedFilePath);

                    if (!fileExists)
                    {
                        if (extension == ".mp3" || extension == ".flac" || extension == ".ogg" || extension == ".wav")
                        {
                            mediaList.Add(new Media(selectedFilePath, clip.duration, thumbnail_audio));
                        }
                        else if (extension == ".mp4" || extension == ".avi" || extension == ".mkv")
                        {
                            mediaList.Add(new Media(selectedFilePath, clip.duration, thumbnail_video));
                        }
                        else
                        {
                            throw new ArgumentException("Invalid file", nameof(extension));
                        }
                    }
                }
            if (mediaList.Count > 0)
            {
                saveButton.IsEnabled = true;
            }
        }
        private void importButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select a Folder"
            };

            if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                mediaList.Clear();
                string selectedFolderPath = folderDialog.FileName;
                string[] files = Directory.GetFiles(selectedFolderPath, "*", SearchOption.AllDirectories);
                addListPathFile(files);
                saveButtonCheck();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            HotkeysManager.ShutdownSystemHook();
        }

        private void AddPresetButton_Click(object sender, RoutedEventArgs e)
        {
            var addButton = sender as FrameworkElement;
            if (addButton != null)
            {
                addButton.ContextMenu.IsOpen = true;
            }
        }

        private void plListView_SelectionChanged(Object sender, SelectionChangedEventArgs e)
        {
            canvasPreviewImage.Visibility = Visibility.Hidden;

            // This happens after removing a selected item
            if (plListView.SelectedIndex < 0 && currentMedia == null)
            {
                return;
            }
            
            // If user has chosen media before
            if (currentMedia != null)
            {
                foreach (var mediaInfo in mediaPlaybackInfos)
                {
                    if (mediaInfo.Media == currentMedia)
                    {
                        mediaInfo.Position = currentMediaElement.Position;
                        break; 
                    }
                }

                // Stop and close it
                pauseMedia();
                currentMediaElement.Close();
            } 
            // If not
            else
            {   
                hideWelcomeBackground();
            }

            currentMedia = (Media)plListView.SelectedItem;

            // If the selected item is null
            if (currentMedia == null)
            {
               // Hide UI for playing media, show welcome background
               // and return
                hideMediaControl();
                hideVideoBackground();
                hideMusicBackground();
                showWelcomeBackground();
                return;
            }

            updateRecentMedia(currentMedia);

            MediaPlaybackInfo playbackInfo = new MediaPlaybackInfo();
            foreach (var mediaInfo in mediaPlaybackInfos)
            {
                if (mediaInfo.Media == currentMedia)
                {
                    playbackInfo = mediaInfo;
                    break;
                }
            }

            showMediaControl();
            if (currentMedia?.Type == "music")
            {
                hideVideoBackground();
                showMusicBackground();
            }
            else if (currentMedia?.Type == "video")
            {
                hideMusicBackground();
                showVideoBackground();
            }
            if (currentMedia?.FilePath != null)
            {
                currentMediaElement.Source = new Uri(currentMedia?.FilePath, UriKind.Relative);
                if (currentMediaElement.Source != null)
                {
                    handleMedia(playbackInfo);
                }
            }
            Canvas.SetLeft(canvasPreviewImage, 0); // set previewImage to 0
           // previewImage.Visibility = Visibility.Hidden;
        }

        private bool updateRecentMedia(Media media)
        {
            if (media == null)
            {
                return false;
            }
            bool isContained = recentMediaList.Contains(media);
            int i = recentMediaList.IndexOf(media);

            if (isContained)
            {
                recentMediaList.Remove(media);
                recentMediaList.Insert(0, media);
            } else 
            {
                if (recentMediaList.Count == 3)
                {
                    recentMediaList.RemoveAt(recentMediaList.Count - 1);
                }
                recentMediaList.Insert(0, media);
            }

            return true;
        }

        private void hideWelcomeBackground()
        {
            waitingBackground.Visibility = Visibility.Hidden;
            welcomeTextBlock.Visibility = Visibility.Hidden;
            guideTextBlock.Visibility = Visibility.Hidden;
        }

        private void showWelcomeBackground()
        {
            waitingBackground.Visibility = Visibility.Visible;
            welcomeTextBlock.Visibility = Visibility.Visible;
            guideTextBlock.Visibility = Visibility.Visible;
        }

        private void showMusicBackground()
        {
            musicMediaBackground.Visibility = Visibility.Visible;
            musicNote.Visibility = Visibility.Visible;
        }

        private void hideMusicBackground()
        {
            musicMediaBackground.Visibility = Visibility.Hidden;
            musicNote.Visibility = Visibility.Hidden;
        }

        private void showVideoBackground()
        {
            videoMediaBackground.Visibility = Visibility.Visible;
        }

        private void hideVideoBackground()
        {
            videoMediaBackground.Visibility = Visibility.Hidden;
        }

        private void showMediaControl()
        {
            currentMediaElement.Visibility = Visibility.Visible;
            mediaControl.Visibility = Visibility.Visible;
            mediaNameTextBlock.Text = currentMedia?.Name; 
        }

        private void hideMediaControl()
        {
            currentMediaElement.Visibility = Visibility.Hidden;
            mediaControl.Visibility = Visibility.Hidden;
            mediaNameTextBlock.Text = "";
        }

        private void currentMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            currentMediaElement.Position = TimeSpan.Zero;
            drawPlaybackTimeline();

            timerVideoTime.Stop();
            if (!isShuffling)
            {
                if (plListView.SelectedIndex == plListView.Items.Count - 1)
                {
                    plListView.SelectedIndex = 0;
                } else
                {
                    plListView.SelectedIndex += 1;
                }
            } else
            {
                Random rand = new Random();
                int idx = rand.Next(0, plListView.Items.Count);
                plListView.SelectedIndex = idx;
            }

        }

        private void currentMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            updateTimeSlider();
        }

        private void updateTimeSlider()
        {
            totalTime = currentMediaElement.NaturalDuration.TimeSpan;

            // Create a timer that will update the counters and the time slider
            timerVideoTime = new DispatcherTimer();
            timerVideoTime.Interval = TimeSpan.FromSeconds(0.02);
            timerVideoTime.Tick += new EventHandler(timerTick);
            timerVideoTime.Start();
        }

        private void timerTick(object sender, EventArgs e)
        {
            if (totalTime.TotalSeconds > 0)
            {
                Max = (float)totalTime.TotalSeconds;
                Default_value = (float)currentMediaElement.Position.TotalSeconds;
                drawPlaybackTimeline();
            }
        }

        private void handleMedia(MediaPlaybackInfo playbackInfo = null)
        {
            currentMediaElement?.Play();
            playMedia(playbackInfo);
        }

        private void playMediaButton_Click(object sender, RoutedEventArgs e)
        {
            playOrPauseMedia();
        }

        private void backMediaButton_Click(object sender, RoutedEventArgs e)
        {
            playPreviousMedia();
        }

        private void playPreviousMedia()
        {
            int i = plListView.SelectedIndex;
            if (isShuffling)
            {
                if (i != -1)
                {
                    var exclude = new List<int>() { i };
                    int idx = randExclude(0, plListView.Items.Count, exclude);
                    plListView.SelectedIndex = idx;
                } else
                {
                    Random rnd = new Random();
                    plListView.SelectedIndex = rnd.Next(0, plListView.Items.Count);
                }
            } else
            {
                if (i == 0)
                {
                    plListView.SelectedIndex = plListView.Items.Count - 1;
                }
                else
                {
                    plListView.SelectedIndex = i - 1;
                }
            }
        }

        private void nextMediaButton_Click(object sender, RoutedEventArgs e)
        {
            playNextMedia();
        }

        private void playNextMedia()
        {
            int i = plListView.SelectedIndex;
            if (isShuffling)
            {
                if (i != -1)
                {
                    var exclude = new List<int>() { i };
                    int idx = randExclude(0, plListView.Items.Count, exclude);
                    plListView.SelectedIndex = idx;
                }
                else
                {
                    Random rnd = new Random();
                    plListView.SelectedIndex = rnd.Next(0, plListView.Items.Count);
                }
            }
            else
            {
                if (i == plListView.Items.Count - 1)
                {
                    plListView.SelectedIndex = 0;
                }
                else
                {
                    plListView.SelectedIndex = i + 1;
                }
            }
        }

        private void playOrPauseMedia()
        {
            if (isPlayingMedia)
            {
                pauseMedia();
            }
            else
            {
                playMedia();
            }
        }

        private void playMedia(MediaPlaybackInfo playbackInfo = null)
        {
            string workDir = AppDomain.CurrentDomain.BaseDirectory;

            if (playbackInfo != null && playbackInfo.Position > TimeSpan.Zero)
            {
                currentMediaElement.Position = playbackInfo.Position;
            }

            currentMediaElement?.Play();
            isPlayingMedia = true;
            Uri uri = new Uri($"{workDir}/Images/pause.png", UriKind.Absolute);
            playMediaButtonImageSource.Source = new BitmapImage(uri);
            playMediaButtonImageSource.Margin = new Thickness(0, 0, 0, 0);
            canvasPreviewImage.Visibility = Visibility.Hidden;
        }
        private void pauseMedia()
        {
            string workDir = AppDomain.CurrentDomain.BaseDirectory;
            currentMediaElement?.Pause();
            isPlayingMedia = false;
            Uri uri = new Uri($"{workDir}/Images/play.png", UriKind.Absolute);
            playMediaButtonImageSource.Source = new BitmapImage(uri);
            playMediaButtonImageSource.Margin = new Thickness(2, 0, 0, 0);
            canvasPreviewImage.Visibility = Visibility.Hidden;

        }

        private void shuffleButton_Click(object sender, RoutedEventArgs e)
        {
            toggleShuffle();
        }

        private void toggleShuffle()
        {
            if (isShuffling)
            {
                isShuffling = false;
                string workDir = AppDomain.CurrentDomain.BaseDirectory;
                Uri uri = new Uri($"{workDir}/Images/shuffle.png", UriKind.Absolute);
                shuffleButtonImageSource.Source = new BitmapImage(uri);
            }
            else
            {
                isShuffling = true;
                string workDir = AppDomain.CurrentDomain.BaseDirectory;
                Uri uri = new Uri($"{workDir}/Images/shuffle-off.png", UriKind.Absolute);
                shuffleButtonImageSource.Source = new BitmapImage(uri);
            }
        }

        private void saveButtonCheck()
        {
            if (mediaList.Count > 0)
            {
                saveButton.IsEnabled = true;
            } else
            {
                saveButton.IsEnabled = false;
            }
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Tag is Media media)
                {
                    if (media == plListView.SelectedItem)
                    {
                        plListView.SelectedIndex = -1;
                    }
                    mediaList.Remove(media);
                    foreach (var mediaInfo in mediaPlaybackInfos.ToList())
                    {
                        if (mediaInfo.Media == media)
                        {
                            mediaPlaybackInfos.Remove(mediaInfo);
                        }
                    }
                    saveButtonCheck();
                }
            }
        }

        float Default_value = 0.1f, Min = 0.0f, Max = 1.0f;
        bool mouse = false;

        public float Bar(float value)
        {
            return (float)slider.Width * (value - Min) / (float)(Max - Min);
        }

        public void Thumb(float value)
        {
            if (value < Min) value = Min;
            if (value > Max) value = Max;
            Default_value = value;

            drawPlaybackTimeline();
        }

        private void slider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouse = true;
            Point relativePoint = e.GetPosition(slider);

            float value = slider_width((float)relativePoint.X);
            Thumb(value);

            double seconds = totalTime.TotalSeconds * (float)relativePoint.X / slider.Width;
            currentMediaElement.Position = TimeSpan.FromSeconds(seconds);
            canvasPreviewImage.Visibility = Visibility.Hidden;
        }

        private DateTime lastPositionUpdateTime = DateTime.Now;
        private TimeSpan updateInterval = TimeSpan.FromSeconds(0.05);

        private void slider_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouse) return;

            Point relativePoint = e.GetPosition(slider);
            float value = slider_width((float)relativePoint.X);
            Thumb(value);

            DateTime now = DateTime.Now;
            if (now - lastPositionUpdateTime >= updateInterval)
            {
                double seconds = totalTime.TotalSeconds * (float)relativePoint.X / slider.Width;
                currentMediaElement.Position = TimeSpan.FromSeconds(seconds);
                lastPositionUpdateTime = now;

                UpdateCanvasPreviewImage(relativePoint.X);
            }
        }

        private void UpdateCanvasPreviewImage(double sliderPositionX)
        {
            // Calculate the position of the canvas preview image
            double canvasPreviewImageX = sliderPositionX - (canvasPreviewImage.ActualWidth / 2);

            // Ensure the canvas preview image stays within the bounds of the slider
            if (canvasPreviewImageX < 0)
            {
                canvasPreviewImageX = 0;
            }
            else if (canvasPreviewImageX > slider.ActualWidth - canvasPreviewImage.ActualWidth)
            {
                canvasPreviewImageX = slider.ActualWidth - canvasPreviewImage.ActualWidth;
            }
                // Create a RenderTargetBitmap to capture the frame
                int width = (int)currentMediaElement.ActualWidth;
                int height = (int)currentMediaElement.ActualHeight;
            if(currentMedia.FilePath.Contains("mp4"))
            {
                canvasPreviewImage.Background = new SolidColorBrush(Colors.Transparent);
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(width, height, 60, 60, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(currentMediaElement);
                WriteableBitmap bitmap = new WriteableBitmap(renderTargetBitmap);
                previewImage.Source = bitmap;
            }
            else{
                previewImage.Source = currentMedia.PreviewImage;
                canvasPreviewImage.Background = new SolidColorBrush(Colors.Black);
            }
            Canvas.SetLeft(canvasPreviewImage, canvasPreviewImageX);
            canvasPreviewImage.Visibility = Visibility.Visible;

        }

        public float slider_width(float x)
        {
            return Min + (Max - Min) * x / (float)(slider.Width);
        }

        private void slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mouse = false;
            canvasPreviewImage.Visibility = Visibility.Hidden;
        }

        private void Slider_Loaded(object sender, RoutedEventArgs e)
        {
            drawPlaybackTimeline();
        }

        private void drawPlaybackTimeline()
        {
            float bar_side = 0.45f;
            float x = Bar(Default_value);
            int y = (int)(slider.Height * bar_side);

            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            Pen pen = new Pen(Brushes.DarkGray, 4);
            drawingContext.DrawLine(pen, new Point(0, 0), new Point(slider.Width, 0));

            // Vẽ thumb
            Color customColor = (Color)ColorConverter.ConvertFromString("#d01273");
            SolidColorBrush customBrush = new SolidColorBrush(customColor);

            Pen redPen = new Pen(customBrush, 4);
            drawingContext.DrawEllipse(customBrush, redPen, new Point(x + 1, y - 11), 5, 5);

            // Vẽ thanh đã xem
            drawingContext.DrawRectangle(customBrush, null, new Rect(0, y - 13, x, 4));

            drawingContext.Close();
            slider.Source = new DrawingImage(drawingVisual.Drawing);
        }

        private void hidePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            playlistTab.Visibility = Visibility.Collapsed;
            playlistTempTab.Visibility = Visibility.Visible;
        }

        private void showPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            playlistTempTab.Visibility = Visibility.Collapsed;
            playlistTab.Visibility = Visibility.Visible;
        }

        private void toggleRecentButton_Click(object sender, RoutedEventArgs e)
        {
            if (recentListView.Visibility == Visibility.Visible)
            {
                recentListView.Visibility = Visibility.Collapsed;
                toggleRecentButtonImageSource.Source = stringToImageSource("Images/bottom-arrow.png");
            } else
            {
                recentListView.Visibility = Visibility.Visible;
                toggleRecentButtonImageSource.Source = stringToImageSource("Images/top-arrow.png");
            }
        }

        private ImageSource stringToImageSource(string relativeFilePath)
        {
            string workDir = AppDomain.CurrentDomain.BaseDirectory;
            Uri uri = new Uri($"{workDir}/{relativeFilePath}", UriKind.Absolute);
            return new BitmapImage(uri);
        }

        private int randExclude(int start, int count, List<int> exclude)
        {
            var hashSetExclude = new HashSet<int>(exclude);
            var range = Enumerable.Range(start, count).Where(i => !exclude.Contains(i));

            var rand = new System.Random();
            int index = rand.Next(start, count - exclude.Count);
            return range.ElementAt(index);
        }
    }
}
