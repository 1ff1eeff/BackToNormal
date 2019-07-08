using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using System.Drawing;
using Image = System.Drawing.Image;
using System.Net;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.IO;
using Point = System.Drawing.Point;

namespace BackToNormal
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    ///    

    public partial class MainWindow : Window
    {

        CancellationTokenSource cts;
        CancellationTokenSource cts2;
        CancellationTokenSource cts3;
        readonly List<string> outputs = new List<string>();

        string BadDirectory = "";
        string GoodDirectory = "";


        public MainWindow()
        {
            InitializeComponent();
        }       

        /// <summary>
        /// Обработка
        /// </summary>
        /// 
        private async void Combine_Click(object sender, RoutedEventArgs e)
        {

            if (new DirectoryInfo(BadDirectory).Exists)
            {

                CombineButton.IsEnabled = false;

                try
                {
                    cts2 = new CancellationTokenSource();
                    StopButton2.Visibility = Visibility.Visible;

                    cts3 = new CancellationTokenSource();


                    int row = 4;
                    int col = 4;

                    string[] files = { "" };
                    if (SrcTextBox.Text != "")
                    {
                        files = Directory.GetFiles(SrcTextBox.Text);
                    }

                    foreach (string image in files)
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(image);
                        string fileName = Path.GetFileName(image);
                        ResultsTextBox.AppendText("Обрабатывается: " + fileName + '\n');
                        DirectoryInfo di = Directory.CreateDirectory(SrcTextBox.Text + "Temp\\" + fileNameWithoutExtension);
                        string nameFromURL = "";
                        Regex regex = new Regex(@"[\d]");
                        Match match = regex.Match(UrlTextBox.Text);
                        while (match.Success)
                        {
                            nameFromURL += match.Value;
                            match = match.NextMatch();
                        }
                        await SplitToImagesAsync(image, row, col, di.FullName, cts2.Token);
                        string[] stitchedImages = Directory.GetFiles(di.FullName);
                        DirectoryInfo di2 = Directory.CreateDirectory(OutTextBox.Text + "\\" + nameFromURL);
                        Image combinedImage = await CombineAsync(stitchedImages, row, col, cts3.Token);
                        combinedImage.Save(di2.FullName + "\\" + fileName, System.Drawing.Imaging.ImageFormat.Png);
                        combinedImage.Dispose();

                        //Purge(di);
                    }
                    ResultsTextBox.AppendText("Изображения обработаны.\n");

                    //DirectoryInfo bad = new DirectoryInfo(BadDirectory);
                    //while (bad.Exists)
                    //{
                    //    Thread.Sleep(100);
                    //    ResultsTextBox.AppendText("Папка \"" + bad.Name + "\" используется. Пожалуйста подождите ещё.\n");
                    //    Purge(bad);
                    //}
                }
                catch (OperationCanceledException)
                {
                    ResultsTextBox.AppendText("Обработка отменена.\n");
                }
                catch (Exception)
                {
                    ResultsTextBox.AppendText("Обработка не удалась.\n");
                }
                finally
                {
                    CombineButton.IsEnabled = true;
                    StopButton2.Visibility = Visibility.Hidden;
                }
            }
            else
                ResultsTextBox.AppendText("Директория: \"" + BadDirectory + "\" не найдена.\n");
        }

        private void StopButton2_Click(object sender, RoutedEventArgs e)
        {
            if (cts2 != null)
            {
                cts2.Cancel();
            }
        }

        

        async Task AccessTheWebAsync(string url, CancellationToken ct)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url, 0, ct);
            string output = await response.Content.ReadAsStringAsync();
            string output2 = "", output3 = "", output4 = "", output5 = "";

            if (new DirectoryInfo(BadDirectory).Exists) {
                Purge(new DirectoryInfo(BadDirectory));
            }

            //В теге <img> с классами "page-image js-page-image hidden"
            Regex regex1 = new Regex(@"<img\n\s\s\s\sclass=""page-image\sjs-page-image\shidden""[^>]+>");
            Match match = regex1.Match(output);
            while (match.Success)
            {
                output2 += match.Value;
                match = match.NextMatch();
            }
            //Строка data-src=""
            Regex regex2 = new Regex(@"data-src=""[^""]+""");
            match = regex2.Match(output2);
            while (match.Success)
            {
                output3 += match.Value;
                match = match.NextMatch();
            }
            //В кавычках
            Regex regex3 = new Regex(@"""(.*?)""");
            match = regex3.Match(output3);
            while (match.Success)
            {
                output4 += match.Value;
                output4 += "\n";
                match = match.NextMatch();
            }
            //Без кавычек
            Regex regex4 = new Regex(@"[^""]");
            match = regex4.Match(output4);
            while (match.Success)
            {
                output5 += match.Value;
                match = match.NextMatch();
            }

            string[] words = output5.Split('\n');
            foreach (string word in words)
            {
                if (word != "")
                    outputs.Add(word);
            }
            ResultsTextBox.AppendText(output5);
            
            int i = 0;
            foreach (string imageSrc in outputs)
            {
                WebClient webClient = new WebClient();
                DirectoryInfo bad = Directory.CreateDirectory(SrcTextBox.Text);
                webClient.DownloadFileAsync(new Uri(imageSrc), SrcTextBox.Text + "IMG-" + i++.ToString() + ".png");                               
            }            
            i = 0;
            outputs.Clear();

        }



        static async Task<Image> CombineAsync(string[] files, int row, int col, CancellationToken token)
        {
            return await Task.Run(() => Combine(files, row, col, token));
        }
        private static Image Combine(string[] files, int row, int col, CancellationToken token)
        {            
            Image img = null;
            try
            {
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(files[0]);
                int width = bitmap.Width * row; //280 px
                int height = bitmap.Height * col; //400 px
                img = new Bitmap(width, height);

                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(img))
                {
                    Rectangle fillRect = new Rectangle(0, 0, img.Width, img.Height);
                    SolidBrush tBrush = new SolidBrush(Color.Transparent);
                    Region fillRegion = new Region(fillRect);

                    g.FillRegion(tBrush, fillRegion);

                    foreach (string image in files)
                    {
                        g.DrawImage(Image.FromFile(image), new Point(0, 0));
                    }

                    int offsetX = 0;
                    int offsetY = 0;
                    int curr = 0;
                    for (int i = 0; i < col; i++)
                    {
                        for (int j = 0; j < row; j++)
                        {                            
                            g.DrawImage(Image.FromFile(files[curr]), new Point(offsetX, offsetY));
                            offsetX += bitmap.Width;
                            curr++;
                        }
                        offsetX = 0;
                        offsetY += bitmap.Height;
                    }
                    if (token.IsCancellationRequested)
                    {
                        return img;
                    }
                    
                    bitmap.Dispose();
                    g.Dispose();
                }
                return img;
            }
            catch (Exception ex)
            {
                if (img != null)
                    img.Dispose();
                throw ex;
            }

        }



        static async Task SplitToImagesAsync(string image, int row, int col, string outDir, CancellationToken token)
        {
            await Task.Run(() => SplitToImages(image, row, col, outDir, token));

        }
        private static void SplitToImages(string image, int row, int col, string outDir, CancellationToken token)
        {
            Image img = Image.FromFile(image);
            
            //img = CropImage(img, 0, 0, 0, 0);

            int offsetX = 0;
            int offsetY = 0;
            int width = img.Width / row - 1;
            int height = img.Height / col;


            for (int i = 0; i < col; i++)
            {
                for (int j = 0; j < row; j++)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    Rectangle cropRect = new Rectangle(offsetX, offsetY, width, height);
                    Bitmap bmpImage = new Bitmap(img);
                    Bitmap bmpCrop = bmpImage.Clone(cropRect, bmpImage.PixelFormat);
                    Image finalImg = (Image)(bmpCrop);
                    finalImg.Save(outDir + "\\" + @"OUT-" + j.ToString()+ "-" + i.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);
                    offsetX += width;
                    finalImg.Dispose();
                    bmpImage.Dispose();
                    bmpCrop.Dispose();
                }
                offsetX = 0;
                offsetY += height;

            }
            
        }

        /// <summary>
        /// Обрезать изображение. Срезать слева, справа, сверху или снизу.
        /// </summary>
        /// 
        private static Image CropImage(Image img, int left, int right, int top, int bottom)
        {
            //Image img = Image.FromFile(image);
            Rectangle cropRect = new Rectangle(left, top, img.Width - right, img.Height - bottom);
            Bitmap bmpImage = new Bitmap(img);
            Bitmap bmpCrop = bmpImage.Clone(cropRect, bmpImage.PixelFormat);
            return (Image)(bmpCrop);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutTextBox.Text = fbd.SelectedPath.ToString();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SrcTextBox.Text = fbd.SelectedPath.ToString();
            }
        }

        /// <summary>
        /// Загрузка
        /// </summary>
        /// 
        private async void Start_Click(object sender, RoutedEventArgs e)
        {

            StartButton.IsEnabled = false;

            try
            {
                cts = new CancellationTokenSource();
                StopButton.Visibility = Visibility.Visible;
                await AccessTheWebAsync(UrlTextBox.Text.ToString(), cts.Token);
                ResultsTextBox.AppendText("Загрузка завершена.\n");
                if (new DirectoryInfo(BadDirectory).Exists)
                    PurgeButton.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException)
            {
                ResultsTextBox.AppendText("Загрузка отменена.\n");
            }
            catch (Exception)
            {
                ResultsTextBox.AppendText("Загрузка не удалась.\n");
            }
            finally
            {
                StartButton.IsEnabled = true;
                StopButton.Visibility = Visibility.Hidden;
            }

        }

        /// <summary>
        /// Остановить загрузку
        /// </summary>
        /// 
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResultsTextBox.AppendText(
                "\"URL\" - страница манги на comic-gardo. \n" +
                "\"Bad\" - папка для перепутанных изображений с сайта. \n" +
                "\"Good\" - папка для обработаных изображений. \n" +
                "\"Загрузить\" - загрузить изображения с сайта в папку Bad. \n" +
                "\"Обработать\" - восстановить порядок в изображениях и положить в папку Good.\n" +
                "\"Purge\" (временная) - удалить папку \"Bad\". Может потребовать подождать некоторое время.\n" +
                "===============================================================\n"

                );
            BadDirectory = System.AppDomain.CurrentDomain.BaseDirectory + "Bad\\";
            SrcTextBox.Text = BadDirectory;
            GoodDirectory = System.AppDomain.CurrentDomain.BaseDirectory + "Good\\";
            OutTextBox.Text = GoodDirectory;
            if(new DirectoryInfo(BadDirectory).Exists)
                PurgeButton.Visibility = Visibility.Visible;
        }

        private void ResultsTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ResultsTextBox.ScrollToEnd();
        }

        private void Purge(DirectoryInfo di)
        { 
            try
            {
                Directory.Delete(di.FullName,true);
                //Directory.Delete(@"C:\Users\Пользователь\source\repos\BackToNormal\BackToNormal\bin\Debug\Bad\Temp", true);
                //ResultsTextBox.AppendText("Папка Bad - удалена.\n");

                //foreach (FileInfo file in di.EnumerateFiles())
                //{
                //    //if (WaitForFile(file.FullName))
                //        file.Delete();
                //}
                //foreach (DirectoryInfo dir in di.EnumerateDirectories())
                //{
                //    dir.Delete(true);
                //}
                ResultsTextBox.AppendText("Папка \"" + di.Name + "\" из каталога " + di.Parent.FullName + " успешно удалена!\n");
                PurgeButton.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                if (di.Exists)
                    ResultsTextBox.AppendText("Папка \"" + di.Name + "\" используется. Пожалуйста подождите ещё.\n");
                else
                    ResultsTextBox.AppendText("Папка \"" + di.Name + "\" не найдена в каталоге " + di.Parent.FullName + "\n");
                //ResultsTextBox.AppendText(ex.Message + "\n");
            }
            finally
            {
                
            }
        }

        
        /// <summary>
        /// Blocks until the file is not locked any more.
        /// </summary>
        public bool WaitForFile(string fullPath)
        {
            int numTries = 0;
            while (true)
            {
                ++numTries;
                try
                {
                    // Attempt to open the file exclusively.
                    using (FileStream fs = new FileStream(fullPath,
                        FileMode.Open, FileAccess.ReadWrite,
                        FileShare.None, 100))
                    {
                        fs.ReadByte();

                        // If we got this far the file is ready
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ResultsTextBox.AppendText(
                        "WaitForFile " + fullPath + " failed to get an exclusive lock: " + ex.ToString()
                        );

                    if (numTries > 10)
                    {
                        ResultsTextBox.AppendText(
                            "WaitForFile " + fullPath + " giving up after 10 tries"
                            );
                        return false;
                    }

                    // Wait for the lock to be released
                    System.Threading.Thread.Sleep(500);
                }
            }

            ResultsTextBox.AppendText("WaitForFile " + fullPath + " returning true after " + numTries + " tries"
                );
            return true;
        }

        private void Purge_Click(object sender, RoutedEventArgs e)
        {
            Purge(new DirectoryInfo(BadDirectory));
        }

    }
}
