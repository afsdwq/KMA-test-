using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text;

namespace 기상청DLL
{
    public class Class1
    {
        public class list
        {
            public string T1H { get; set; }           // 기온 (온도)
            public string SKY { get; set; }           // 하늘 상태
            public string fcstTime { get; set; }      // 예보 시간 (HH:mm시 형식)
            public string REH { get; set; }           // 습도
            public string PTY { get; set; }           // 강수 형태
            public string Img { get; set; }           // 아이콘 이미지 경로
            public string fcstTimeRaw { get; set; }   // 원본 시간 문자열 (예: "1300")
            public string RegionName { get; set; }    // 지역명
        }

        public class RegionMapper
        {
            private Dictionary<(int, int), string> regionMap = new Dictionary<(int, int), string>();

            public void LoadCsv(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("[오류] CSV 파일 없음: " + filePath);
                    return;
                }

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && int.TryParse(parts[5].Trim(), out int x) && int.TryParse(parts[6].Trim(), out int y))
                    {
                        string region = $"{parts[2]} {parts[3]} {parts[4]}".Trim();
                        regionMap[(x, y)] = region;
                    }
                }
            }

            public string GetRegionName(int x, int y)
            {
                if (regionMap.TryGetValue((x, y), out var name))
                    return name;
                else
                    return "지역 정보 없음";
            }
        }

        public class App
        {
            public static readonly HttpClient client = new HttpClient();
            private RegionMapper mapper = new RegionMapper();

            public App()
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "위경도(인코딩O).csv");
                mapper.LoadCsv(filePath);
            }

            public async Task<List<list>> GetWeatherAsync(int x, int y, string baseDateOverride = null, string baseTimeOverride = null)
            {
                string baseDate = baseDateOverride ?? DateTime.Now.ToString("yyyyMMdd");
                string baseTime = baseTimeOverride ?? GetBaseTime();

                string apiUrl = $"https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst" +
                                $"?serviceKey=TL1oV3WMVx5vBkPAsmJZtyIOCV2twWQWYb7VBSSgxoWNlzFF%2F5%2BSRyK62iUcCDTtypkUkOicvNPh9oTeW9AS1A%3D%3D" +
                                $"&pageNo=1&numOfRows=1000&dataType=XML&base_date={baseDate}&base_time={baseTime}&nx={x}&ny={y}";

                var response = await client.GetAsync(apiUrl);
                string xmlData = await response.Content.ReadAsStringAsync();
                var weatherList = ParseWeatherXml(xmlData);

                string region = mapper.GetRegionName(x, y);
                foreach (var item in weatherList)
                {
                    item.RegionName = region;
                }

                return weatherList;
            }

            private List<list> ParseWeatherXml(string xml)
            {
                XDocument xmlDoc = XDocument.Parse(xml);
                var items = xmlDoc.Descendants("item");

                Dictionary<string, list> groupedData = new Dictionary<string, list>();

                try
                {
                    foreach (var item in items)
                    {
                        string category = item.Element("category")?.Value ?? "";
                        string fcstTime = item.Element("fcstTime")?.Value ?? "";
                        string value = item.Element("fcstValue")?.Value ?? "";

                        if (!groupedData.TryGetValue(fcstTime, out var data))
                        {
                            data = new list
                            {
                                fcstTime = $"{fcstTime.Substring(0, 2)}:{fcstTime.Substring(2, 2)}시",
                                fcstTimeRaw = fcstTime
                            };
                            groupedData[fcstTime] = data;
                        }

                        if (category == "T1H")
                            data.T1H = value + "ºC";
                        else if (category == "SKY")
                        {
                            switch (value)
                            {
                                case "1": data.SKY = "맑음"; break;
                                case "3": data.SKY = "구름많음"; break;
                                case "4": data.SKY = "흐림"; break;
                            }
                        }
                        else if (category == "REH")
                            data.REH = value + "%";
                        else if (category == "PTY")
                        {
                            switch (value)
                            {
                                case "0": data.PTY = "없음"; break;
                                case "1": data.PTY = "비"; break;
                                case "2": data.PTY = "비/눈"; break;
                                case "3": data.PTY = "눈"; break;
                                case "4": data.PTY = "소나기"; break;
                                case "5": data.PTY = "약한비"; break;
                                case "6": data.PTY = "빗방울눈날림"; break;
                                case "7": data.PTY = "눈날림"; break;
                            }
                        }
                    }

                    // PTY 값에 따라 SKY 표시 여부 조정 및 이미지 경로 설정
                    foreach (var data in groupedData.Values)
                    {
                        string imgBasePath = "C:/Users/swoom/source/repos/기상청DLL/img/";

                        if (!string.IsNullOrEmpty(data.PTY) && data.PTY != "없음")
                        {
                            data.Img = imgBasePath + data.PTY.Replace("/", "").Replace(" ", "") + ".png";
                            data.SKY = "";  // PTY가 있을 때는 SKY는 빈 값으로
                        }
                        else
                        {
                            data.PTY = data.SKY;  // PTY가 없으면 SKY 값으로 대체
                            data.Img = imgBasePath + data.SKY + ".png";
                        }
                    }

                    return groupedData.Values.OrderBy(x => x.fcstTimeRaw).ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("XML 파싱 중 오류 발생: " + ex.Message);
                }

                return new List<list>();
            }

            private string GetBaseTime()
            {
                DateTime now = DateTime.Now;
                int hour = now.Minute < 45 ? now.Hour - 1 : now.Hour;
                if (hour < 0) hour = 23; // 0시 이전 처리
                return hour.ToString("D2") + "00";
            }
        }
    }
}
