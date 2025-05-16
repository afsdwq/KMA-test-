using System;
using System.Linq;
using System.Windows;
using 기상청DLL;

namespace 기상날씨앱
{
    public partial class MainWindow : Window
    {
        private Class1.App weatherApp = new Class1.App();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void GetWeather_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TextBox1.Text.Trim(), out int x))
                {
                    MessageBox.Show("X 좌표를 숫자로 입력하세요.");
                    return;
                }

                if (!int.TryParse(TextBox2.Text.Trim(), out int y))
                {
                    MessageBox.Show("Y 좌표를 숫자로 입력하세요.");
                    return;
                }

                string targetFcstTime = TextBoxFcstTime.Text.Trim();
                if (string.IsNullOrEmpty(targetFcstTime))
                {
                    targetFcstTime = DateTime.Now.ToString("HH00");  // 기본값 예: 1400
                }

                var weatherData = await weatherApp.GetWeatherAsync(x, y);

                var data = weatherData.FirstOrDefault(w => w.fcstTimeRaw == targetFcstTime);

                if (data == null)
                {
                    TextBlockWeather.Text = $"예보 시간({targetFcstTime})에 대한 데이터가 없습니다.";
                    return;
                }

                // PTY 와 SKY를 같이 표시 (PTY가 있으면 PTY, 없으면 SKY)
                string skyOrPty = string.IsNullOrEmpty(data.PTY) || data.PTY == "없음" ? data.SKY : data.PTY;

                string output = $"지역: {data.RegionName}\n" +
                                $"예보 시간: {data.fcstTime}\n" +
                                $"기온: {data.T1H}\n" +
                                $"습도: {data.REH}\n" +
                                $"강수형태/하늘: {skyOrPty}";

                TextBlockWeather.Text = output;
            }
            catch (Exception ex)
            {
                MessageBox.Show("오류: " + ex.Message);
            }
        }
    }
}
