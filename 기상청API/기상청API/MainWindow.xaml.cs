using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using 기상청DLL; // 만든 DLL 네임스페이스를 참조

namespace 기상날씨앱
{
    public partial class MainWindow : Window
    {
        // DLL에서 제공하는 App 클래스의 인스턴스를 생성
        private Class1.App weatherApp = new Class1.App();
        public MainWindow()
        {
            InitializeComponent(); // WPF UI 초기화\
        }


        // "날씨 조회" 버튼 클릭 이벤트 핸들러
        private async void GetWeather_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                // 아무 것도 선택하지 않았을 경우 경고 메시지 출력
                if (!int.TryParse(TextBox1.Text.Trim(), out int x))
                {
                    MessageBox.Show("x 좌표를 숫자로 입력하세요.");
                    return;
                }

                if (!int.TryParse(TextBox2.Text.Trim(), out int y))
                {
                    MessageBox.Show("y 좌표를 숫자로 입력하세요.");
                    return;
                }
                string targetDate = "20250515";
                string baseTime = "0730";

                // DLL의 GetWeatherAsync 메서드를 비동기로 호출하여 날씨 데이터 가져오기
                List<Class1.list> weatherData = await weatherApp.GetWeatherAsync(x, y, targetDate, baseTime);

       

                // 현재 시각을 "HH00" 형식으로 가져오기 (예: "14:00", "15:00")
                string currentTime = DateTime.Now.ToString("HHmm");


                // 문자열 정리 없이 원본 fcstTimeRaw를 기준으로 비교
                // 내가 보고 싶은 예보 시각
                string targetFcstTime = "0800";
                var filtered = weatherData.FirstOrDefault(w => w.fcstTimeRaw == targetFcstTime);

                if (filtered == null)
                {
                    MessageBox.Show($"예보 시각({targetFcstTime})에 대한 데이터가 없습니다.");
                    return;
                }

                weatherListView.ItemsSource = new List<Class1.list> { filtered };


                // ListView에 바인딩 (가장 가까운 시간의 데이터)
                //weatherListView.ItemsSource = new List<Class1.list> { filtered.Weather };
            } catch (Exception ex) {
                MessageBox.Show("오류" + ex.Message);
            }
        }
    } 
}
