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
        // 날씨 정보를 담을 클래스 정의
        public class list
        {
            public string T1H { get; set; }           // 기온 (온도)
            public string SKY { get; set; }           // 하늘 상태
            public string fcstTime { get; set; }      // 예보 시간 (HH:mm시 형식)
            public string REH { get; set; }           // 습도
            public string PTY { get; set; }           // 강수 형태
            public string Img { get; set; }           // 날씨 아이콘 이미지 경로

            public string fcstTimeRaw { get; set; }   // 원본 시간 문자열 (예: "1300")
            public string RegionName { get; set; }    // 좌표에 해당하는 지역명
        }

        // 좌표 → 지역명 매핑용 클래스
        public class RegionMapper
        {
            // (x, y) 좌표 → 지역명 매핑 딕셔너리
            private Dictionary<(int, int), string> regionMap = new Dictionary<(int, int), string>();

            // CSV 파일을 읽어 좌표-지역명 매핑 딕셔너리에 저장
            public void LoadCsv(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("[오류] CSV 파일 없음: " + filePath);
                    return;
                }

                // CSV 파일을 UTF-8로 읽고 첫 줄(헤더)은 건너뜀
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && int.TryParse(parts[5].Trim(), out int x) && int.TryParse(parts[6].Trim(), out int y))
                    {
                        // 시도명 + 시군구 + 읍면동을 합쳐 지역명 생성
                        string region = $"{parts[2]} {parts[3]} {parts[4]}".Trim();
                        regionMap[(x, y)] = region;
                    }
                }
            }

            // x, y 좌표로 지역명 반환
            public string GetRegionName(int x, int y)
            {
                Console.WriteLine($"총 로드된 좌표 수: {regionMap.Count}");
                if (regionMap.TryGetValue((x, y), out var name))
                {
                    return name;
                }
                else
                {
                    Console.WriteLine($"[매핑 실패] 좌표 ({x},{y})");
                    return "지역 정보 없음";
                }
            }
        }

        // 날씨 데이터를 가져오는 주요 클래스
        public class App
        {
            // HTTP 요청용 클라이언트
            public static readonly HttpClient client = new HttpClient();
            private RegionMapper mapper = new RegionMapper();  // 지역 매핑 객체

            // 생성자: CSV 파일에서 지역 정보를 로드
            public App()
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "위경도(인코딩O).csv");
                mapper.LoadCsv(filePath);
            }

            // 지정한 좌표(x, y)의 날씨 정보를 비동기로 반환
            public async Task<List<list>> GetWeatherAsync(int x, int y, string baseDateOverride = null, string baseTimeOverride = null)
            {
                string baseDate = baseDateOverride ?? DateTime.Now.ToString("yyyyMMdd");
                string baseTime = baseTimeOverride ?? GetBaseTime();

                string apiUrl = $"https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst?serviceKey=TL1oV3WMVx5vBkPAsmJZtyIOCV2twWQWYb7VBSSgxoWNlzFF%2F5%2BSRyK62iUcCDTtypkUkOicvNPh9oTeW9AS1A%3D%3D&pageNo=1&numOfRows=1000&dataType=XML&base_date={baseDate}&base_time={baseTime}&nx={x}&ny={y}";
                Console.WriteLine(apiUrl);


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

            // XML 데이터를 파싱하여 날씨 리스트로 변환
            private List<list> ParseWeatherXml(string xml)
            {
                XDocument xmlDoc = XDocument.Parse(xml);  // XML 문자열 파싱
                var items = xmlDoc.Descendants("item");   // 모든 <item> 요소 추출

                Dictionary<string, list> groupedData = new Dictionary<string, list>(); // 시간별 그룹

                try
                {
                    foreach (var item in items)
                    {
                        string category = item.Element("category")?.Value ?? "";
                        string fcstTime = item.Element("fcstTime")?.Value ?? "";
                        string value = item.Element("fcstValue")?.Value ?? "";

                        // 시간 단위로 그룹핑
                        if (!groupedData.TryGetValue(fcstTime, out var data))
                        {
                            data = new list
                            {
                                fcstTime = $"{fcstTime.Substring(0, 2)}:{fcstTime.Substring(2, 2)}시",
                                fcstTimeRaw = fcstTime
                            };
                            groupedData[fcstTime] = data;
                        }

                        // 항목 카테고리에 따라 값 설정
                        if (category == "T1H")
                        {
                            data.T1H = value + "ºC";
                        }
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
                        {
                            data.REH = value + "%";
                        }
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

                    // 이미지 설정 (PTY 우선, 없으면 SKY 기준)
                    foreach (var data in groupedData.Values)
                    {
                        string imgBasePath = "C:/Users/swoom/source/repos/기상청DLL/img/";

                        if (!string.IsNullOrEmpty(data.PTY) && data.PTY != "없음")
                        {
                            // 강수 형태가 있는 경우 → PTY 텍스트 및 이미지 사용
                            data.Img = imgBasePath + data.PTY.Replace("/", "").Replace(" ", "") + ".png";
                            // data.PTY는 이미 텍스트로 잘 설정되어 있음 (예: "비", "눈")
                        }
                        else
                        {
                            // 강수 없음 → SKY 상태 텍스트도 표시
                            data.PTY = data.SKY; // 하늘 상태를 텍스트로 설정
                            data.Img = imgBasePath + data.SKY + ".png";
                        }
                    }

                    // 시간 순 정렬 후 반환
                    return groupedData.Values.OrderBy(x => x.fcstTime).ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("XML 파싱 중 오류 발생: " + ex.Message);
                }

                return new List<list>();  // 예외 발생 시 빈 리스트 반환
            }

            // 현재 시간 기준으로 가장 가까운 1시간 정시 시간 반환
            private string GetBaseTime()
            {
                DateTime now = DateTime.Now;
                int hour = now.Minute < 45 ? now.Hour - 1 : now.Hour;
                return hour.ToString("D2") + "00";  // 예: "1400"
            }
        }
    }
}
