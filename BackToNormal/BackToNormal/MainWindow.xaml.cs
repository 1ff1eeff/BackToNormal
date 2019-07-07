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

        List<string> outputs = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
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
                ResultsTextBox.Clear();
                StopButton.Visibility = Visibility.Visible;
                await AccessTheWebAsync(UrlTextBox.Text.ToString(), cts.Token);
                ResultsTextBox.Text += "Загрузка завершена.";
            }
            catch (OperationCanceledException)
            {
                ResultsTextBox.Text += "Загрузка отменена.";
            }
            catch (Exception)
            {
                ResultsTextBox.Text += "Загрузка не удалась.";
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

        /// <summary>
        /// Обработка
        /// </summary>
        /// 
        private async void Combine_Click(object sender, RoutedEventArgs e)
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
                    Directory.CreateDirectory("tmp");
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(image);
                    string fileName = Path.GetFileName(image);
                    ResultsTextBox.Text += "Обрабатывается: " + fileName + '\n';
                    DirectoryInfo di = Directory.CreateDirectory("tmp\\" + fileNameWithoutExtension);

                    string outDirName = "";
                    Regex regex = new Regex(@"[\\d]");
                    Match match = regex.Match(OutTextBox.Text);
                    while (match.Success)
                    {
                        outDirName += match.Value;
                        match = match.NextMatch();
                    }
                    await SplitToImagesAsync(image, row, col, di.FullName, cts2.Token);
                    string[] stitchedImages = Directory.GetFiles(di.FullName);
                    Image combinedImage = await CombineAsync(stitchedImages, row, col, cts3.Token);
                    Directory.CreateDirectory(OutTextBox.Text);
                    combinedImage.Save(OutTextBox.Text + "\\" + fileName, System.Drawing.Imaging.ImageFormat.Png);
                    combinedImage.Dispose();
                }

                ResultsTextBox.Text += "\nИзображения обработаны.";

            }
            catch (OperationCanceledException)
            {
                ResultsTextBox.Text += "Обработка отменена.";
            }
            catch (Exception)
            {
                ResultsTextBox.Text += "Обработка не удалась.";
            }
            finally
            {
                CombineButton.IsEnabled = true;
                StopButton2.Visibility = Visibility.Hidden;
            }
            
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
            ResultsTextBox.Text += output5;


            int i = 0;
            foreach (string imageSrc in outputs)
            {
                WebClient webClient = new WebClient();
                DirectoryInfo di = Directory.CreateDirectory(SrcTextBox.Text);
                webClient.DownloadFileAsync(new Uri(imageSrc), SrcTextBox.Text + "IMG-" + i++.ToString() + ".png");
               
            }
            i = 0;

        }



        static async Task<Image> CombineAsync(string[] files, int row, int col, CancellationToken token)
        {
            return await Task.Run(() => Combine(files, row, col, token));
        }
        public static Image Combine(string[] files, int row, int col, CancellationToken token)
        {
            Image img = null;
            try
            {
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(files[0]);
                int width = bitmap.Width * row;
                int height = bitmap.Height * col;
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
                            if (token.IsCancellationRequested)
                            {
                                img.Dispose();
                                return img;
                            }
                            g.DrawImage(Image.FromFile(files[curr]), new Point(offsetX, offsetY));
                            offsetX += bitmap.Width;
                            curr++;
                        }
                        offsetX = 0;
                        offsetY += bitmap.Height;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SrcTextBox.Text = System.AppDomain.CurrentDomain.BaseDirectory + "Bad\\";
            OutTextBox.Text = System.AppDomain.CurrentDomain.BaseDirectory + "Good\\";

        }

        
    }
}
